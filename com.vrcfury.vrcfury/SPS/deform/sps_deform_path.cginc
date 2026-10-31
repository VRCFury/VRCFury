#ifndef SPS_INC_DEFORM_PATH
#define SPS_INC_DEFORM_PATH

#include "../common/sps_types.cginc"
#include "sps_deform_contacts.cginc"
#include "sps_deform_lights.cginc"
#include "Packages/com.vrchat.base/ShaderLibrary/VRCContacts.cginc"

#define SPS_CHAIN_MAX_SOCKETS 8

struct SpsDeformNode {
    float3 world;
    float3 forward;
    float3 up;
    uint flags;
};

struct SpsDeformPathInfo {
    float3 plugUp;
    float3 firstSocketUp;
    float worldLength;
    float bakeScale;
};

bool sps_prepare_socket(SpsSocketCandidate socket, SpsDeformNode previous, float worldLength, float worldRadius,
    bool first, bool guided, out SpsDeformNode node, out float distanceSq) {
    node = (SpsDeformNode)0;
    node.world = socket.world;
    // Offset in the socket's own up direction before distance/approach checks.
    // Orientation unlocking below must not change which side of the socket we target.
    if (sps_has_flag(socket.flags, SPS_SOCKET_FLAG_RADIUS_OFFSET)) {
        node.world += socket.up * worldRadius;
    }
    float3 delta = node.world - previous.world;
    distanceSq = dot(delta, delta);
    float3 direction = sps_normalize(delta);
    float3 normal = socket.normal;
    float3 up = sps_nearest_normal(normal, socket.up);
    if (sps_has_flag(socket.flags, SPS_SOCKET_FLAG_UNLOCK_ALL)) {
        normal = -direction;
        up = sps_nearest_normal(normal, up);
    } else if (sps_has_flag(socket.flags, SPS_SOCKET_FLAG_UNLOCK_LOCAL_X)) {
        float3 right = sps_normalize(cross(up, -normal));
        normal = sps_nearest_normal(right, -direction);
        up = sps_normalize(cross(right, normal));
    } else if (sps_has_flag(socket.flags, SPS_SOCKET_FLAG_DOUBLE_SIDED) && dot(normal, direction) > 0) {
        normal = -normal;
    }
    node.forward = -normal;
    node.up = up;
    node.flags = socket.flags;
    if (guided) return true;
    bool hilted = first && sps_has_flag(socket.flags, SPS_SOCKET_FLAG_HOLE)
        && distanceSq <= worldLength * worldLength * 0.25;
    if (!hilted) {
        // Socket inward direction (-normal) is more than 108 degrees from our current travel direction.
        if (dot(-normal, previous.forward) < -0.309016994) return false;
        // Previous-to-socket direction points within 36 degrees of its outward normal: approaching from behind.
        if (dot(normal, direction) > 0.809016994) return false;
        // Socket is beside or behind the previous node, rather than ahead along its travel direction.
        if (dot(direction, previous.forward) <= 0) return false;
    }
    // Socket is >1.6x plug length away
    if (distanceSq > worldLength * worldLength * 2.56) return false;
    return true;
}

// Separate arrays avoid FXC's padded struct-array copies; keep them as direct outputs.
// Node zero is the plug frame; the remaining nodes are socket entrances/stops.
// fastopt keeps FXC from spending excessive compile time optimizing the nested loops.
bool sps_try_get_path(
    out float3 nodeWorlds[SPS_CHAIN_MAX_SOCKETS + 1],
    out float3 nodeForwards[SPS_CHAIN_MAX_SOCKETS + 1],
    out uint nodeFlags[SPS_CHAIN_MAX_SOCKETS + 1],
    out SpsDeformPathInfo path
) {
    // Unity's shader upgrader mistakes built-in array casts for legacy array declarations.
    [unroll]
    for (uint i = 0; i <= SPS_CHAIN_MAX_SOCKETS; i++) {
        nodeWorlds[i] = 0;
        nodeForwards[i] = 0;
        nodeFlags[i] = 0;
    }
    path = (SpsDeformPathInfo)0;
    float4x4 frame = sps_contact_frame();
    float frameScale = length(frame._m02_m12_m22);
    if (!(frameScale > 0)) return false;
    nodeWorlds[0] = frame._m03_m13_m23;
    nodeForwards[0] = sps_normalize(frame._m02_m12_m22);
    path.plugUp = sps_nearest_normal(nodeForwards[0], frame._m01_m11_m21);
    path.bakeScale = frameScale;
    path.worldLength = _SPS_BakedLength * path.bakeScale;
    float worldRadius = _SPS_BakedRadius * path.bakeScale;
    if (!(path.worldLength > 0)) return false;

    SpsSocketCandidate sockets[SPS_CONTACT_COUNT + 4];
    [fastopt]
    for (uint i = 0; i < SPS_CONTACT_COUNT; i++) {
        sockets[i] = sps_read_contact(i);
    }
    [fastopt]
    for (uint lightIndex = 0; lightIndex < 4; lightIndex++) {
        sockets[SPS_CONTACT_COUNT + lightIndex] = sps_read_light(lightIndex, nodeWorlds[0], path.plugUp);
    }

    uint used = 0;
    SpsSocketCandidate previous = (SpsSocketCandidate)0;
    SpsDeformNode previousNode = (SpsDeformNode)0;
    previousNode.world = nodeWorlds[0];
    previousNode.forward = nodeForwards[0];
    previousNode.up = path.plugUp;
    [fastopt]
    for (uint step = 1; step <= SPS_CHAIN_MAX_SOCKETS; step++) {
        int chosen = -1;
        SpsDeformNode chosenNode = (SpsDeformNode)0;
        float nearest = 1e30;
        // Look for path stops first
        if (previous.pathKey != 0) {
            [fastopt]
            for (int candidate = 2 * VRC_CONTACT_MAX_TARGETS; candidate < SPS_CONTACT_COUNT; candidate++) {
                SpsSocketCandidate socket = sockets[candidate];
                if (!socket.valid || (used & (1u << candidate)) != 0) continue;
                // Adding 128 advances the path index without changing the low 7-bit group.
                if (socket.owner != previous.owner || socket.pathKey != previous.pathKey + 128u) continue;
                SpsDeformNode node;
                float distanceSq;
                if (!sps_prepare_socket(socket, previousNode, path.worldLength, worldRadius,
                    step == 1, true, node, distanceSq) || distanceSq >= nearest) continue;
                chosen = candidate;
                chosenNode = node;
                nearest = distanceSq;
            }
        }
        // If no path stop, look for a new socket entrance (unless it just entered a hole)
        if (chosen < 0 && (step == 1 || !sps_has_flag(previousNode.flags, SPS_SOCKET_FLAG_HOLE))) {
            [fastopt]
            for (int slot = 0; slot < 2 * VRC_CONTACT_MAX_TARGETS + 4; slot++) {
                int candidate = slot < 2 * VRC_CONTACT_MAX_TARGETS ? slot : slot + VRC_CONTACT_MAX_TARGETS;
                SpsSocketCandidate socket = sockets[candidate];
                if (!socket.valid || socket.pathKey >= 128u || (used & (1u << candidate)) != 0) continue;
                SpsDeformNode node;
                float distanceSq;
                if (!sps_prepare_socket(socket, previousNode, path.worldLength, worldRadius,
                    step == 1, false, node, distanceSq) || distanceSq >= nearest) continue;
                chosen = candidate;
                chosenNode = node;
                nearest = distanceSq;
            }
        }
        if (chosen < 0) break;
        nodeWorlds[step] = chosenNode.world;
        nodeForwards[step] = chosenNode.forward;
        nodeFlags[step] = chosenNode.flags;
        if (step == 1) path.firstSocketUp = chosenNode.up;
        previous = sockets[chosen];
        previousNode = chosenNode;
        used |= 1u << chosen;
    }
    return previous.valid;
}

#endif
