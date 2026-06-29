#ifndef SPS_INC_CELL_LAYOUT
#define SPS_INC_CELL_LAYOUT

#include "sps_cell_hash.cginc"
#include "sps_texture.cginc"
#include "sps_encode.cginc"

#define SPS_SOCKET_MAX_SLOTS 4096
#define SPS_CELL_WIDTH 16
#define SPS_CELL_HEIGHT 16
#define SPS_CELL_ROW_SHIFT 4
#define SPS_CELL_COL_MASK ((uint)SPS_CELL_WIDTH - 1)
#define SPS_CELL_HEADER_TOP_ROW_COUNT 1
#define SPS_CELL_HEADER_BOTTOM_ROW_COUNT 1
#define SPS_CELL_PAYLOAD_START SPS_CELL_WIDTH
#define SPS_CELL_DICTIONARY_GROUP_SIZE 16
#define SPS_CELL_DICTIONARY_GROUP_COUNT 256
#define SPS_CELL_DICTIONARY_MAGIC float4(1, 0, 1, 1)
#define SPS_CELL_REPLICA_COUNT 5
#define SPS_CHAIN_MAX_SOCKETS 5
#define SPS_RESOLVER_CANDIDATE_COUNT 10
#define SPS_MAGIC_COUNT 4
#define SPS_CELL_MAGIC_0 float4(1, 0, 0, 1)
#define SPS_CELL_MAGIC_1 float4(0, 1, 0, 1)
#define SPS_CELL_MAGIC_2 float4(0, 0, 1, 1)
#define SPS_CELL_MAGIC_3 float4(1, 1, 0, 1)
#define SPS_CELL_MAGIC_INDEX_0 0
#define SPS_CELL_MAGIC_INDEX_1 (uint)SPS_CELL_WIDTH - 1
#define SPS_CELL_MAGIC_INDEX_2 ((uint)SPS_CELL_HEIGHT - 1) * (uint)SPS_CELL_WIDTH
#define SPS_CELL_MAGIC_INDEX_3 (uint)SPS_CELL_HEIGHT * (uint)SPS_CELL_WIDTH - 1
#define SPS_PRODUCT_SOCKET 1
#define SPS_PRODUCT_PLUG 2
#define SPS_VENDOR_SPS 1
#define SPS_VERSION_SPS 1
#define SPS_HEADER_VENDOR_INDEX 1
#define SPS_HEADER_PRODUCT_INDEX 2
#define SPS_HEADER_VERSION_INDEX 3
#define SPS_HEADER_UNIQUE_ID_INDEX 4
#define SPS_HEADER_PLAYER_ID_INDEX 5
#define SPS_HEADER_DEBUG_INDEX 6
#define SPS_HEADER_BOTTOM_ROW_BASE (((SPS_CELL_HEIGHT - 1) * SPS_CELL_WIDTH))
#define SPS_HEADER_BOTTOM_ROW_START (SPS_HEADER_BOTTOM_ROW_BASE + 1)
// Extends through +9: world xyz, forward xyz, up xyz, scale.
#define SPS_HEADER_WORLD_INDEX (SPS_HEADER_BOTTOM_ROW_START + 0)
#define SPS_HEADER_FORWARD_INDEX (SPS_HEADER_BOTTOM_ROW_START + 3)
#define SPS_HEADER_UP_INDEX (SPS_HEADER_BOTTOM_ROW_START + 6)
#define SPS_HEADER_SCALE_INDEX (SPS_HEADER_BOTTOM_ROW_START + 9)
#define SPS_SOCKET_PAYLOAD_FLAGS 0
#define SPS_SOCKET_PAYLOAD_NEXT_ID 1
#define SPS_SOCKET_PAYLOAD_TAG_START 2
#define SPS_SOCKET_PAYLOAD_TAG_COUNT 8
#define SPS_SOCKET_PAYLOAD_TAG_1 (SPS_SOCKET_PAYLOAD_TAG_START + 0)
#define SPS_SOCKET_PAYLOAD_TAG_2 (SPS_SOCKET_PAYLOAD_TAG_START + 1)
#define SPS_SOCKET_PAYLOAD_TAG_3 (SPS_SOCKET_PAYLOAD_TAG_START + 2)
#define SPS_SOCKET_PAYLOAD_TAG_4 (SPS_SOCKET_PAYLOAD_TAG_START + 3)
#define SPS_SOCKET_PAYLOAD_TAG_5 (SPS_SOCKET_PAYLOAD_TAG_START + 4)
#define SPS_SOCKET_PAYLOAD_TAG_6 (SPS_SOCKET_PAYLOAD_TAG_START + 5)
#define SPS_SOCKET_PAYLOAD_TAG_7 (SPS_SOCKET_PAYLOAD_TAG_START + 6)
#define SPS_SOCKET_PAYLOAD_TAG_8 (SPS_SOCKET_PAYLOAD_TAG_START + 7)

inline uint sps_decode_uint(float4 rgba) {
    uint b0 = (uint)round(sps_decode_channel(rgba.r) * 255.0);
    uint b1 = (uint)round(sps_decode_channel(rgba.g) * 255.0);
    uint b2 = (uint)round(sps_decode_channel(rgba.b) * 255.0);
    uint b3 = (uint)round(sps_decode_channel(rgba.a) * 255.0);
    return b0 | (b1 << 8) | (b2 << 16) | (b3 << 24);
}

inline uint sps_decode_uint_raw(float4 rgba) {
    uint b0 = (uint)round(rgba.r * 255.0);
    uint b1 = (uint)round(rgba.g * 255.0);
    uint b2 = (uint)round(rgba.b * 255.0);
    uint b3 = (uint)round(rgba.a * 255.0);
    return b0 | (b1 << 8) | (b2 << 16) | (b3 << 24);
}

struct SpsCell {
    SpsTexture raw;
    uint2 offset;
    uint2 size;

    uint2 get_pixel(uint index) {
        return uint2(index % size.x, index / size.x);
    }

    float4 read_rgba_raw(uint2 pixel) {
        return SPS_READ_TEX(raw, offset + pixel);
    }

    float4 read_rgba_raw(uint index) {
        return read_rgba_raw(get_pixel(index));
    }

    uint read_uint(uint index) {
        return sps_decode_uint(read_rgba_raw(index));
    }

    float read_float(uint index) {
        return asfloat(read_uint(index));
    }

    float3 read_float3(uint index) {
        return float3(
            read_float(index),
            read_float(index + 1),
            read_float(index + 2)
        );
    }
};

inline int sps_cell_grid_columns() {
    return max(1, (int)floor(_ScreenParams.x / SPS_CELL_WIDTH));
}

inline uint sps_socket_slot_count() {
    int cols = sps_cell_grid_columns();
    int rows = max(1, (int)floor(_ScreenParams.y / SPS_CELL_HEIGHT));
    return (uint)max(1, min(cols * rows - 1, SPS_SOCKET_MAX_SLOTS));
}

inline int2 sps_cell_grid_size_for_slot_count(uint slotCount) {
    int cols = sps_cell_grid_columns();
    uint safeCols = (uint)max(cols, 1);
    int rowsUsed = max(1, (int)((slotCount + safeCols - 1) / safeCols));
    return int2(cols, rowsUsed);
}

inline uint sps_cell_grid_cell_to_index(int2 cell, int2 grid) {
    return (uint)cell.x + (uint)cell.y * (uint)grid.x;
}

inline bool sps_cell_grid_cell_is_valid(int2 cell, uint slotCount) {
    int2 grid = sps_cell_grid_size_for_slot_count(slotCount);
    if (cell.x < 0 || cell.y < 0 || cell.x >= grid.x || cell.y >= grid.y) return false;
    return sps_cell_grid_cell_to_index(cell, grid) < slotCount;
}

inline int2 sps_get_cell_origin(int index) {
    int columns = sps_cell_grid_columns();
    uint screenIndex = index + 1;
    return int2(
        (screenIndex % columns) * SPS_CELL_WIDTH,
        (screenIndex / columns) * SPS_CELL_HEIGHT
    );
}

inline int2 sps_cell_origin_from_index(int index) {
    return index < 0 ? int2(0, 0) : sps_get_cell_origin(index);
}

inline SpsCell sps_get_cell_raw(SpsTexture tex, uint2 origin) {
    SpsCell cell;
    cell.raw = tex;
    cell.offset = origin;
    cell.size = uint2(SPS_CELL_WIDTH, SPS_CELL_HEIGHT);
    return cell;
}

inline SpsCell sps_get_cell(SpsTexture tex, int index) {
    return sps_get_cell_raw(tex, uint2(sps_cell_origin_from_index(index)));
}

inline SpsCell sps_get_slot_dictionary(SpsTexture tex) {
    return sps_get_cell(tex, -1);
}

inline bool sps_cell_dictionary_group_used(SpsCell dictionary, uint group) {
    return all(dictionary.read_rgba_raw(group) == SPS_CELL_DICTIONARY_MAGIC);
}

bool sps_try_get_slot_header_rgba(
    uint index,
    uint uniqueId,
    uint playerId,
    uint product,
    float3 world,
    float3 forward,
    float3 up,
    float scale,
    float4 debug,
    out float4 rgba
) {
    rgba = 0;
    if (index == SPS_CELL_MAGIC_INDEX_0) { rgba = SPS_CELL_MAGIC_0; return true; }
    if (index == SPS_CELL_MAGIC_INDEX_1) { rgba = SPS_CELL_MAGIC_1; return true; }
    if (index == SPS_CELL_MAGIC_INDEX_2) { rgba = SPS_CELL_MAGIC_2; return true; }
    if (index == SPS_CELL_MAGIC_INDEX_3) { rgba = SPS_CELL_MAGIC_3; return true; }
    float value = 0;
    switch (index) {
        case SPS_HEADER_VENDOR_INDEX:
            rgba = sps_encode_uint(SPS_VENDOR_SPS);
            return true;
        case SPS_HEADER_PRODUCT_INDEX:
            rgba = sps_encode_uint(product);
            return true;
        case SPS_HEADER_VERSION_INDEX:
            rgba = sps_encode_uint(SPS_VERSION_SPS);
            return true;
        case SPS_HEADER_UNIQUE_ID_INDEX:
            rgba = sps_encode_uint(uniqueId);
            return true;
        case SPS_HEADER_PLAYER_ID_INDEX:
            rgba = sps_encode_uint(playerId);
            return true;
        case SPS_HEADER_WORLD_INDEX + 0:
            value = world.x;
            break;
        case SPS_HEADER_WORLD_INDEX + 1:
            value = world.y;
            break;
        case SPS_HEADER_WORLD_INDEX + 2:
            value = world.z;
            break;
        case SPS_HEADER_FORWARD_INDEX + 0:
            value = forward.x;
            break;
        case SPS_HEADER_FORWARD_INDEX + 1:
            value = forward.y;
            break;
        case SPS_HEADER_FORWARD_INDEX + 2:
            value = forward.z;
            break;
        case SPS_HEADER_UP_INDEX + 0:
            value = up.x;
            break;
        case SPS_HEADER_UP_INDEX + 1:
            value = up.y;
            break;
        case SPS_HEADER_UP_INDEX + 2:
            value = up.z;
            break;
        case SPS_HEADER_SCALE_INDEX:
            value = scale;
            break;
        case SPS_HEADER_DEBUG_INDEX:
            rgba = debug;
            return true;
        default:
            return false;
    }
    rgba = sps_encode_float(value);
    return true;
}

inline float3 sps_cell_header_world(SpsCell cell) {
    return cell.read_float3(SPS_HEADER_WORLD_INDEX);
}

inline float3 sps_cell_header_forward(SpsCell cell) {
    return cell.read_float3(SPS_HEADER_FORWARD_INDEX);
}

inline float3 sps_cell_header_up(SpsCell cell) {
    return cell.read_float3(SPS_HEADER_UP_INDEX);
}

inline float sps_cell_header_scale(SpsCell cell) {
    return cell.read_float(SPS_HEADER_SCALE_INDEX);
}

inline uint sps_cell_header_unique_id(SpsCell cell) {
    return cell.read_uint(SPS_HEADER_UNIQUE_ID_INDEX);
}

inline uint sps_cell_header_player_id(SpsCell cell) {
    return cell.read_uint(SPS_HEADER_PLAYER_ID_INDEX);
}

inline bool sps_cell_check_magic(SpsTexture tex, uint2 cellOffset) {
    if (any(SPS_READ_TEX(tex, cellOffset + uint2(0, 0)) != SPS_CELL_MAGIC_0)) return false;
    if (any(SPS_READ_TEX(tex, cellOffset + uint2(SPS_CELL_WIDTH - 1, 0)) != SPS_CELL_MAGIC_1)) return false;
    if (any(SPS_READ_TEX(tex, cellOffset + uint2(0, SPS_CELL_HEIGHT - 1)) != SPS_CELL_MAGIC_2)) return false;
    if (any(SPS_READ_TEX(tex, cellOffset + uint2(SPS_CELL_WIDTH - 1, SPS_CELL_HEIGHT - 1)) != SPS_CELL_MAGIC_3)) return false;
    return true;
}

inline bool sps_cell_check_magic(SpsCell cell) {
    return sps_cell_check_magic(cell.raw, cell.offset);
}

uint sps_cell_pixel_index_from_payload_index(uint payloadIndex) {
    return payloadIndex + SPS_CELL_PAYLOAD_START;
}

inline bool sps_cell_payload_index_from_pixel_index(uint index, out uint payloadIndex) {
    payloadIndex = 0u;
    if (index < SPS_CELL_PAYLOAD_START) return false;
    payloadIndex = index - SPS_CELL_PAYLOAD_START;
    return true;
}

bool sps_try_find_cell(
    SpsTexture tex,
    uint slotSeed,
    uint id,
    uint playerId,
    uint product,
    out int outCellIndex,
    out SpsCell outCell
) {
    outCellIndex = -1;
    outCell = sps_get_cell_raw(tex, uint2(0, 0));
    [unroll]
    for (uint replica = 0u; replica < SPS_CELL_REPLICA_COUNT; replica++) {
        uint cellIndex = sps_hashed_screen_slot_index_from_id(slotSeed, replica);
        SpsCell cell = sps_get_cell(tex, cellIndex);
        if (!sps_cell_check_magic(cell)
            || cell.read_uint(SPS_HEADER_VENDOR_INDEX) != SPS_VENDOR_SPS
            || cell.read_uint(SPS_HEADER_PRODUCT_INDEX) != product) {
            continue;
        }
        if (sps_cell_header_unique_id(cell) != id) continue;
        if (sps_cell_header_player_id(cell) != playerId) continue;
        outCellIndex = (int)cellIndex;
        outCell = cell;
        return true;
    }
    return false;
}

#endif
