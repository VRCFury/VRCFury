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
    bool isGuidedSegment,
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
    float3 segmentOffset = endPoint - startPoint;
    float segmentDistance = length(segmentOffset);

    float3 startForward = startForwardInput;
    if (sps_is_zero(startForward)) startForward = sps_normalize(segmentOffset);
    if (sps_is_zero(startForward)) startForward = float3(0, 0, 1);

    float3 endForward = endForwardInput;
    if (sps_is_zero(endForward)) endForward = sps_normalize(segmentOffset);
    if (sps_is_zero(endForward)) endForward = float3(0, 0, 1);

    if (isGuidedSegment) applyLerp = 1;
    else {
        float fadeStart = remainingLength + worldLength * 0.2;
        float fadeEnd = remainingLength + worldLength * 0.6;
        applyLerp = 1 - sps_saturated_map(segmentDistance, fadeStart, fadeEnd);
    }

    float bezierLerp = sps_saturated_map(applyLerp, 0, 1);
    float handleDistance = segmentDistance * 0.5;
    float handleDistanceWithPullout = sps_map(bezierLerp, 0, 1, worldLength * 5, handleDistance);
    p1 = !sps_is_zero(startTangentOut)
        ? startTangentOut
        : p0 + startForward * handleDistanceWithPullout;
    p2 = !sps_is_zero(endTangentIn)
        ? endTangentIn
        : p3 - endForward * handleDistance;
}

#endif
