#ifndef SPS_INC_DEFORM_CONTROL_POINTS
#define SPS_INC_DEFORM_CONTROL_POINTS

#include "../common/sps_types.cginc"
#include "../common/sps_utils.cginc"

inline void sps_deform_segment_control_points(
    float3 startPoint,
    float3 startForwardInput,
    float3 startTangentOut,
    float3 endPoint,
    float3 endForwardInput,
    float3 endTangentIn,
    bool nextLink,
    float worldLength,
    float remainingLength,
    out float3 p0,
    out float3 p1,
    out float3 p2,
    out float3 p3,
    out float applyLerp
) {
    p0 = startPoint;
    p3 = endPoint;

    float3 startForward = startForwardInput;
    if (length(startForward) <= 0) startForward = sps_normalize(endPoint - startPoint);
    if (length(startForward) <= 0) startForward = float3(0, 0, 1);

    float3 endForward = endForwardInput;
    if (length(endForward) <= 0) endForward = sps_normalize(endPoint - startPoint);
    if (length(endForward) <= 0) endForward = float3(0, 0, 1);

    if (nextLink) applyLerp = 1;
    else {
        float fadeStart = remainingLength + worldLength * 0.2;
        float fadeEnd = remainingLength + worldLength * 0.6;
        float distance = length(endPoint - startPoint);
        applyLerp = 1 - sps_saturated_map(distance, fadeStart, fadeEnd);
    }

    float bezierLerp = sps_saturated_map(applyLerp, 0, 1);
    float handleDistance = length(endPoint - startPoint) * 0.5;
    float handleDistanceWithPullout = sps_map(bezierLerp, 0, 1, worldLength * 5, handleDistance);
    p1 = any(startTangentOut != 0)
        ? startTangentOut
        : p0 + startForward * handleDistanceWithPullout;
    p2 = any(endTangentIn != 0)
        ? endTangentIn
        : p3 - endForward * handleDistance;
}

#endif
