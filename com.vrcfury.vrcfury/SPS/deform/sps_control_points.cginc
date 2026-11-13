#ifndef SPS_INC_CONTROL_POINTS
#define SPS_INC_CONTROL_POINTS

#include "sps_config.cginc"

#include "sps_types.cginc"
#include "sps_utils.cginc"

inline float sps_entry_advance_distance(
    float3 offset,
    float3 plugForward,
    float3 socketForward,
    float worldLength
) {
    // Measure passing the entrance along the plug, and sideways offset along
    // the socket. A backward exit must not make a socket ahead appear hilted.
    float axialDepth = dot(offset, plugForward);
    float lateralDistance = length(offset - socketForward * dot(offset, socketForward));
    return max(
        axialDepth + lateralDistance * SPS_LATERAL_ADVANCE_MULTIPLIER + worldLength * SPS_ENTRY_ADVANCE,
        0
    );
}

inline void sps_deform_segment_control_points(
    float3 startPoint,
    float3 startForwardInput,
    float3 endPoint,
    float3 endForwardInput,
    out float3 p0,
    out float3 p1,
    out float3 p2,
    out float3 p3
) {
    p0 = startPoint;
    p3 = endPoint;
    float3 segmentOffset = endPoint - startPoint;
    float segmentDistance = length(segmentOffset);

    float3 startForward = startForwardInput;
    if (sps_is_zero(startForward)) startForward = sps_normalize(segmentOffset);
    if (sps_is_zero(startForward)) startForward = float3(0, 0, 1);

    float3 endForward = endForwardInput;
    if (sps_is_zero(endForward)) endForward = sps_normalize(segmentOffset);
    if (sps_is_zero(endForward)) endForward = float3(0, 0, 1);

    float handleDistance = segmentDistance * SPS_HANDLE_LENGTH;
    p1 = p0 + startForward * handleDistance;
    p2 = p3 - endForward * handleDistance;
}

// Cheap estimate of how sharply this segment must bend, not an exact curvature
// or turning radius. Two-way sockets choose the face with the lower score;
// one-way sockets compare it against the squared configured sharpness limit.
//
// delta goes from the segment's start to its target, including lookahead for
// the initial entrance. The forward vectors must be unit directions along the
// curve at its two ends. worldLength is the full plug length, not remaining reach.
// Returns a squared, dimensionless score without evaluating or solving a curve.
inline float sps_bend_sharpness_sq(
    float3 delta,
    float3 startForward,
    float3 endForward,
    float worldLength
) {
    float distanceSq = dot(delta, delta);
    // Coincident endpoints leave no room to change direction. Identical
    // directions need no bend; otherwise use a large finite rejection score.
    if (distanceSq == 0) return sps_is_zero(startForward - endForward) ? 0 : 1e30;

    // The chord is the straight line to the target, not the actual curved path.
    // These dot products are cosines: 1 follows the chord, 0 is perpendicular,
    // and -1 points directly against it.
    float inverseDistance = rsqrt(distanceSq);
    float3 chord = delta * inverseDistance;
    float startAlignment = clamp(dot(chord, startForward), -1, 1);
    float endAlignment = clamp(dot(chord, endForward), -1, 1);
    float worstAlignment = min(startAlignment, endAlignment);
    // (1 - cos(angle)) / (1 + cos(angle)) = tan(angle/2)^2.
    // Use the harder of the two endpoint turns. The penalty is zero when
    // aligned and tends to infinity as a turn approaches a full reversal.
    float turn = worstAlignment <= -1 ? 1e30 : (1 - worstAlignment) / (1 + worstAlignment);

    // h is each tangent handle's length as a fraction of the chord length,
    // matching the control-point construction used for the curve.
    float h = SPS_HANDLE_LENGTH;
    // Estimate overall turning effort as well as the worst individual turn.
    // The first two alignment losses penalize deviation from the chord; the
    // final term discounts a consistent change between the endpoint directions.
    // This helps distinguish a broad U-turn from two extra opposing bends.
    // Straight travel has zero penalty regardless of target distance.
    float opposing = max(
        3 * h * (2 - startAlignment - endAlignment - h * (1 - dot(startForward, endForward))),
        0
    );
    // The overall angles can still miss a very tight fold just before the exit.
    // "middle" is the vector between the inner control points (p2 - p1),
    // divided by chord length. It is control-polygon geometry, not a sampled
    // point or tangent on the curve. Construct relative to zero to avoid
    // subtracting large world positions when the segment is short.
    float3 p0;
    float3 p1;
    float3 p2;
    float3 p3;
    sps_deform_segment_control_points(
        float3(0, 0, 0),
        startForward,
        delta,
        endForward,
        p0,
        p1,
        p2,
        p3
    );
    float3 middle = (p2 - p1) * inverseDistance;
    // Measure its strongest backward component against either endpoint tangent.
    // A sufficiently strong opposition can make forward motion nearly stop,
    // then reverse locally, even when the overall bend score looks acceptable.
    float opposition = max(-dot(middle, startForward), -dot(middle, endForward));
    float fold = 0;
    // Only test for that fold when the backward component exceeds an endpoint
    // handle's length (h in these normalized units). A tiny inner separation
    // can point backward even on a nearly straight curve and should be ignored.
    if (opposition > h) {
        // Squared component perpendicular to the opposed tangent. Clamp away
        // small negative values caused by subtracting nearly equal floats.
        float sidewaysSq = max(dot(middle, middle) - opposition * opposition, 0);
        // Less sideways room means a tighter reversal. Sharpness grows with
        // (backward / sideways)^2 here; the returned score squares that again.
        // The finite fallback handles a reversal with no sideways room.
        float ratio = sidewaysSq > 0 ? opposition * opposition / sidewaysSq : 1e15;
        fold = ratio * ratio;
    }
    // Use the strongest warning, rather than adding penalties for the same bend.
    // The directional terms above do not know how much room the bend has.
    // Multiplying by (plug length / target distance)^2 makes the same directional
    // geometry twice as sharp when squeezed into half the distance (4x score).
    // Both lengths scale together with the avatar, so avatar scale cancels out.
    return max(max(turn, opposing), fold) * worldLength * worldLength / distanceSq;
}

#endif
