#ifndef SPS_INC_CONTROL_POINTS
#define SPS_INC_CONTROL_POINTS

#include "sps_config.cginc"

#include "sps_types.cginc"
#include "sps_utils.cginc"

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

#endif
