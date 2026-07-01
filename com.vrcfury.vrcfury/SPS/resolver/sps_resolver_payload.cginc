#ifndef SPS_INC_RESOLVER_PAYLOAD
#define SPS_INC_RESOLVER_PAYLOAD

#include "../common/sps_id.cginc"
#include "../common/sps_cell_layout.cginc"

#define SPS_RESOLVER_CHAIN_BASE 0u
#define SPS_RESOLVER_CHAIN_STRIDE 10u
#define SPS_RESOLVER_CHAIN_VALUES (SPS_CHAIN_MAX_SOCKETS * SPS_RESOLVER_CHAIN_STRIDE)
#define SPS_RESOLVER_CHAIN_END (SPS_RESOLVER_CHAIN_BASE + SPS_RESOLVER_CHAIN_VALUES)
#define SPS_RESOLVER_METADATA_BASE 176u
#define SPS_RESOLVER_METADATA_COLOR_X_INDEX (SPS_RESOLVER_METADATA_BASE + 0u)
#define SPS_RESOLVER_METADATA_COLOR_Y_INDEX (SPS_RESOLVER_METADATA_BASE + 1u)
#define SPS_RESOLVER_METADATA_COLOR_Z_INDEX (SPS_RESOLVER_METADATA_BASE + 2u)
#define SPS_RESOLVER_METADATA_LENGTH_INDEX (SPS_RESOLVER_METADATA_BASE + 3u)
#define SPS_RESOLVER_METADATA_RADIUS_INDEX (SPS_RESOLVER_METADATA_BASE + 4u)
#define SPS_RESOLVER_METADATA_SOCKET_PLAYER_ID_INDEX (SPS_RESOLVER_METADATA_BASE + 5u)
#define SPS_RESOLVER_METADATA_SOCKET_UNIQUE_ID_INDEX (SPS_RESOLVER_METADATA_BASE + 6u)
#define SPS_RESOLVER_METADATA_SOCKET_FRACTION_INDEX (SPS_RESOLVER_METADATA_BASE + 7u)
#define SPS_RESOLVER_METADATA_VALUES 8u
#define SPS_RESOLVER_RADIUS_SAMPLE_COUNT 32u
#define SPS_RESOLVER_RADIUS_SAMPLE_BASE 192u
// Extends through +9: world xyz, forward xyz, up xyz, flags.
#define SPS_RESOLVER_CHAIN_WORLD_INDEX(segmentIndex) (SPS_RESOLVER_CHAIN_BASE + (uint)(segmentIndex) * SPS_RESOLVER_CHAIN_STRIDE + 0u)
#define SPS_RESOLVER_CHAIN_FORWARD_INDEX(segmentIndex) (SPS_RESOLVER_CHAIN_BASE + (uint)(segmentIndex) * SPS_RESOLVER_CHAIN_STRIDE + 3u)
#define SPS_RESOLVER_CHAIN_UP_INDEX(segmentIndex) (SPS_RESOLVER_CHAIN_BASE + (uint)(segmentIndex) * SPS_RESOLVER_CHAIN_STRIDE + 6u)
#define SPS_RESOLVER_CHAIN_FLAGS_INDEX(segmentIndex) (SPS_RESOLVER_CHAIN_BASE + (uint)(segmentIndex) * SPS_RESOLVER_CHAIN_STRIDE + 9u)
#define SPS_RESOLVER_PAYLOAD_VALUES (SPS_RESOLVER_RADIUS_SAMPLE_BASE + SPS_RESOLVER_RADIUS_SAMPLE_COUNT)

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
    float3 value;
    if (sampleIndex <= 0) value = sps_cell_header_world(cell);
    else value = sps_read_resolver_chain_float3(cell, SPS_RESOLVER_CHAIN_WORLD_INDEX(0), sampleIndex);
    return value;
}

float3 sps_read_resolver_chain_forward(SpsCell cell, int sampleIndex) {
    float3 value;
    if (sampleIndex <= 0) value = sps_cell_header_forward(cell);
    else value = sps_read_resolver_chain_float3(cell, SPS_RESOLVER_CHAIN_FORWARD_INDEX(0), sampleIndex);
    return value;
}

float3 sps_read_resolver_chain_up(SpsCell cell, int sampleIndex) {
    if (sampleIndex <= 0) return sps_cell_header_up(cell);
    return sps_read_resolver_chain_float3(cell, SPS_RESOLVER_CHAIN_UP_INDEX(0), sampleIndex);
}

uint sps_read_resolver_chain_flags(SpsCell cell, int sampleIndex) {
    if (sampleIndex <= 0) return 0u;
    return sps_read_resolver_chain_uint(cell, SPS_RESOLVER_CHAIN_FLAGS_INDEX(0), sampleIndex);
}

uint sps_resolver_radius_payload_index(int sampleIndex) {
    return sps_cell_pixel_index_from_payload_index(
        SPS_RESOLVER_RADIUS_SAMPLE_BASE + (uint)clamp(sampleIndex, 0, (int)SPS_RESOLVER_RADIUS_SAMPLE_COUNT - 1)
    );
}

float sps_read_resolver_radius_sample(SpsCell cell, int sampleIndex) {
    return cell.read_float(sps_resolver_radius_payload_index(sampleIndex));
}

#endif
