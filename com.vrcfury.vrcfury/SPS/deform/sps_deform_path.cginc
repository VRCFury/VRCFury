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
    float3 tangentIn;
    float3 tangentOut;
    uint flags;
    bool guided;
};

struct SpsDeformPath {
    // Node zero is the plug frame; the remaining nodes are socket entrances/stops.
    SpsDeformNode nodes[SPS_CHAIN_MAX_SOCKETS + 1];
    float worldLength;
    float bakeScale;
};

bool sps_prepare_socket(SpsSocketCandidate socket, SpsDeformNode previous, float worldLength,
    bool first, bool guided, out SpsDeformNode node, out float distanceSq) {
    node = (SpsDeformNode)0;
    node.world = socket.world;
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
    node.guided = guided;
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

bool sps_try_get_path(out SpsDeformPath path) {
    path = (SpsDeformPath)0;
    float4x4 frame = sps_contact_frame();
    float frameScale = length(frame._m02_m12_m22);
    if (!(frameScale > 0)) return false;
    path.nodes[0].world = frame._m03_m13_m23;
    path.nodes[0].forward = sps_normalize(frame._m02_m12_m22);
    path.nodes[0].up = sps_nearest_normal(path.nodes[0].forward, frame._m01_m11_m21);
    path.bakeScale = frameScale;
    path.worldLength = _SPS_BakedLength * path.bakeScale;
    if (!(path.worldLength > 0)) return false;

    SpsSocketCandidate sockets[SPS_CONTACT_COUNT + 4];
    [loop]
    for (uint i = 0; i < SPS_CONTACT_COUNT; i++) {
        sockets[i] = sps_read_contact(i);
    }
    [unroll]
    for (uint lightIndex = 0; lightIndex < 4; lightIndex++) {
        sockets[SPS_CONTACT_COUNT + lightIndex] = sps_read_light(lightIndex, path.nodes[0].world, path.nodes[0].up);
    }

    uint used = 0;
    SpsSocketCandidate previous = (SpsSocketCandidate)0;
    [unroll]
    for (uint step = 1; step <= SPS_CHAIN_MAX_SOCKETS; step++) {
        int chosen = -1;
        SpsDeformNode chosenNode = (SpsDeformNode)0;
        float nearest = 1e30;
        // Look for path stops first
        if (previous.group != 0) {
            [loop]
            for (int candidate = 2 * VRC_CONTACT_MAX_TARGETS; candidate < SPS_CONTACT_COUNT; candidate++) {
                SpsSocketCandidate socket = sockets[candidate];
                if (!socket.valid || (used & (1u << candidate)) != 0) continue;
                if (socket.owner != previous.owner || socket.group != previous.group
                    || socket.pathIndex != previous.pathIndex + 1) continue;
                SpsDeformNode node;
                float distanceSq;
                if (!sps_prepare_socket(socket, path.nodes[step - 1], path.worldLength,
                    step == 1, true, node, distanceSq) || distanceSq >= nearest) continue;
                chosen = candidate;
                chosenNode = node;
                nearest = distanceSq;
            }
        }
        // If no path stop, look for a new socket entrance (unless it just entered a hole)
        if (chosen < 0 && (step == 1 || !sps_has_flag(path.nodes[step - 1].flags, SPS_SOCKET_FLAG_HOLE))) {
            [loop]
            for (int slot = 0; slot < 2 * VRC_CONTACT_MAX_TARGETS + 4; slot++) {
                int candidate = slot < 2 * VRC_CONTACT_MAX_TARGETS ? slot : slot + VRC_CONTACT_MAX_TARGETS;
                SpsSocketCandidate socket = sockets[candidate];
                if (!socket.valid || socket.pathIndex != 0 || (used & (1u << candidate)) != 0) continue;
                SpsDeformNode node;
                float distanceSq;
                if (!sps_prepare_socket(socket, path.nodes[step - 1], path.worldLength,
                    step == 1, false, node, distanceSq) || distanceSq >= nearest) continue;
                chosen = candidate;
                chosenNode = node;
                nearest = distanceSq;
            }
        }
        if (chosen < 0) break;
        path.nodes[step] = chosenNode;
        previous = sockets[chosen];
        used |= 1u << chosen;
    }
    return previous.valid;
}

#endif
