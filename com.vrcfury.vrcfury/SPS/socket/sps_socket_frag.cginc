#ifndef SPS_INC_SOCKET_FRAG
#define SPS_INC_SOCKET_FRAG

#include "../common/sps_cell_layout.cginc"

bool sps_try_get_socket_payload_rgba(
    uint index,
    uint flags,
    uint nextId,
    uint tags[SPS_SOCKET_PAYLOAD_TAG_COUNT],
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
    return false;
}

#endif
