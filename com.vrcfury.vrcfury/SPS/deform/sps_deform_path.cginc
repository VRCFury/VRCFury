#ifndef SPS_INC_DEFORM_PATH
#define SPS_INC_DEFORM_PATH

#include "sps_deform_config.cginc"

#include "../common/sps_types.cginc"
#include "../common/sps_utils.cginc"
#include "sps_deform_contacts.cginc"
#include "sps_deform_lights.cginc"
#include "Packages/com.vrchat.base/ShaderLibrary/VRCContacts.cginc"

// Per-invocation scratch arrays avoid copying the path between helpers.
static float3 sps_path_worlds[SPS_CHAIN_MAX_SOCKETS + 1];
static float3 sps_path_forwards[SPS_CHAIN_MAX_SOCKETS + 1];
static uint sps_path_flags[SPS_CHAIN_MAX_SOCKETS + 1];
static float sps_path_weights[SPS_CHAIN_MAX_SOCKETS + 1];

struct SpsDeformNode {
    float3 world;
    float3 forward;
    float3 up;
    uint flags;
    float deformationWeight;
};

struct SpsDeformPathInfo {
    float3 plugUp;
    float3 firstSocketUp;
    float worldLength;
    float bakeScale;
    float deformationWeight;
};

inline float sps_deformation_weight(
    float3 delta,
    float3 currentForward,
    float3 currentUp,
    float3 socketForward,
    float worldLength,
    float worldRadius,
    float worldBaseActivationRadiusOverride,
    bool hole
) {
    float forwardDistance = dot(delta, currentForward);
    float distance = length(delta);
    // Fade excessive bends using the configured dot-product thresholds.
    float angleWeight = sps_saturated_map(
        dot(currentForward, socketForward),
        cos(radians(SPS_DEFORM_BEND_FADE_END_DEGREES)),
        cos(radians(SPS_DEFORM_BEND_FADE_START_DEGREES))
    );
    float baseActivationRadius = worldBaseActivationRadiusOverride > 0
        ? worldBaseActivationRadiusOverride : worldRadius * SPS_DEFORM_BASE_RADIUS_MULTIPLIER;
    float2 sideways = float2(dot(delta, cross(currentUp, currentForward)), dot(delta, currentUp));
    float innerDistance;
    float outerDistance;
    if (forwardDistance < 0) {
        // Behind the base: a cylinder with the same radius and one radius of depth.
        outerDistance = max(length(sideways), -forwardDistance) / baseActivationRadius;
        innerDistance = outerDistance / SPS_DEFORM_SIDE_FADE_START_FRACTION;
    } else {
        float2 sidewaysReach = forwardDistance * tan(radians(float2(
            SPS_DEFORM_CONE_HORIZONTAL_HALF_ANGLE_DEGREES,
            SPS_DEFORM_CONE_VERTICAL_HALF_ANGLE_DEGREES
        )));
        // The forward cone has elliptical inner/outer boundaries. Interpolate
        // along their radius so the fade has the configured width in each direction.
        innerDistance = length(sideways / (sidewaysReach + baseActivationRadius * SPS_DEFORM_SIDE_FADE_START_FRACTION));
        outerDistance = length(sideways / (sidewaysReach + baseActivationRadius));
    }
    // Perpendicular distance to the current stop's forward axis. The target plane's
    // orientation does not affect this limit, so parallel sockets can still be reached.
    float lateralOffset = length(sideways);
    float lateralRatio = lateralOffset / (baseActivationRadius * SPS_DEFORM_MAX_LATERAL_OFFSET_MULTIPLIER);
    outerDistance = max(outerDistance, lateralRatio);
    innerDistance = max(innerDistance, lateralRatio / SPS_DEFORM_SIDE_FADE_START_FRACTION);
    float boundaryWeight = outerDistance >= 1 ? 0 : innerDistance <= 1 ? 1
        : (1 - outerDistance) / (1 - outerDistance / innerDistance);
    float farEdgeWeight = 1 - sps_saturated_map(
        distance,
        worldLength * SPS_DEFORM_REACH_FADE_START,
        worldLength * SPS_DEFORM_MAX_REACH
    );
    float captureWeight = angleWeight * boundaryWeight;
    // A hole snaps at its side/back limits; only its far edge fades.
    if (hole) captureWeight = captureWeight > 0 ? 1 : 0;
    return farEdgeWeight * captureWeight;
}

bool sps_prepare_socket(
    SpsSocketCandidate socket,
    SpsDeformNode previous,
    float worldLength,
    float worldRadius,
    float worldBaseActivationRadiusOverride,
    bool guided,
    out SpsDeformNode node,
    out float distanceSq
) {
    node = (SpsDeformNode)0;
    node.world = socket.world;
    // Offset in the socket's own up direction before distance/approach checks.
    // Orientation unlocking below must not change which side of the socket we target.
    if (sps_has_flag(socket.control, SPS_SOCKET_FLAG_RADIUS_OFFSET)) {
        node.world += socket.up * worldRadius * max(_SPS_RadiusOffsetMultiplier, 0);
    }
    float3 delta = node.world - previous.world;
    distanceSq = dot(delta, delta);
    float3 direction = sps_normalize(delta);
    float3 normal = socket.normal;
    float3 up = sps_nearest_normal(normal, socket.up);
    if (sps_has_flag(socket.control, SPS_SOCKET_FLAG_UNLOCK_ALL)) {
        normal = -direction;
        up = sps_nearest_normal(normal, up);
    } else if (sps_has_flag(socket.control, SPS_SOCKET_FLAG_UNLOCK_LOCAL_X)) {
        float3 right = sps_normalize(cross(up, -normal));
        normal = sps_nearest_normal(right, -direction);
        up = sps_normalize(cross(right, normal));
    } else if (sps_has_flag(socket.control, SPS_SOCKET_FLAG_DOUBLE_SIDED) && dot(normal, previous.forward) > 0) {
        normal = -normal;
    }
    node.forward = -normal;
    node.up = up;
    node.flags = socket.control & 0xfc00u; // Flags in bits 10-15, without path key or validity.
    node.deformationWeight = 1;
    if (!guided) node.deformationWeight = sps_deformation_weight(
        delta,
        previous.forward,
        previous.up,
        node.forward,
        worldLength,
        worldRadius,
        worldBaseActivationRadiusOverride,
        sps_has_flag(node.flags, SPS_SOCKET_FLAG_HOLE)
    );
    return node.deformationWeight > 0;
}

// Node zero is the plug frame; the remaining nodes are socket entrances/stops.
// fastopt keeps FXC from spending excessive compile time optimizing the nested loops.
bool sps_try_get_path(out SpsDeformPathInfo path) {
    // Unity's shader upgrader mistakes built-in array casts for legacy array declarations.
    [unroll]
    for (uint i = 0; i <= SPS_CHAIN_MAX_SOCKETS; i++) {
        sps_path_worlds[i] = 0;
        sps_path_forwards[i] = 0;
        sps_path_flags[i] = 0;
        sps_path_weights[i] = 0;
    }
    path = (SpsDeformPathInfo)0;
    float4x4 frame = sps_contact_frame();
    float frameScale = length(frame._m02_m12_m22);
    if (!(frameScale > 0)) return false;
    sps_path_worlds[0] = frame._m03_m13_m23;
    sps_path_forwards[0] = sps_normalize(frame._m02_m12_m22);
    path.plugUp = sps_nearest_normal(sps_path_forwards[0], frame._m01_m11_m21);
    path.bakeScale = frameScale;
    path.worldLength = _SPS_BakedLength * path.bakeScale;
    float worldRadius = _SPS_BakedRadius * path.bakeScale;
    float worldBaseActivationRadiusOverride = _SPS_BaseActivationRadiusOverride;
    if (!(path.worldLength > 0)) return false;

    SpsSocketCandidate sockets[SPS_CONTACT_COUNT + 4];
    [fastopt]
    for (uint i = 0; i < SPS_CONTACT_COUNT; i++) {
        sockets[i] = sps_read_contact(i);
    }
    [fastopt]
    for (uint lightIndex = 0; lightIndex < 4; lightIndex++) {
        sockets[SPS_CONTACT_COUNT + lightIndex] = sps_read_light(lightIndex, sps_path_worlds[0], path.plugUp);
    }

    uint used = 0;
    SpsSocketCandidate previous = (SpsSocketCandidate)0;
    SpsDeformNode previousNode = (SpsDeformNode)0;
    previousNode.world = sps_path_worlds[0];
    previousNode.forward = sps_path_forwards[0];
    previousNode.up = path.plugUp;
    float remainingLength = path.worldLength * SPS_DEFORM_MAX_REACH;
    [fastopt]
    for (uint step = 1; step <= SPS_CHAIN_MAX_SOCKETS; step++) {
        int chosen = -1;
        SpsDeformNode chosenNode = (SpsDeformNode)0;
        float nearest = 1e30;
        // Look for path stops first
        if (sps_socket_key(previous) != 0) {
            [fastopt]
            for (int candidate = 2 * VRC_CONTACT_MAX_TARGETS; candidate < SPS_CONTACT_COUNT; candidate++) {
                SpsSocketCandidate socket = sockets[candidate];
                if (!sps_socket_valid(socket) || (used & (1u << candidate)) != 0) continue;
                // Adding 128 advances the path index without changing the low 7-bit group.
                if (socket.owner != previous.owner || sps_socket_key(socket) != sps_socket_key(previous) + 128u) continue;
                SpsDeformNode node;
                float distanceSq;
                if (!sps_prepare_socket(
                    socket,
                    previousNode,
                    remainingLength / SPS_DEFORM_MAX_REACH,
                    worldRadius,
                    worldBaseActivationRadiusOverride,
                    true,
                    node,
                    distanceSq
                ) || distanceSq >= nearest) continue;
                chosen = candidate;
                chosenNode = node;
                nearest = distanceSq;
            }
        }
        // If no path stop, look for a new socket entrance (unless it just entered a hole)
        if (chosen < 0 && (step == 1 || !sps_has_flag(previousNode.flags, SPS_SOCKET_FLAG_HOLE))) {
            nearest = remainingLength * remainingLength;
            [fastopt]
            for (int slot = 0; slot < 2 * VRC_CONTACT_MAX_TARGETS + 4; slot++) {
                int candidate = slot < 2 * VRC_CONTACT_MAX_TARGETS ? slot : slot + VRC_CONTACT_MAX_TARGETS;
                SpsSocketCandidate socket = sockets[candidate];
                if (!sps_socket_valid(socket) || sps_socket_key(socket) >= 128u || (used & (1u << candidate)) != 0) continue;
                SpsDeformNode node;
                float distanceSq;
                if (!sps_prepare_socket(
                    socket,
                    previousNode,
                    remainingLength / SPS_DEFORM_MAX_REACH,
                    worldRadius,
                    worldBaseActivationRadiusOverride,
                    false,
                    node,
                    distanceSq
                ) || distanceSq >= nearest) continue;
                chosen = candidate;
                chosenNode = node;
                nearest = distanceSq;
            }
        }
        if (chosen < 0) break;
        // New entrances share one reach budget. Guided stops remain available even
        // beyond it; the curve walker only travels as far as each vertex needs.
        remainingLength = max(remainingLength - sqrt(nearest), 0);
        sps_path_worlds[step] = chosenNode.world;
        sps_path_forwards[step] = chosenNode.forward;
        sps_path_flags[step] = chosenNode.flags;
        sps_path_weights[step] = chosenNode.deformationWeight;
        if (step == 1) {
            path.firstSocketUp = chosenNode.up;
            path.deformationWeight = chosenNode.deformationWeight;
        }
        previous = sockets[chosen];
        previousNode = chosenNode;
        used |= 1u << chosen;
    }
    return sps_socket_valid(previous);
}

#endif
