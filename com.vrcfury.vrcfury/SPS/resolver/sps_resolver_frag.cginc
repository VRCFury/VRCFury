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

#define SPS_SELECT_5(result, index, value0, value1, value2, value3, value4) \
    { \
        uint spsSelectIndex = (uint)(index); \
        if (spsSelectIndex == 0u) result = (value0); \
        else if (spsSelectIndex == 1u) result = (value1); \
        else if (spsSelectIndex == 2u) result = (value2); \
        else if (spsSelectIndex == 3u) result = (value3); \
        else result = (value4); \
    }

float sps_resolver_chain_apply_lerp(v2f input, int segmentIndex) {
    if (segmentIndex < 0) return 0;
    uint safeSegmentIndex = (uint)segmentIndex;
    int chainSlotIndex = SPS_CHAIN_REF_INVALID;
    float chainApplyLerp = 0;
    SPS_SELECT_5(chainSlotIndex, safeSegmentIndex, input.chainSlotIndex[0], input.chainSlotIndex[1], input.chainSlotIndex[2], input.chainSlotIndex[3], input.chainSlotIndex[4]);
    SPS_SELECT_5(chainApplyLerp, safeSegmentIndex, input.chainApplyLerp[0], input.chainApplyLerp[1], input.chainApplyLerp[2], input.chainApplyLerp[3], input.chainApplyLerp[4]);
    if (chainSlotIndex <= SPS_CHAIN_REF_INVALID) return 0;
    return chainApplyLerp * saturate(_SPS_Enabled);
}

float sps_resolver_metadata_color_component(uint payloadIndex) {
    float value = _SPS_MetadataColor.z;
    if (payloadIndex == SPS_RESOLVER_METADATA_COLOR_X_INDEX) value = _SPS_MetadataColor.x;
    else if (payloadIndex == SPS_RESOLVER_METADATA_COLOR_Y_INDEX) value = _SPS_MetadataColor.y;
    return value;
}

void sps_resolver_first_socket_data(
    SpsTexture socketTex,
    v2f input,
    out bool found,
    out CellData socketCell,
    out SocketData socketData
) {
    socketCell = sps_make_empty_cell();
    socketData = sps_make_empty_socket();
    found = input.chainSlotIndex[0] > SPS_CHAIN_REF_INVALID;
    if (!found) return;
    socketCell = sps_read_cell(socketTex, input.chainSlotIndex[0]);
    socketData = sps_read_socket(socketTex, socketCell.cellIndex);
}

float sps_resolver_first_socket_fraction(CellData socketCell, SocketData socketData) {
    float resolverLength = sps_resolver_length();
    if (resolverLength <= 0) return 0;

    float3 plugWorld = sps_object_origin_world();
    float3 targetWorld = sps_resolver_socket_target_world(socketCell, socketData.flags);
    float distanceToTarget = length(targetWorld - plugWorld);
    return saturate(1.0 - distanceToTarget / resolverLength);
}

bool sps_resolver_payload_rgba(SpsTexture socketTex, v2f input, uint payloadIndex, out float4 rgba) {
    rgba = 0;
    if (payloadIndex >= SPS_RESOLVER_PAYLOAD_VALUES) return false;

    if (payloadIndex == SPS_RESOLVER_PAYLOAD_APPLY_LERP_INDEX) {
        rgba = sps_encode_float(sps_resolver_chain_apply_lerp(input, 0));
        return true;
    }
    if (payloadIndex >= SPS_RESOLVER_METADATA_BASE
        && payloadIndex < SPS_RESOLVER_METADATA_BASE + SPS_RESOLVER_METADATA_VALUES) {
        if (payloadIndex <= SPS_RESOLVER_METADATA_COLOR_Z_INDEX) {
            rgba = sps_encode_float(sps_resolver_metadata_color_component(payloadIndex));
            return true;
        }
        if (payloadIndex == SPS_RESOLVER_METADATA_LENGTH_INDEX) {
            rgba = sps_encode_float(sps_resolver_length());
            return true;
        }
        if (payloadIndex == SPS_RESOLVER_METADATA_RADIUS_INDEX) {
            rgba = sps_encode_float(sps_resolver_radius());
            return true;
        }
        bool foundSocket;
        CellData firstSocketCell;
        SocketData firstSocketData;
        sps_resolver_first_socket_data(socketTex, input, foundSocket, firstSocketCell, firstSocketData);
        if (payloadIndex == SPS_RESOLVER_METADATA_SOCKET_PLAYER_ID_INDEX) {
            rgba = sps_encode_uint(foundSocket ? firstSocketCell.playerId : 0u);
            return true;
        }
        if (payloadIndex == SPS_RESOLVER_METADATA_SOCKET_UNIQUE_ID_INDEX) {
            rgba = sps_encode_uint(foundSocket ? firstSocketCell.id : 0u);
            return true;
        }
        rgba = sps_encode_float(foundSocket ? sps_resolver_first_socket_fraction(firstSocketCell, firstSocketData) : 0);
        return true;
    }
    if (payloadIndex >= SPS_RESOLVER_RADIUS_SAMPLE_BASE
        && payloadIndex < SPS_RESOLVER_RADIUS_SAMPLE_BASE + SPS_RESOLVER_RADIUS_SAMPLE_COUNT) {
        rgba = sps_encode_float(sps_resolver_radius_sample((int)(payloadIndex - SPS_RESOLVER_RADIUS_SAMPLE_BASE)));
        return true;
    }
    if (payloadIndex < SPS_RESOLVER_CHAIN_BASE) return true;
    if (payloadIndex >= SPS_RESOLVER_CHAIN_END) return true;

    uint chainValueIndex = payloadIndex - SPS_RESOLVER_CHAIN_BASE;
    uint segmentIndex = chainValueIndex / SPS_RESOLVER_CHAIN_STRIDE;
    int chainSlotIndex = SPS_CHAIN_REF_INVALID;
    bool chainFlipped = false;
    SPS_SELECT_5(chainSlotIndex, segmentIndex, input.chainSlotIndex[0], input.chainSlotIndex[1], input.chainSlotIndex[2], input.chainSlotIndex[3], input.chainSlotIndex[4]);
    SPS_SELECT_5(chainFlipped, segmentIndex, input.chainFlipped[0], input.chainFlipped[1], input.chainFlipped[2], input.chainFlipped[3], input.chainFlipped[4]);
    if (chainSlotIndex <= SPS_CHAIN_REF_INVALID) return true;

    uint fieldIndex = chainValueIndex - segmentIndex * SPS_RESOLVER_CHAIN_STRIDE;
    CellData socket = sps_read_cell(socketTex, chainSlotIndex);
    SocketData socketData = sps_read_socket(socketTex, socket.cellIndex);
    if (fieldIndex < 3) {
        float3 sampleWorld = sps_resolver_socket_target_world(socket, socketData.flags);
        rgba = sps_encode_float(sps_resolver_vector_component(sampleWorld, (int)fieldIndex));
        return true;
    }
    if (fieldIndex < 6) {
        float3 sampleForward = -(socket.normal * (chainFlipped ? -1 : 1));
        rgba = sps_encode_float(sps_resolver_vector_component(sampleForward, (int)(fieldIndex - 3u)));
        return true;
    }
    if (fieldIndex < 9) {
        float3 sampleUp = sps_nearest_normal(
            -(socket.normal * (chainFlipped ? -1 : 1)),
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
