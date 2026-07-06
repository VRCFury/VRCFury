#ifndef SPS_INC_RESOLVER_SOCKET
#define SPS_INC_RESOLVER_SOCKET

#include "../common/sps_types.cginc"
#include "../common/sps_utils.cginc"
#include "sps_resolver_globals.cginc"
#include "sps_resolver_types.cginc"

bool sps_resolver_check_socket(
    CellData candidate,
    SocketData socketData,
    float3 previousWorld,
    float3 sourceForward,
    bool isGuideTarget,
    out float distanceSq,
    out float3 normal,
    out float3 up,
    out uint rejectionFlags
) {
    rejectionFlags = 0;
    float3 entryOffset = sps_resolver_socket_target_world(candidate, socketData.flags) - previousWorld;
    distanceSq = sps_length_sq(entryOffset);
    normal = sps_normalize(candidate.normal);
    up = sps_normalize(candidate.up);
    float3 rootDirection = sps_normalize(entryOffset);
    float3 normalizedSourceForward = sps_normalize(sourceForward);
    float entryForwardDot = dot(rootDirection, normalizedSourceForward);
    up = sps_nearest_normal(-normal, up);

    if (sps_has_flag(socketData.flags, SPS_SOCKET_FLAG_UNLOCK_ALL)) {
        normal = -rootDirection;
        up = sps_nearest_normal(-normal, up);
    } else if (sps_has_flag(socketData.flags, SPS_SOCKET_FLAG_UNLOCK_LOCAL_X)) {
        float3 right = sps_normalize(cross(up, -normal));
        normal = sps_nearest_normal(right, -rootDirection);
        up = sps_normalize(cross(right, normal));
    } else if (sps_has_flag(socketData.flags, SPS_SOCKET_FLAG_DOUBLE_SIDED) && dot(normal, rootDirection) > 0) {
        normal *= -1;
    }

    if (isGuideTarget) return true;
    float exitForwardDot = dot(-normal, normalizedSourceForward);
    if (exitForwardDot < SPS_EXIT_DOT_LIMIT) {
        SPS_DEBUG_SET(rejectionFlags, SPS_DEBUG_FLAG_EXIT_REJECT);
        return false;
    }

    if (!sps_has_flag(socketData.flags, SPS_SOCKET_FLAG_DOUBLE_SIDED)
        && dot(normal, rootDirection) > SPS_ENTRANCE_DOT_LIMIT) {
        SPS_DEBUG_SET(rejectionFlags, SPS_DEBUG_FLAG_ENTRANCE_REJECT);
        return false;
    }

    float worldLength = sps_resolver_length();
    float hiltDistance = worldLength * 0.5;
    bool behind = entryForwardDot <= 0
        && (!sps_has_flag(socketData.flags, SPS_SOCKET_FLAG_HOLE)
            || distanceSq > hiltDistance * hiltDistance);
    if (behind) {
        SPS_DEBUG_SET(rejectionFlags, SPS_DEBUG_FLAG_BEHIND_REJECT);
        return false;
    }

    float maxDistance = worldLength * 1.6;
    if (distanceSq >= maxDistance * maxDistance) {
        SPS_DEBUG_SET(rejectionFlags, SPS_DEBUG_FLAG_TOO_FAR_REJECT);
        return false;
    }

    return true;
}

#endif
