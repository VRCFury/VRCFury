#ifndef SPS_INC_CURVE
#define SPS_INC_CURVE

#include "sps_config.cginc"

#include "sps_path.cginc"
#include "sps_types.cginc"
#include "sps_utils.cginc"
#include "sps_bezier.cginc"
#include "sps_control_points.cginc"
#include "sps_globals.cginc"

// Approximate the outgoing path with straight lines between stops.
// Returns the next socket index (2 if unchanged), or SPS_CHAIN_SOCKETS_MAX + 1 on the final exit.
// terminalAdvance reports how far the new entrance extends beyond the final stop.
inline uint sps_deform_advance_entry(
    float distance,
    inout float3 position,
    inout float3 forward,
    inout uint flags,
    inout float weight,
    out float terminalAdvance
) {
    terminalAdvance = 0;
    if (distance <= 0) return 2;
    [fastopt]
    for (uint next = 2; next <= SPS_CHAIN_SOCKETS_MAX; next++) {
        float3 end = sps_path_worlds[next];
        float3 endForward = sps_path_forwards[next];
        if (sps_is_zero(endForward)) break;
        float3 delta = end - position;
        float span = length(delta);
        if (distance < span) {
            // Join between these stops; use their chord if their directions cancel.
            float t = distance / span;
            // Introduce the next stop's fade gradually as the entrance approaches it.
            weight = lerp(weight, min(weight, sps_path_weights[next]), t);
            position += delta * t;
            float3 blendedForward = lerp(forward, endForward, t);
            forward = sps_normalize(sps_is_zero(blendedForward) ? delta : blendedForward);
            return next;
        }
        // This stop is already behind the new entrance.
        weight = min(weight, sps_path_weights[next]);
        distance -= span;
        position = end;
        forward = endForward;
        flags = sps_path_flags[next];
    }
    // Beyond the last stop, follow its straight-line exit.
    terminalAdvance = distance;
    position += forward * distance;
    return SPS_CHAIN_SOCKETS_MAX + 1;
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
    float entryWeight = path.deformationWeight;
    float3 offset = sps_path_worlds[0] - entryPoint;
    // Signed distance past the socket plane, plus sideways distance from its axis.
    float axialDepth = dot(offset, entryForward);
    float lateralDistance = length(offset - entryForward * axialDepth);
    // Aim farther along the path so off-center plugs need less of a return bend.
    float advance = max(
        axialDepth + lateralDistance * SPS_LATERAL_ADVANCE_MULTIPLIER + path.worldLength * SPS_ENTRY_ADVANCE,
        0
    );
    float terminalAdvance;
    uint nextSocket = sps_deform_advance_entry(
        advance,
        entryPoint,
        entryForward,
        entryFlags,
        entryWeight,
        terminalAdvance
    );
    float remainingDistance = max(targetDistance, 0);
    uint terminalFlags = 0u;
    // Straighten the curve before blending back to the original mesh. Blending
    // bent vertex positions directly to straight ones cuts across the bend.
    float curveWeight = sps_saturated_map(entryWeight, SPS_MESH_FADE_WEIGHT_MAX, 1);
    float capLength = path.worldLength * SPS_HOLE_CAP_LENGTH;
    float previousCurveWeight = curveWeight;
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
    float3 originalStartPoint = outPosition;
    float3 originalStartForward = outForward;
    uint previousFlags = 0u;

    [fastopt]
    for (uint sampleIndex = 1; sampleIndex <= SPS_CHAIN_SOCKETS_MAX; sampleIndex++) {
        // Keep the root-to-entrance Bezier, using the advanced entrance as its target.
        float3 endPoint = sampleIndex == 1 ? entryPoint : sps_path_worlds[sampleIndex];
        float3 endForward = sampleIndex == 1 ? entryForward : sps_path_forwards[sampleIndex];
        if (sps_is_zero(endForward)) break;
        uint socketFlags = sampleIndex == 1 ? entryFlags : sps_path_flags[sampleIndex];
        float weight = sampleIndex == 1 ? entryWeight : sps_path_weights[sampleIndex];
        curveWeight = min(curveWeight, sps_saturated_map(weight, SPS_MESH_FADE_WEIGHT_MAX, 1));

        // Straighten each segment from the previous segment's adjusted endpoint.
        float3 segmentOffset = endPoint - originalStartPoint;
        originalStartPoint = endPoint;
        float3 forwardChange = endForward - originalStartForward;
        originalStartForward = endForward;
        endPoint = startPoint + lerp(startForward * length(segmentOffset), segmentOffset, curveWeight);
        // Fade only the change in direction. A vanishing residual segment must not
        // bend the already-faded entrance toward the same stop a second time.
        endForward = sps_normalize(startForward + forwardChange * curveWeight);

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
                float cullAdvance = (1 - previousCurveWeight) * (path.worldLength + capLength);
                outRadiusMult = capLength > 0
                    ? 1 - sps_saturated_map(distanceIntoSegment, cullAdvance - capLength, cullAdvance)
                    : (distanceIntoSegment < cullAdvance ? 1 : 0);
            }
            return;
        }

        // Use the known endpoint tangent after completing a segment. Subtracting
        // nearly coincident control points can lose its direction to float precision.
        outForward = endForward;
        outUp = sps_nearest_normal(outForward, outUp);
        currentUp = outUp;
        previousFlags = socketFlags;
        previousCurveWeight = curveWeight;
        startPoint = endPoint;
        startForward = endForward;
        // Resume after any stops consumed by the entrance look-ahead.
        if (sampleIndex == 1) sampleIndex = nextSocket - 1;
    }

    if (sps_has_flag(terminalFlags, SPS_SOCKET_FLAG_HOLE)) {
        // Ensure the configured overrun past the terminal hole, without adding it twice
        // when the entrance look-ahead already extends beyond that hole.
        float cullAdvance = (1 - curveWeight) * (path.worldLength + capLength);
        float overrun = max(path.worldLength * SPS_HOLE_OVERRUN_MIN - terminalAdvance, 0) + cullAdvance;
        outRadiusMult = capLength > 0
            ? sps_saturated_map(max(remainingDistance - overrun, 0), capLength, 0)
            : (remainingDistance <= overrun ? 1 : 0);
        remainingDistance = min(remainingDistance, overrun);
    }
    outPosition += outForward * remainingDistance;
}

#endif
