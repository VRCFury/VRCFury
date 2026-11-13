#ifndef SPS_INC_PATH
#define SPS_INC_PATH

#include "sps_config.cginc"

#include "sps_types.cginc"
#include "sps_utils.cginc"
#include "sps_control_points.cginc"
#include "sps_contacts.cginc"
#include "sps_lights.cginc"
#include "Packages/com.vrchat.base/ShaderLibrary/VRCContacts.cginc"

// Per-invocation scratch arrays avoid copying the path between helpers.
static float3 sps_path_worlds[SPS_CHAIN_SOCKETS_MAX + 1];
static float3 sps_path_forwards[SPS_CHAIN_SOCKETS_MAX + 1];
static uint sps_path_flags[SPS_CHAIN_SOCKETS_MAX + 1];
static float sps_path_weights[SPS_CHAIN_SOCKETS_MAX + 1];

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
    float worldLength,
    float worldRadius,
    float worldBaseActivationRadiusOverride,
    uint flags
) {
    float forwardDistance = dot(delta, currentForward);
    float distance = length(delta);
    float baseActivationRadius = worldBaseActivationRadiusOverride > 0
        ? worldBaseActivationRadiusOverride : worldRadius * SPS_BASE_RADIUS_MULTIPLIER;
    float2 sideways = float2(dot(delta, cross(currentUp, currentForward)), dot(delta, currentUp));
    float innerDistance;
    float outerDistance;
    if (forwardDistance < 0) {
        // Behind the base: a cylinder whose depth scales with its capture radius.
        outerDistance = max(length(sideways), -forwardDistance / SPS_BASE_DEPTH_MULTIPLIER) / baseActivationRadius;
        innerDistance = outerDistance / SPS_SIDE_FADE_FRACTION_MIN;
    } else {
        float2 coneAngles = radians(float2(
            SPS_CONE_HORIZONTAL_HALF_ANGLE_DEGREES,
            SPS_CONE_VERTICAL_HALF_ANGLE_DEGREES
        ));
        // Narrow the inner cone's angles too, so the fade widens with distance.
        innerDistance = length(sideways / (
            forwardDistance * tan(coneAngles * SPS_SIDE_FADE_FRACTION_MIN)
            + baseActivationRadius * SPS_SIDE_FADE_FRACTION_MIN
        ));
        outerDistance = length(sideways / (forwardDistance * tan(coneAngles) + baseActivationRadius));
    }
    float boundaryWeight = outerDistance >= 1 ? 0 : innerDistance <= 1 ? 1
        : (1 - outerDistance) / (1 - outerDistance / innerDistance);
    float farEdgeWeight = 1 - sps_saturated_map(
        distance,
        worldLength * SPS_REACH_FADE_MIN,
        worldLength * SPS_REACH_FADE_MAX
    );
    // A hole snaps at its side/back limits; only its far edge fades.
    if (sps_has_flag(flags, SPS_SOCKET_FLAG_HOLE)) boundaryWeight = boundaryWeight > 0 ? 1 : 0;
    return farEdgeWeight * boundaryWeight;
}

bool sps_prepare_socket(
    SpsSocketCandidate socket,
    SpsDeformNode previous,
    float worldLength,
    float captureLength,
    float worldRadius,
    float worldBaseActivationRadiusOverride,
    bool guided,
    bool entrance,
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
    }
    bool twoWay = sps_has_flag(socket.control, SPS_SOCKET_FLAG_DOUBLE_SIDED);
    bool unlocked = sps_has_flag(socket.control, SPS_SOCKET_FLAG_UNLOCK_ALL)
        || sps_has_flag(socket.control, SPS_SOCKET_FLAG_UNLOCK_LOCAL_X);
    // Guided stops are exempt from rejection. Unlocked two-way sockets already
    // have their orientation determined above, so they do not need a face choice.
    bool checkOneWaySharpness = !twoWay && !guided;
    bool chooseTwoWayFace = twoWay && !unlocked;
    if (checkOneWaySharpness || chooseTwoWayFace) {
        // Only the initial entrance gets lookahead. Its distance is identical for
        // both faces, but advancing along their opposite exits gives different targets.
        float advance = entrance
            ? sps_entry_advance_distance(-delta, previous.forward, -normal, worldLength) : 0;
        float sharpness = sps_bend_sharpness_sq(
            delta - normal * advance,
            previous.forward,
            -normal,
            worldLength
        );
        if (chooseTwoWayFace) {
            float opposite = sps_bend_sharpness_sq(
                delta + normal * advance,
                previous.forward,
                normal,
                worldLength
            );
            bool flip = opposite < sharpness;
            if (opposite == sharpness) {
                // Break symmetric ties in the incoming frame, independently of
                // the authored normal's sign. Prefer forward, then up, then right.
                float facing = dot(normal, previous.forward);
                if (facing == 0) facing = dot(normal, previous.up);
                if (facing == 0) facing = dot(normal, cross(previous.up, previous.forward));
                flip = facing > 0;
            }
            if (flip) normal = -normal;
        } else if (sharpness > SPS_ONE_WAY_BEND_SHARPNESS_MAX * SPS_ONE_WAY_BEND_SHARPNESS_MAX) {
            return false;
        }
    }
    node.forward = -normal;
    node.up = up;
    node.flags = socket.control & 0xfc00u; // Flags in bits 10-15, without path key or validity.
    node.deformationWeight = 1;
    if (!guided) node.deformationWeight = sps_deformation_weight(
        delta,
        previous.forward,
        previous.up,
        captureLength,
        worldRadius,
        worldBaseActivationRadiusOverride,
        node.flags
    );
    return node.deformationWeight > 0;
}

// Node zero is the plug frame; the remaining nodes are socket entrances/stops.
// fastopt keeps FXC from spending excessive compile time optimizing the nested loops.
bool sps_try_get_path(out SpsDeformPathInfo path) {
    // Unity's shader upgrader mistakes built-in array casts for legacy array declarations.
    [unroll]
    for (uint i = 0; i <= SPS_CHAIN_SOCKETS_MAX; i++) {
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
    float remainingLength = path.worldLength * SPS_REACH_FADE_MAX;
    [fastopt]
    for (uint step = 1; step <= SPS_CHAIN_SOCKETS_MAX; step++) {
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
                    path.worldLength,
                    remainingLength / SPS_REACH_FADE_MAX,
                    worldRadius,
                    worldBaseActivationRadiusOverride,
                    true,
                    false,
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
                    path.worldLength,
                    remainingLength / SPS_REACH_FADE_MAX,
                    worldRadius,
                    worldBaseActivationRadiusOverride,
                    false,
                    step == 1,
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
