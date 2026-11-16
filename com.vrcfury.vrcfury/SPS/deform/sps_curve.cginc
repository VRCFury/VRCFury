#ifndef SPS_INC_CURVE
#define SPS_INC_CURVE

#include "sps_config.cginc"

#include "sps_path.cginc"
#include "sps_types.cginc"
#include "sps_utils.cginc"
#include "sps_bezier.cginc"
#include "sps_control_points.cginc"
#include "sps_globals.cginc"

inline void sps_deform_walk_chain(
    SpsDeformPathInfo path,
    float targetDistance,
    out float outDeformationWeight,
    out float outRadiusMult,
    out float3 outPosition,
    out float3 outForward,
    out float3 outUp
) {
    float remainingDistance = max(targetDistance, 0);
    uint terminalFlags = 0u;
    float capLength = path.worldLength * SPS_HOLE_CAP_LENGTH;
    float previousCullAdvance = 0;
    outDeformationWeight = saturate(path.deformationWeight / SPS_MESH_FADE_WEIGHT_MAX);
    outRadiusMult = 1;
    outPosition = sps_path_worlds[0];
    outForward = sps_path_forwards[0];
    if (sps_is_zero(outForward)) outForward = float3(0, 0, 1);
    outForward = sps_normalize(outForward);
    outUp = sps_nearest_normal(outForward, path.plugUp);
    float3 startPoint = outPosition;
    float3 startForward = outForward;
    float3 currentUp = outUp;
    uint previousFlags = 0u;

    [fastopt]
    for (uint sampleIndex = 1; sampleIndex <= SPS_CHAIN_SOCKETS_MAX; sampleIndex++) {
        // Path construction already moved the entrance and removed skipped nodes.
        float3 endPoint = sps_path_worlds[sampleIndex];
        float3 endForward = sps_path_forwards[sampleIndex];
        if (sps_is_zero(endForward)) break;
        uint socketFlags = sps_path_flags[sampleIndex];
        float cullAdvance = sps_path_cull_advances[sampleIndex];

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
            p0,
            p1,
            p2,
            p3
        );

        bool zeroLength = sps_is_zero(p1 - p0) && sps_is_zero(p2 - p0) && sps_is_zero(p3 - p0);
        float distanceIntoSegment = remainingDistance;
        float nextRemainingDistance = remainingDistance;
        if (zeroLength) {
            // Coincident endpoints can leave no curve length or derivative to solve.
            outPosition = endPoint;
            outForward = endForward;
            outUp = sps_nearest_normal(endForward, currentUp);
        } else {
            sps_bezierSolve(
                p0,
                p1,
                p2,
                p3,
                remainingDistance,
                currentUp,
                nextRemainingDistance,
                outPosition,
                outForward,
                outUp
            );
        }
        remainingDistance = nextRemainingDistance;

        if (remainingDistance <= 0) {
            if (!zeroLength && sps_has_flag(previousFlags, SPS_SOCKET_FLAG_HOLE)) {
                if (sps_has_flag(previousFlags, SPS_SOCKET_FLAG_GUIDED)) {
                    outRadiusMult = 0;
                } else {
                    outRadiusMult = capLength > 0
                        ? 1 - sps_saturated_map(distanceIntoSegment, previousCullAdvance - capLength, previousCullAdvance)
                        : (distanceIntoSegment < previousCullAdvance ? 1 : 0);
                }
            }
            return;
        }

        // Use the known endpoint tangent after completing a segment. Subtracting
        // nearly coincident control points can lose its direction to float precision.
        outForward = endForward;
        outUp = sps_nearest_normal(outForward, outUp);
        currentUp = outUp;
        previousFlags = socketFlags;
        previousCullAdvance = cullAdvance;
        startPoint = endPoint;
        startForward = endForward;
    }

    if (sps_has_flag(terminalFlags, SPS_SOCKET_FLAG_HOLE)) {
        if (sps_has_flag(terminalFlags, SPS_SOCKET_FLAG_GUIDED)) {
            // Guided holes collapse immediately, including the final stop.
            outRadiusMult = 0;
            remainingDistance = 0;
        } else {
            // Ensure the configured overrun past the terminal hole, without adding it twice
            // when the entrance look-ahead already extends beyond that hole.
            float overrun = max(path.worldLength * SPS_HOLE_OVERRUN_MIN - path.terminalAdvance, 0) + previousCullAdvance;
            outRadiusMult = capLength > 0
                ? sps_saturated_map(max(remainingDistance - overrun, 0), capLength, 0)
                : (remainingDistance <= overrun ? 1 : 0);
            remainingDistance = min(remainingDistance, overrun);
        }
    }
    outPosition += outForward * remainingDistance;
}

#endif
