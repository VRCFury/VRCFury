#ifndef SPS_INC_RESOLVER_FRAG
#define SPS_INC_RESOLVER_FRAG

#include "../common/sps_cell_frag.cginc"
#include "sps_resolver_read.cginc"
#include "sps_resolver_payload.cginc"
#include "sps_resolver_types.cginc"

float sps_resolver_vector_component(float3 value, int component) {
    if (component == 0) return value.x;
    if (component == 1) return value.y;
    return value.z;
}

uint sps_resolver_id() {
    return sps_id();
}

uint sps_resolver_player_id() {
    return sps_player_id();
}

float sps_resolver_chain_apply_lerp(v2f input, int segmentIndex) {
    if (segmentIndex < 0) return 0;
    if (input.chainSlotIndex[segmentIndex] <= SPS_CHAIN_REF_INVALID) return 0;
    return input.chainApplyLerp[segmentIndex] * saturate(_SPS_Enabled);
}

bool sps_resolver_payload_rgba(SpsTexture socketTex, v2f input, uint payloadIndex, out float4 rgba) {
    rgba = 0;
    if (payloadIndex >= SPS_RESOLVER_PAYLOAD_VALUES) return false;

    if (payloadIndex == SPS_RESOLVER_PAYLOAD_APPLY_LERP_INDEX) {
        rgba = sps_encode_float(sps_resolver_chain_apply_lerp(input, 0));
        return true;
    }
    if (payloadIndex < SPS_RESOLVER_CHAIN_BASE) return true;

    uint chainValueIndex = payloadIndex - SPS_RESOLVER_CHAIN_BASE;
    uint segmentIndex = chainValueIndex / SPS_RESOLVER_CHAIN_STRIDE;
    if (input.chainSlotIndex[segmentIndex] <= SPS_CHAIN_REF_INVALID) return true;

    uint fieldIndex = chainValueIndex - segmentIndex * SPS_RESOLVER_CHAIN_STRIDE;
    CellData socket = sps_read_cell(socketTex, input.chainSlotIndex[segmentIndex]);
    SocketData socketData = sps_read_socket(socketTex, socket.cellIndex);
    if (fieldIndex < 3) {
        float3 sampleWorld = sps_resolver_socket_target_world(socket, socketData.flags);
        rgba = sps_encode_float(sps_resolver_vector_component(sampleWorld, (int)fieldIndex));
        return true;
    }
    if (fieldIndex < 6) {
        float3 sampleForward = -(socket.normal * (input.chainFlipped[segmentIndex] ? -1 : 1));
        rgba = sps_encode_float(sps_resolver_vector_component(sampleForward, (int)(fieldIndex - 3u)));
        return true;
    }
    if (fieldIndex < 9) {
        float3 sampleUp = sps_nearest_normal(
            -(socket.normal * (input.chainFlipped[segmentIndex]) ? -1 : 1),
            socket.up
        );
        rgba = sps_encode_float(sps_resolver_vector_component(sampleUp, (int)(fieldIndex - 6u)));
        return true;
    }
    if (fieldIndex < 10) {
        rgba = sps_encode_uint(socketData.flags);
        return true;
    }
    rgba = sps_encode_float(sps_resolver_chain_apply_lerp(input, (int)segmentIndex));
    return true;
}

float4 sps_resolver_frag(SpsTexture socketTex, v2f input) {
    int cellIndex = input.cellIndex;
    uint pixelIndex;
    float4 rgba;
    if (sps_cell_frag(
        cellIndex,
        input.vertex,
        pixelIndex,
        rgba
    )) return rgba;

    uint resolverId = sps_resolver_id();
    uint resolverPlayerId = sps_resolver_player_id();
    if (sps_try_get_slot_header_rgba(
        pixelIndex,
        resolverId,
        resolverPlayerId,
        SPS_PRODUCT_PLUG,
        sps_object_origin_world(),
        sps_object_forward_world(),
        sps_object_up_world(),
        sps_object_scale_world(),
        input.debug,
        rgba
    )) {
        return rgba;
    }

    uint payloadIndex;
    if (sps_cell_payload_index_from_pixel_index(pixelIndex, payloadIndex)
        && sps_resolver_payload_rgba(socketTex, input, payloadIndex, rgba)) {
        return rgba;
    }

    return 0;
}

#endif
