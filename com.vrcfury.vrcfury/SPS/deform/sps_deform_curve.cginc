#ifndef SPS_INC_DEFORM_CURVE
#define SPS_INC_DEFORM_CURVE

#include "sps_deform_path.cginc"
#include "../common/sps_types.cginc"
#include "../common/sps_utils.cginc"
#include "sps_deform_bezier.cginc"
#include "sps_deform_control_points.cginc"
#include "sps_deform_globals.cginc"

inline void sps_deform_walk_chain(
    SpsDeformNode nodes[SPS_CHAIN_MAX_SOCKETS + 1],
    float worldLength,
    float targetDistance,
    out float outFirstSegmentLerp,
    out float outRadiusMult,
    out float3 outPosition,
    out float3 outForward,
    out float3 outUp
) {
    float remainingDistance = max(targetDistance, 0);
    float walkedLength = 0;
    uint terminalFlags = 0u;
    outFirstSegmentLerp = 0;
    outRadiusMult = 1;
    outPosition = nodes[0].world;
    outForward = nodes[0].forward;
    if (sps_is_zero(outForward)) outForward = float3(0, 0, 1);
    outForward = sps_normalize(outForward);
    outUp = sps_nearest_normal(outForward, nodes[0].up);
    float3 startPoint = outPosition;
    float3 startForward = outForward;
    float3 currentUp = outUp;
    uint previousFlags = 0u;

    [fastopt]
    for (uint sampleIndex = 1; sampleIndex <= SPS_CHAIN_MAX_SOCKETS; sampleIndex++) {
        float3 endPoint = nodes[sampleIndex].world;
        float3 endForward = nodes[sampleIndex].forward;
        if (sps_is_zero(endForward)) break;
        uint socketFlags = nodes[sampleIndex].flags;
        bool isGuidedSegment = sps_has_flag(socketFlags, SPS_SOCKET_FLAG_GUIDED);
        float segmentPulloutLerp;

        terminalFlags = socketFlags;
        float3 p0;
        float3 p1;
        float3 p2;
        float3 p3;
        sps_deform_segment_control_points(
            startPoint,
            startForward,
            endPoint,
            endForward,
            isGuidedSegment,
            worldLength,
            max(worldLength - walkedLength, 0),
            p0, p1, p2, p3,
            segmentPulloutLerp
        );

        if (sampleIndex == 1) outFirstSegmentLerp = segmentPulloutLerp;

        float nextRemainingDistance;
        float3 samplePosition;
        float3 sampleForward;
        float3 sampleUp;
        sps_bezierSolve(
            p0, p1, p2, p3,
            remainingDistance,
            currentUp,
            nextRemainingDistance,
            samplePosition,
            sampleForward,
            sampleUp
        );

        outPosition = samplePosition;
        outForward = sampleForward;
        outUp = sampleUp;
        float consumedDistance = remainingDistance - nextRemainingDistance;
        remainingDistance = nextRemainingDistance;
        walkedLength += consumedDistance;

        if (remainingDistance <= 0) {
            if (sps_has_flag(previousFlags, SPS_SOCKET_FLAG_HOLE)) {
                outRadiusMult = 0;
            }
            return;
        }

        currentUp = sampleUp;
        previousFlags = socketFlags;
        startPoint = endPoint;
        startForward = endForward;
    }

    if (sps_has_flag(terminalFlags, SPS_SOCKET_FLAG_HOLE)) {
        float collapseStart = worldLength * 0.05;
        float collapseEnd = worldLength * 0.1;
        if (remainingDistance > collapseEnd) {
            remainingDistance = collapseEnd;
        }
        outRadiusMult = sps_saturated_map(
            remainingDistance,
            collapseEnd,
            collapseStart
        );
        if (!sps_to_bool(_SPS_Overrun) && remainingDistance > 0) {
            remainingDistance = 0;
        }
    }
    outPosition += outForward * remainingDistance;
}

#endif
