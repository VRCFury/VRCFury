#ifndef SPS_INC_RESOLVER_READ
#define SPS_INC_RESOLVER_READ

#include "../common/sps_cell_hash.cginc"
#include "../common/sps_cell_layout.cginc"
#include "../common/sps_utils.cginc"
#include "sps_resolver_globals.cginc"
#include "sps_resolver_types.cginc"

uint sps_read_legacy_socket_flags(int lightIndex) {
    float range = 5.0 * rsqrt(unity_4LightAtten0[lightIndex]);
    int secondDecimal = round(fmod(range, 0.1) * 100.0);
    if (secondDecimal == 1) return SPS_SOCKET_FLAG_HOLE;
    if (secondDecimal == 2) return SPS_SOCKET_FLAG_DOUBLE_SIDED;
    return 0u;
}

CellData sps_read_legacy_cell(int cellIndex) {
    uint pairIndex = (uint)(-1 - cellIndex);
    uint rootIndex = pairIndex >> 2;
    uint frontIndex = pairIndex & 3u;

    CellData cellData;
    cellData.cellIndex = cellIndex;
    cellData.distanceSq = 0;
    cellData.world = float3(
        unity_4LightPosX0[rootIndex],
        unity_4LightPosY0[rootIndex],
        unity_4LightPosZ0[rootIndex]
    );
    cellData.normal = frontIndex == rootIndex
        ? sps_normalize(sps_object_origin_world() - cellData.world)
        : sps_normalize(float3(
            unity_4LightPosX0[frontIndex],
            unity_4LightPosY0[frontIndex],
            unity_4LightPosZ0[frontIndex]
        ) - cellData.world);
    cellData.up = sps_object_up_world();
    cellData.id = sps_hash_world(cellData.world, 1);
    cellData.playerId = 0u;
    return cellData;
}

CellData sps_read_positive_cell(SpsCell cell, int cellIndex) {
    CellData data;
    data.cellIndex = cellIndex;
    data.distanceSq = 0;
    data.world = sps_cell_header_world(cell);
    data.normal = sps_normalize(sps_cell_header_forward(cell));
    data.up = sps_normalize(sps_cell_header_up(cell));
    data.id = sps_cell_header_unique_id(cell);
    data.playerId = sps_cell_header_player_id(cell);
    return data;
}

CellData sps_make_empty_cell() {
    CellData data;
    data.cellIndex = -1;
    data.distanceSq = 0;
    data.world = 0;
    data.normal = 0;
    data.up = 0;
    data.id = 0u;
    data.playerId = 0u;
    return data;
}

inline SocketData sps_read_positive_socket(SpsCell cell) {
    SocketData data;
    data.flags = cell.read_uint(sps_cell_pixel_index_from_payload_index(SPS_SOCKET_PAYLOAD_FLAGS));
    data.nextId = cell.read_uint(sps_cell_pixel_index_from_payload_index(SPS_SOCKET_PAYLOAD_NEXT_ID));
    [unroll]
    for (uint tagIndex = 0u; tagIndex < SPS_SOCKET_PAYLOAD_TAG_COUNT; tagIndex++) {
        data.tags[tagIndex] = cell.read_uint(sps_cell_pixel_index_from_payload_index(SPS_SOCKET_PAYLOAD_TAG_START + tagIndex));
    }
    return data;
}

inline SocketData sps_make_empty_socket() {
    SocketData data;
    data.flags = 0u;
    data.nextId = 0u;
    [unroll]
    for (uint tagIndex = 0u; tagIndex < SPS_SOCKET_PAYLOAD_TAG_COUNT; tagIndex++) {
        data.tags[tagIndex] = 0u;
    }
    return data;
}

SocketData sps_read_legacy_socket(int cellIndex) {
    SocketData data = sps_make_empty_socket();
    data.flags = sps_read_legacy_socket_flags((int)(((uint)(-1 - cellIndex)) >> 2));
    data.tags[0] = 1337u;
    return data;
}

CellData sps_read_cell(SpsTexture tex, int cellIndex) {
    CellData data;
    if (cellIndex < 0) data = sps_read_legacy_cell(cellIndex);
    else data = sps_read_positive_cell(sps_get_cell(tex, (uint)cellIndex), cellIndex);
    return data;
}

SocketData sps_read_socket(SpsTexture tex, int cellIndex) {
    SocketData data;
    if (cellIndex < 0) data = sps_read_legacy_socket(cellIndex);
    else data = sps_read_positive_socket(sps_get_cell(tex, (uint)cellIndex));
    return data;
}

float3 sps_resolver_socket_target_world(CellData candidate, uint flags) {
    if (sps_has_flag(flags, SPS_SOCKET_FLAG_RADIUS_OFFSET)) {
        return candidate.world + candidate.up * sps_resolver_radius();
    }
    return candidate.world;
}

bool sps_try_find_socket_data(
    SpsTexture tex,
    uint id,
    uint playerId,
    out CellData cellData,
    out SocketData socketData
) {
    cellData = sps_make_empty_cell();
    socketData = sps_make_empty_socket();

    uint slotSeed = sps_id_hash();
    for (uint replica = 0u; replica < SPS_CELL_REPLICA_COUNT; replica++) {
        uint candidateSlotIndex = sps_hashed_screen_slot_index_from_id(slotSeed, replica);
        SpsCell cell = sps_get_cell(tex, candidateSlotIndex);
        if (!sps_cell_check_magic(cell)
            || cell.read_uint(SPS_HEADER_VENDOR_INDEX) != SPS_VENDOR_SPS
            || cell.read_uint(SPS_HEADER_PRODUCT_INDEX) != SPS_PRODUCT_SOCKET) {
            continue;
        }
        if (sps_cell_header_unique_id(cell) != id) continue;
        if (sps_cell_header_player_id(cell) != playerId) continue;
        cellData = sps_read_positive_cell(cell, (int)candidateSlotIndex);
        socketData = sps_read_positive_socket(cell);
        return true;
    }
    return false;
}

#endif
