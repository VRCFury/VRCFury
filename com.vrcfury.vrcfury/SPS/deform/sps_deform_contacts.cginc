#ifndef SPS_INC_DEFORM_CONTACTS
#define SPS_INC_DEFORM_CONTACTS

#include "sps_deform_globals.cginc"
#include "../common/sps_types.cginc"
#include "Packages/com.vrchat.base/ShaderLibrary/VRCContacts.cginc"

// Each receiver writes a separate buffer on the renderer.
VRC_CONTACT_DECLARE(SPS_Self);
VRC_CONTACT_DECLARE(SPS_Others);
VRC_CONTACT_DECLARE(SPS_Path);

#define SPS_CONTACT_COUNT (3 * VRC_CONTACT_MAX_TARGETS)

float4x4 sps_contact_transform(uint index) {
    if (index < VRC_CONTACT_MAX_TARGETS) return VRC_CONTACT_TRANSFORM(SPS_Self, index);
    if (index < 2 * VRC_CONTACT_MAX_TARGETS) return VRC_CONTACT_TRANSFORM(SPS_Others, index - VRC_CONTACT_MAX_TARGETS);
    return VRC_CONTACT_TRANSFORM(SPS_Path, index - 2 * VRC_CONTACT_MAX_TARGETS);
}

int sps_contact_count(uint buffer) {
    if (buffer == 0) return VRC_CONTACT_TARGETCOUNT(SPS_Self);
    if (buffer == 1) return VRC_CONTACT_TARGETCOUNT(SPS_Others);
    return VRC_CONTACT_TARGETCOUNT(SPS_Path);
}

int sps_contact_player_id(uint index) {
    if (index < VRC_CONTACT_MAX_TARGETS) return VRC_CONTACT_PLAYERID(SPS_Self, index);
    if (index < 2 * VRC_CONTACT_MAX_TARGETS) return VRC_CONTACT_PLAYERID(SPS_Others, index - VRC_CONTACT_MAX_TARGETS);
    return VRC_CONTACT_PLAYERID(SPS_Path, index - 2 * VRC_CONTACT_MAX_TARGETS);
}

uint sps_contact_tags(uint index) {
    if (index < VRC_CONTACT_MAX_TARGETS) return VRC_CONTACT_TAGS(SPS_Self, index);
    if (index < 2 * VRC_CONTACT_MAX_TARGETS) return VRC_CONTACT_TAGS(SPS_Others, index - VRC_CONTACT_MAX_TARGETS);
    return VRC_CONTACT_TAGS(SPS_Path, index - 2 * VRC_CONTACT_MAX_TARGETS);
}

bool sps_contact_present(uint index) {
    int count = sps_contact_count(index / VRC_CONTACT_MAX_TARGETS);
    return (int)(index % VRC_CONTACT_MAX_TARGETS) < clamp(count, 0, VRC_CONTACT_MAX_TARGETS);
}

float4x4 sps_contact_frame() {
    float4x4 frame = VRC_CONTACT_TRANSFORM_SELF(SPS_Self);
    if (any(frame._m02_m12_m22 != 0)) return frame;
    frame = VRC_CONTACT_TRANSFORM_SELF(SPS_Others);
    if (any(frame._m02_m12_m22 != 0)) return frame;
    return VRC_CONTACT_TRANSFORM_SELF(SPS_Path);
}

struct SpsSocketCandidate {
    float3 world;
    float3 normal;
    float3 up;
    uint flags;
    int owner;
    uint group;
    uint pathIndex;
    bool valid;
};

float3 sps_matrix_scale(float4x4 transform) {
    return float3(length(transform._m00_m10_m20), length(transform._m01_m11_m21), length(transform._m02_m12_m22));
}

SpsSocketCandidate sps_read_contact(uint index) {
    SpsSocketCandidate socket = (SpsSocketCandidate)0;
    if (!sps_contact_present(index)) return socket;
    float4x4 transform = sps_contact_transform(index);
    uint tags = sps_contact_tags(index);
    socket.owner = sps_contact_player_id(index);
    bool path = index >= 2 * VRC_CONTACT_MAX_TARGETS;
    bool others = index >= VRC_CONTACT_MAX_TARGETS && !path;
    uint channelCount = (uint)clamp(round(_SPS_OthersChannelTagCount), 0, VRC_CONTACT_MAX_TAGS - 2);
    bool native = !others || (tags & ((1u << channelCount) - 1u)) != 0;
    float3 scale = sps_matrix_scale(transform);
    socket.world = transform._m03_m13_m23;
    socket.normal = sps_normalize(transform._m02_m12_m22);
    socket.up = sps_nearest_normal(socket.normal, transform._m01_m11_m21);
    if (native) {
        uint packed = (uint)round(scale.x);
        if (packed > 32767 || packed == 0) return socket;
        socket.group = packed & 127u;
        socket.pathIndex = (packed >> 7) & 7u;
        socket.flags = ((packed >> 14) & 1u) * SPS_SOCKET_FLAG_HOLE
            | ((packed >> 13) & 1u) * SPS_SOCKET_FLAG_DOUBLE_SIDED
            | ((packed >> 12) & 1u) * SPS_SOCKET_FLAG_RADIUS_OFFSET
            | ((packed >> 11) & 1u) * SPS_SOCKET_FLAG_UNLOCK_LOCAL_X
            | ((packed >> 10) & 1u) * SPS_SOCKET_FLAG_UNLOCK_ALL;
        socket.flags |= path ? SPS_SOCKET_FLAG_GUIDED : 0u;
        socket.valid = socket.group != 0 && (path ? socket.pathIndex > 0 : socket.pathIndex == 0);
    } else {
        // Legacy Hole/Ring tags identify SPS1/2 sockets
        // If they don't have the hole/ring tag, they're either old sps4all or TPS, which means they don't have
        // scale info and we can't trust their orientation, so ignore them and we'll use their lights.
        if (!sps_to_bool(_SPS_OthersLegacyTags)) return socket;
        bool hole = (tags & (1u << channelCount)) != 0;
        bool ring = (tags & (1u << (channelCount + 1))) != 0;
        socket.valid = hole || ring;
        socket.flags = hole && !ring ? SPS_SOCKET_FLAG_HOLE : 0u;
        if (ring && !hole) socket.flags |= SPS_SOCKET_FLAG_DOUBLE_SIDED;
    }
    socket.valid = socket.valid && all(scale > 0) && all(isfinite(socket.world));
    return socket;
}

#endif
