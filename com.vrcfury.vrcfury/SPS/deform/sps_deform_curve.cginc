#ifndef SPS_INC_DEFORM_CURVE
#define SPS_INC_DEFORM_CURVE

#include "sps_deform_path.cginc"
#include "../common/sps_types.cginc"
#include "../common/sps_utils.cginc"
#include "sps_deform_bezier.cginc"
#include "sps_deform_control_points.cginc"
#include "sps_deform_globals.cginc"

// Approximate the outgoing path with straight lines between stops.
// Returns the next socket index (2 if unchanged), or SPS_CHAIN_MAX_SOCKETS + 1 on the final exit.
// terminalAdvance reports how far the new entrance extends beyond the final stop.
inline uint sps_deform_advance_entry(
    float distance,
    inout float3 position,
    inout float3 forward,
    inout uint flags,
    out float terminalAdvance
) {
    terminalAdvance = 0;
    if (distance <= 0) return 2;
    [fastopt]
    for (uint next = 2; next <= SPS_CHAIN_MAX_SOCKETS; next++) {
        float3 end = sps_path_worlds[next];
        float3 endForward = sps_path_forwards[next];
        if (sps_is_zero(endForward)) break;
        float3 delta = end - position;
        float span = length(delta);
        if (distance < span) {
            // Join between these stops; use their chord if their directions cancel.
            float t = distance / span;
            position += delta * t;
            float3 blendedForward = lerp(forward, endForward, t);
            forward = sps_normalize(sps_is_zero(blendedForward) ? delta : blendedForward);
            return next;
        }
        // This stop is already behind the new entrance.
        distance -= span;
        position = end;
        forward = endForward;
        flags = sps_path_flags[next];
    }
    // Beyond the last stop, follow its straight-line exit.
    terminalAdvance = distance;
    position += forward * distance;
    return SPS_CHAIN_MAX_SOCKETS + 1;
}

inline void sps_deform_walk_chain(
    SpsDeformPathInfo path,
    float targetDistance,
    out float outDeformationWeight,
    out float outRadiusMult,
    out float3 outPosition,
    out float3 outForward,
    out float3 outUp
) {
    float3 entryPoint = sps_path_worlds[1];
    float3 entryForward = sps_path_forwards[1];
    uint entryFlags = sps_path_flags[1];
    float3 offset = sps_path_worlds[0] - entryPoint;
    // Signed distance past the socket plane, plus sideways distance from its axis.
    float axialDepth = dot(offset, entryForward);
    float lateralDistance = length(offset - entryForward * axialDepth);
    // Aim farther along the path so off-center plugs need less of a return bend.
    float advance = max(axialDepth + lateralDistance + path.worldLength * 0.1, 0);
    float terminalAdvance;
    uint nextSocket = sps_deform_advance_entry(
        advance,
        entryPoint,
        entryForward,
        entryFlags,
        terminalAdvance
    );
    float remainingDistance = max(targetDistance, 0);
    uint terminalFlags = 0u;
    // Straighten the curve before blending back to the original mesh. Blending
    // bent vertex positions directly to straight ones cuts across the bend.
    float curveWeight = sps_saturated_map(path.deformationWeight, 0.2, 1);
    outDeformationWeight = saturate(path.deformationWeight * 5);
    outRadiusMult = 1;
    outPosition = sps_path_worlds[0];
    outForward = sps_path_forwards[0];
    if (sps_is_zero(outForward)) outForward = float3(0, 0, 1);
    outForward = sps_normalize(outForward);
    float3 plugForward = outForward;
    outUp = sps_nearest_normal(outForward, path.plugUp);
    float3 startPoint = outPosition;
    float3 startForward = outForward;
    float3 currentUp = outUp;
    float3 originalStartPoint = outPosition;
    uint previousFlags = 0u;

    [fastopt]
    for (uint sampleIndex = 1; sampleIndex <= SPS_CHAIN_MAX_SOCKETS; sampleIndex++) {
        // Keep the root-to-entrance Bezier, using the advanced entrance as its target.
        float3 endPoint = sampleIndex == 1 ? entryPoint : sps_path_worlds[sampleIndex];
        float3 endForward = sampleIndex == 1 ? entryForward : sps_path_forwards[sampleIndex];
        if (sps_is_zero(endForward)) break;
        uint socketFlags = sampleIndex == 1 ? entryFlags : sps_path_flags[sampleIndex];

        // Straighten each segment from the previous segment's adjusted endpoint.
        float3 segmentOffset = endPoint - originalStartPoint;
        originalStartPoint = endPoint;
        endPoint = startPoint + lerp(plugForward * length(segmentOffset), segmentOffset, curveWeight);
        endForward = sps_normalize(lerp(plugForward, endForward, curveWeight));

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
                outRadiusMult = 1 - curveWeight;
            }
            return;
        }

        currentUp = outUp;
        previousFlags = socketFlags;
        startPoint = endPoint;
        startForward = endForward;
        // Resume after any stops consumed by the entrance look-ahead.
        if (sampleIndex == 1) sampleIndex = nextSocket - 1;
    }

    if (sps_has_flag(terminalFlags, SPS_SOCKET_FLAG_HOLE)) {
        // Ensure 10% overrun past the terminal hole, without adding it twice when
        // the entrance look-ahead already extends beyond that hole. Keep the 5% cap.
        float overrun = max(path.worldLength * 0.1 - terminalAdvance, 0);
        float capRadius = sps_saturated_map(
            max(remainingDistance - overrun, 0),
            path.worldLength * 0.05,
            0
        );
        outRadiusMult = lerp(1, capRadius, curveWeight);
        remainingDistance = lerp(remainingDistance, min(remainingDistance, overrun), curveWeight);
    }
    outPosition += outForward * remainingDistance;
}

#endif
