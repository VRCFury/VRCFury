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

    float worldLength = sps_resolver_length();
    bool isHilted = false;
    if (sps_has_flag(socketData.flags, SPS_SOCKET_FLAG_HOLE)) {
        float hiltDistance = worldLength * 0.5;
        float rootDistanceSq = sps_length_sq(candidate.world - previousWorld);
        isHilted = rootDistanceSq <= hiltDistance * hiltDistance;
    }

    if (!isHilted) {
        // The plug would have to change direction more than 108 degrees to enter the socket
        float3 normalizedSourceForward = sps_normalize(sourceForward);
        if (dot(-normal, normalizedSourceForward) < -0.309016994) {
            SPS_DEBUG_SET(rejectionFlags, SPS_DEBUG_FLAG_EXIT_REJECT);
            return false;
        }

        // Even if the plug aimed directly at the socket position, it would still have
        // to turn more than 144 degrees to enter the socket
        if (dot(normal, rootDirection) > 0.809016994) {
            SPS_DEBUG_SET(rejectionFlags, SPS_DEBUG_FLAG_ENTRANCE_REJECT);
            return false;
        }

        // The socket lies behind the plug's current forward direction,
        // so targeting it would require turning around.
        float entryForwardDot = dot(rootDirection, normalizedSourceForward);
        if (entryForwardDot <= 0) {
            SPS_DEBUG_SET(rejectionFlags, SPS_DEBUG_FLAG_BEHIND_REJECT);
            return false;
        }
    }

    float maxDistance = worldLength * 1.6;
    // Range check: the socket is outside the resolver's allowed reach.
    if (distanceSq >= maxDistance * maxDistance) {
        SPS_DEBUG_SET(rejectionFlags, SPS_DEBUG_FLAG_TOO_FAR_REJECT);
        return false;
    }

    return true;
}

#endif
