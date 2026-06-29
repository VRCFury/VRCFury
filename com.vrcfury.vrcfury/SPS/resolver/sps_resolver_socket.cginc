#ifndef SPS_INC_RESOLVER_SOCKET
#define SPS_INC_RESOLVER_SOCKET

#include "../common/sps_types.cginc"
#include "../common/sps_utils.cginc"
#include "sps_resolver_globals.cginc"
#include "sps_resolver_types.cginc"

float sps_prepare_and_evaluate_socket(
    float3 entryOffset,
    float3 sourceForward,
    float distanceSq,
    uint flags,
    inout float3 normal,
    out uint rejectionFlags
) {
    rejectionFlags = 0;
    float3 rootDirection = sps_normalize(entryOffset);
    float entryForwardDot = dot(rootDirection, sps_normalize(sourceForward));
    if (sps_has_flag(flags, SPS_SOCKET_FLAG_DOUBLE_SIDED) && dot(sps_normalize(normal), rootDirection) > 0) {
        normal *= -1;
    }
    if (entryForwardDot < SPS_EXIT_DOT_LIMIT) {
        SPS_DEBUG_SET(rejectionFlags, SPS_DEBUG_FLAG_EXIT_REJECT);
        return 0;
    }
    if (!sps_has_flag(flags, SPS_SOCKET_FLAG_DOUBLE_SIDED)
        && dot(sps_normalize(normal), rootDirection) > SPS_ENTRANCE_DOT_LIMIT) {
        SPS_DEBUG_SET(rejectionFlags, SPS_DEBUG_FLAG_ENTRANCE_REJECT);
        return 0;
    }
    float worldLength = sps_resolver_length();
    float hiltDistance = worldLength * 0.5;
    bool behind = entryForwardDot <= 0
        && (!sps_has_flag(flags, SPS_SOCKET_FLAG_HOLE)
            || distanceSq > hiltDistance * hiltDistance);
    if (behind) {
        SPS_DEBUG_SET(rejectionFlags, SPS_DEBUG_FLAG_BEHIND_REJECT);
        return 0;
    }
    float fadeStart = worldLength * 1.2;
    float fadeEnd = worldLength * 1.6;
    if (distanceSq >= fadeEnd * fadeEnd) {
        SPS_DEBUG_SET(rejectionFlags, SPS_DEBUG_FLAG_TOO_FAR_REJECT);
        return 0;
    }
    if (distanceSq > fadeStart * fadeStart) {
        float tooFar = sps_saturated_map(sqrt(distanceSq), fadeStart, fadeEnd);
        return 1 - tooFar;
    }
    return 1;
}

#endif
