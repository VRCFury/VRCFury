#ifndef SPS_INC_RESOLVER_PAYLOAD
#define SPS_INC_RESOLVER_PAYLOAD

#include "../common/sps_id.cginc"
#include "../common/sps_cell_layout.cginc"

#define SPS_RESOLVER_PAYLOAD_APPLY_LERP_INDEX 0u
#define SPS_RESOLVER_CHAIN_BASE 1u
#define SPS_RESOLVER_CHAIN_STRIDE 11u
// Extends through +8: world xyz, forward xyz, up xyz.
#define SPS_RESOLVER_CHAIN_WORLD_INDEX(segmentIndex) (SPS_RESOLVER_CHAIN_BASE + (uint)(segmentIndex) * SPS_RESOLVER_CHAIN_STRIDE + 0u)
#define SPS_RESOLVER_CHAIN_FORWARD_INDEX(segmentIndex) (SPS_RESOLVER_CHAIN_BASE + (uint)(segmentIndex) * SPS_RESOLVER_CHAIN_STRIDE + 3u)
#define SPS_RESOLVER_CHAIN_UP_INDEX(segmentIndex) (SPS_RESOLVER_CHAIN_BASE + (uint)(segmentIndex) * SPS_RESOLVER_CHAIN_STRIDE + 6u)
#define SPS_RESOLVER_CHAIN_FLAGS_INDEX(segmentIndex) (SPS_RESOLVER_CHAIN_BASE + (uint)(segmentIndex) * SPS_RESOLVER_CHAIN_STRIDE + 9u)
#define SPS_RESOLVER_CHAIN_PULLOUT_LERP_INDEX(segmentIndex) (SPS_RESOLVER_CHAIN_BASE + (uint)(segmentIndex) * SPS_RESOLVER_CHAIN_STRIDE + 10u)
#define SPS_RESOLVER_PAYLOAD_VALUES (SPS_RESOLVER_CHAIN_BASE + SPS_CHAIN_MAX_SOCKETS * SPS_RESOLVER_CHAIN_STRIDE)

uint sps_resolver_chain_payload_index(uint baseIndex, int sampleIndex) {
    return sps_cell_pixel_index_from_payload_index(baseIndex + (uint)(sampleIndex - 1) * SPS_RESOLVER_CHAIN_STRIDE);
}

float3 sps_read_resolver_chain_float3(SpsCell cell, uint baseIndex, int sampleIndex) {
    return cell.read_float3(sps_resolver_chain_payload_index(baseIndex, sampleIndex));
}

uint sps_read_resolver_chain_uint(SpsCell cell, uint baseIndex, int sampleIndex) {
    return cell.read_uint(sps_resolver_chain_payload_index(baseIndex, sampleIndex));
}

float sps_read_resolver_chain_float(SpsCell cell, uint baseIndex, int sampleIndex) {
    return cell.read_float(sps_resolver_chain_payload_index(baseIndex, sampleIndex));
}

float3 sps_read_resolver_chain_world(SpsCell cell, int sampleIndex) {
    if (sampleIndex <= 0) return sps_cell_header_world(cell);
    return sps_read_resolver_chain_float3(cell, SPS_RESOLVER_CHAIN_WORLD_INDEX(0), sampleIndex);
}

float3 sps_read_resolver_chain_forward(SpsCell cell, int sampleIndex) {
    if (sampleIndex <= 0) return sps_cell_header_forward(cell);
    return sps_read_resolver_chain_float3(cell, SPS_RESOLVER_CHAIN_FORWARD_INDEX(0), sampleIndex);
}

float3 sps_read_resolver_chain_up(SpsCell cell, int sampleIndex) {
    if (sampleIndex <= 0) return sps_cell_header_up(cell);
    return sps_read_resolver_chain_float3(cell, SPS_RESOLVER_CHAIN_UP_INDEX(0), sampleIndex);
}

uint sps_read_resolver_chain_flags(SpsCell cell, int sampleIndex) {
    if (sampleIndex <= 0) return 0u;
    return sps_read_resolver_chain_uint(cell, SPS_RESOLVER_CHAIN_FLAGS_INDEX(0), sampleIndex);
}

float sps_read_resolver_chain_pullout_lerp(SpsCell cell, int sampleIndex) {
    if (sampleIndex <= 0) return 0;
    return sps_read_resolver_chain_float(cell, SPS_RESOLVER_CHAIN_PULLOUT_LERP_INDEX(0), sampleIndex);
}

bool sps_try_find_resolver_data(
    SpsTexture tex,
    uint id,
    uint playerId,
    out int outSlotIndex,
    out float outApplyLerp
) {
    outSlotIndex = -1;
    outApplyLerp = 0;

    uint slotSeed = sps_id_hash();
    for (uint replica = 0; replica < SPS_CELL_REPLICA_COUNT; replica++) {
        uint slotIndex = sps_hashed_screen_slot_index_from_id(slotSeed, replica);
        SpsCell cell = sps_get_cell(tex, slotIndex);
        if (!sps_cell_check_magic(cell)
            || cell.read_uint(SPS_HEADER_VENDOR_INDEX) != SPS_VENDOR_SPS
            || cell.read_uint(SPS_HEADER_PRODUCT_INDEX) != SPS_PRODUCT_PLUG) {
            continue;
        }

        if (sps_cell_header_unique_id(cell) != id) continue;
        if (sps_cell_header_player_id(cell) != playerId) continue;
        outApplyLerp = saturate(cell.read_float(sps_cell_pixel_index_from_payload_index(SPS_RESOLVER_PAYLOAD_APPLY_LERP_INDEX)));
        outSlotIndex = (int)slotIndex;
        return true;
    }
    return false;
}

#endif
