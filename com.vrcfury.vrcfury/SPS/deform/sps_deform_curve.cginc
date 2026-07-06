#ifndef SPS_INC_DEFORM_CURVE
#define SPS_INC_DEFORM_CURVE

#include "../resolver/sps_resolver_payload.cginc"
#include "../common/sps_cell_layout.cginc"
#include "../common/sps_types.cginc"
#include "../common/sps_utils.cginc"
#include "sps_deform_bezier.cginc"
#include "sps_deform_control_points.cginc"

inline void sps_deform_apply_portal_transfer(
    float3 entryPoint,
    float3 entryForwardInput,
    float3 entryUpInput,
    float3 exitPoint,
    float3 exitForwardInput,
    float3 exitUpInput,
    float3 incomingUp,
    out float3 sampleWorld,
    out float3 sampleForward,
    out float3 sampleUp
) {
    sampleWorld = exitPoint;
    float3 normalizedExitForward = sps_normalize(exitForwardInput);
    float3 normalizedEntryForward = sps_normalize(entryForwardInput);
    sampleForward = sps_is_zero(exitForwardInput) ? normalizedEntryForward : normalizedExitForward;

    float3 entryForward = sps_is_zero(entryForwardInput) ? sampleForward : normalizedEntryForward;
    float3 entryUpBase = sps_nearest_normal(entryForward, entryUpInput);
    float3 entryRightBase = sps_normalize(cross(entryUpBase, entryForward));
    float3 incomingUpProjected = sps_nearest_normal(entryForward, incomingUp);
    float entryRoll = atan2(dot(incomingUpProjected, entryRightBase), dot(incomingUpProjected, entryUpBase));

    float3 exitUpBase = sps_nearest_normal(sampleForward, exitUpInput);
    sampleUp = sps_nearest_normal(sampleForward, sps_rotate_around_axis(exitUpBase, sampleForward, -entryRoll));
}

inline void sps_deform_walk_chain(
    SpsCell resolverCell,
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
    outPosition = sps_read_resolver_chain_world(resolverCell, 0);
    outForward = sps_read_resolver_chain_forward(resolverCell, 0);
    if (sps_is_zero(outForward)) outForward = float3(0, 0, 1);
    outForward = sps_normalize(outForward);
    outUp = sps_nearest_normal(outForward, sps_read_resolver_chain_up(resolverCell, 0));
    float3 startPoint = outPosition;
    float3 startForward = outForward;
    float3 startUp = outUp;
    float3 startTangentOut = 0;
    float3 currentUp = outUp;
    uint previousFlags = 0u;

    [loop]
    for (uint sampleIndex = 1; sampleIndex <= SPS_CHAIN_MAX_SOCKETS; sampleIndex++) {
        float3 endPoint = sps_read_resolver_chain_world(resolverCell, sampleIndex);
        float3 endForward = sps_read_resolver_chain_forward(resolverCell, sampleIndex);
        if (sps_is_zero(endForward)) break;
        float3 endUp = sps_read_resolver_chain_up(resolverCell, sampleIndex);
        uint socketFlags = sps_read_resolver_chain_flags(resolverCell, sampleIndex);
        bool isGuidedSegment = sps_read_resolver_is_guide_target(resolverCell, sampleIndex);
        float3 endTangentIn = sps_read_resolver_chain_tangent_in(resolverCell, sampleIndex);
        float3 endTangentOut = sps_read_resolver_chain_tangent_out(resolverCell, sampleIndex);
        float segmentPulloutLerp;

        terminalFlags = socketFlags;
        // bool isPortal = sps_has_flag(previousFlags, SPS_SOCKET_FLAG_PORTAL);
        // if (isPortal) {
        //     sps_deform_apply_portal_transfer(
        //         startPoint, startForward, startUp,
        //         endPoint, endForward, endUp,
        //         currentUp,
        //         outPosition, outForward, outUp
        //     );
        //     currentUp = outUp;
        //     previousFlags = socketFlags;
        //     startPoint = endPoint;
        //     startForward = endForward;
        //     startUp = endUp;
        //     continue;
        // }

        float3 p0;
        float3 p1;
        float3 p2;
        float3 p3;
        sps_deform_segment_control_points(
            startPoint,
            startForward,
            startTangentOut,
            endPoint,
            endForward,
            endTangentIn,
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

        if (!sps_is_zero(endTangentOut)) {
            outForward = sps_normalize(endTangentOut);
        }

        currentUp = sampleUp;
        previousFlags = socketFlags;
        startPoint = endPoint;
        startForward = endForward;
        startUp = endUp;
        startTangentOut = endTangentOut;
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
    }
    outPosition += outForward * remainingDistance;
}

#endif
