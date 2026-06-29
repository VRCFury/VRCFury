#ifndef SPS_INC_DICTIONARY
#define SPS_INC_DICTIONARY

#include "sps_id.cginc"
#include "sps_cell_hash.cginc"
#include "sps_cell_geom.cginc"
#include "sps_cell_layout.cginc"

#define SPS_DICTIONARY_INDEX (-1)

inline float4 sps_dictionary_vert(int vertexIndex) {
    return sps_cell_geom_vertex(SPS_DICTIONARY_INDEX, vertexIndex);
}

inline bool sps_dictionary_group_used(uint group) {
    uint slotSeed = sps_id_hash();
    [unroll]
    for (uint replica = 0u; replica < SPS_CELL_REPLICA_COUNT; replica++) {
        uint slotIndex = sps_hashed_screen_slot_index_from_id(slotSeed, replica);
        if (group == slotIndex / SPS_CELL_DICTIONARY_GROUP_SIZE) {
            return true;
        }
    }
    return false;
}

inline bool sps_dictionary_frag(
    int cellIndex,
    uint pixelIndex,
    out float4 rgba
) {
    rgba = 0;
    if (cellIndex != SPS_DICTIONARY_INDEX) return false;
    uint group = pixelIndex;
    if (!sps_dictionary_group_used(group)) clip(-1);
    rgba = SPS_CELL_DICTIONARY_MAGIC;
    return true;
}

#endif
