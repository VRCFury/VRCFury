#ifndef SPS_INC_SOCKET_FRAG
#define SPS_INC_SOCKET_FRAG

#include "../common/sps_cell_layout.cginc"

float sps_socket_vector_component(float3 value, uint componentIndex) {
    if (componentIndex == 0u) return value.x;
    if (componentIndex == 1u) return value.y;
    return value.z;
}

bool sps_try_get_socket_payload_rgba(
    uint index,
    uint flags,
    uint nextId,
    uint tags[SPS_SOCKET_PAYLOAD_TAG_COUNT],
    float3 tangentIn,
    float3 tangentOut,
    out float4 rgba
) {
    rgba = 0;
    if (index == sps_cell_pixel_index_from_payload_index(SPS_SOCKET_PAYLOAD_FLAGS)) {
        rgba = sps_encode_uint(flags);
        return true;
    }
    if (index == sps_cell_pixel_index_from_payload_index(SPS_SOCKET_PAYLOAD_NEXT_ID)) {
        rgba = sps_encode_uint(nextId);
        return true;
    }
    uint payloadIndex;
    if (!sps_cell_payload_index_from_pixel_index(index, payloadIndex)) return false;
    if (payloadIndex >= SPS_SOCKET_PAYLOAD_TAG_START
        && payloadIndex < SPS_SOCKET_PAYLOAD_TAG_START + SPS_SOCKET_PAYLOAD_TAG_COUNT) {
        rgba = sps_encode_uint(tags[payloadIndex - SPS_SOCKET_PAYLOAD_TAG_START]);
        return true;
    }
    if (payloadIndex >= SPS_SOCKET_PAYLOAD_TANGENT_IN_START
        && payloadIndex < SPS_SOCKET_PAYLOAD_TANGENT_IN_START + 3u) {
        rgba = sps_encode_float(sps_socket_vector_component(tangentIn, payloadIndex - SPS_SOCKET_PAYLOAD_TANGENT_IN_START));
        return true;
    }
    if (payloadIndex >= SPS_SOCKET_PAYLOAD_TANGENT_OUT_START
        && payloadIndex < SPS_SOCKET_PAYLOAD_TANGENT_OUT_START + 3u) {
        rgba = sps_encode_float(sps_socket_vector_component(tangentOut, payloadIndex - SPS_SOCKET_PAYLOAD_TANGENT_OUT_START));
        return true;
    }
    return false;
}

#endif
