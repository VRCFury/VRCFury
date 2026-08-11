#ifndef SPS_INC_CELL_HASH
#define SPS_INC_CELL_HASH

inline uint sps_hash_mix(uint x) {
    x ^= x >> 16;
    x *= 0x7feb352du;
    x ^= x >> 15;
    x *= 0x846ca68bu;
    x ^= x >> 16;
    return x;
}

inline uint sps_hash_world(float3 worldPos, uint salt) {
    uint h = 2166136261u;
    h = (h ^ asuint(worldPos.x)) * 16777619u;
    h = (h ^ asuint(worldPos.y)) * 16777619u;
    h = (h ^ asuint(worldPos.z)) * 16777619u;
    h ^= salt * 2246822519u;
    return sps_hash_mix(h);
}

inline uint sps_hashed_index_from_uint(uint seed, uint replica, uint slotCount) {
    return sps_hash_mix(seed ^ sps_hash_mix(replica)) % max(slotCount, 1);
}

#endif
