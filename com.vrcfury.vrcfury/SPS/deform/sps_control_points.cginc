#ifndef SPS_INC_CONTROL_POINTS
#define SPS_INC_CONTROL_POINTS

#include "sps_config.cginc"

#include "sps_types.cginc"
#include "sps_utils.cginc"

// Solve the first permissible join on an outgoing ray. delta is entrance minus
// plug root; both forward vectors must be unit directions along the curve.
// This defines geometric bend bounds, not the exact curvature of the Bezier.
// Returns false when their intersection is empty. advance is in world units.
inline bool sps_solve_entry_join(
    float3 delta,
    float3 startForward,
    float3 endForward,
    float worldLength,
    bool allowAdvance,
    out float advance
) {
    advance = 0;
    float h = SPS_HANDLE_LENGTH;
    if (!(worldLength > 0) || !(h > 0 && h < 1)) return false;
    // Normalize once so bounds and numerical tolerances scale with the avatar.
    delta /= worldLength;
    float room = max(SPS_BEND_ROOM, 1e-6);
    float c = clamp(dot(startForward, endForward), -1, 1);
    float q0 = dot(delta, endForward);
    float3 lateral = delta - endForward * q0;
    float rSq = dot(lateral, lateral);
    float r = sqrt(rSq);
    float b = dot(lateral, startForward);
    float k = h * c;
    // Wide U-turns may borrow sideways clearance. Forward-facing merges get no
    // such allowance, keeping their middle control edge from folding backward.
    float allowance = (1 - h) * rSq / (2 * room) * max(-c, 0);
    // Along the exit, D = lateral + q * endForward. The fold constraints are:
    // q + allowance >= k*|D| and c*q + b + allowance >= k*|D|.
    // The first always supplies a minimum q; its boundary is a quadratic root.
    float lower = (-allowance + k * sqrt(allowance * allowance + (1 - k * k) * rSq)) / (1 - k * k);
    float upper = allowAdvance ? q0 + max(SPS_ENTRY_ADVANCE_MAX, 0) : 1e30;
    if (c == 0) {
        if (b + allowance < 0) return false;
    } else {
        // Divide the second boundary by c: q + shift = h*sqrt(q*q + r*r).
        // A negative c reverses the inequality, making this an UPPER bound.
        float shift = (b + allowance) / c;
        float2 edge = float2(shift, sqrt((1 - h * h) * rSq));
        // Near perpendicular, shift can be finite but too large to square.
        float scale = max(abs(edge.x), edge.y);
        float edgeLength = scale > 0 ? scale * length(edge / scale) : 0;
        float boundary = (-shift + h * edgeLength) / (1 - h * h);
        if (c > 0) lower = max(lower, boundary);
        else upper = min(upper, boundary);
    }
    // Heading change and sideways merging each require a minimum chord length.
    float minimumDistanceSq = max(2 * room * room * (1 - c), sqrt(6 * h) * room * r);
    if (!allowAdvance) {
        // Later entrances are fixed points; only the initial entrance may move.
        return q0 >= lower - 1e-6 && q0 <= upper + 1e-6
            && dot(delta, delta) >= minimumDistanceSq - 1e-6;
    }
    lower = max(max(lower, q0), 0); // Start at the entrance or its closest point to the root.
    lower = max(lower, sqrt(max(minimumDistanceSq - rSq, 0)));
    if (lower > upper + 1e-6 || lower * lower + rSq > SPS_REACH_FADE_MAX * SPS_REACH_FADE_MAX + 1e-6) return false;
    advance = max(lower - q0, 0) * worldLength;
    return true;
}

#endif
