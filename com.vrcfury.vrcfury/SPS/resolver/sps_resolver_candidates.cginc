#ifndef SPS_INC_RESOLVER_CANDIDATES
#define SPS_INC_RESOLVER_CANDIDATES

#include "../common/sps_cell_hash.cginc"
#include "../common/sps_types.cginc"
#include "../common/sps_utils.cginc"
#include "sps_resolver_read.cginc"
#include "sps_resolver_light.cginc"
#include "sps_resolver_types.cginc"

// Saves ~0.5s vs 4-element include/exclude tag and flag arrays
struct SpsTagRules {
    uint includeTag0;
    uint includeTag1;
    uint includeTag2;
    uint includeTag3;
    bool includeSelf0;
    bool includeOthers0;
    bool includeSelf1;
    bool includeOthers1;
    bool includeSelf2;
    bool includeOthers2;
    bool includeSelf3;
    bool includeOthers3;
    uint excludeTag0;
    uint excludeTag1;
    uint excludeTag2;
    uint excludeTag3;
    bool excludeSelf0;
    bool excludeOthers0;
    bool excludeSelf1;
    bool excludeOthers1;
    bool excludeSelf2;
    bool excludeOthers2;
    bool excludeSelf3;
    bool excludeOthers3;
};

inline void sps_insert_candidate(
    Candidate candidate,
    inout int candidateCount,
    inout Candidate candidates[SPS_CANDIDATE_COUNT],
    inout uint debugFlags
) {
    if (candidateCount >= SPS_CANDIDATE_COUNT
        && candidate.distanceSq >= candidates[candidateCount - 1].distanceSq) {
        SPS_DEBUG_SET(debugFlags, SPS_DEBUG_FLAG_DISTANCE_CULL);
        return;
    }

    int insertIndex;
    if (candidateCount < SPS_CANDIDATE_COUNT) {
        insertIndex = candidateCount++;
    } else {
        insertIndex = SPS_CANDIDATE_COUNT - 1;
    }

    [loop]
    while (insertIndex > 0
        && candidate.distanceSq < candidates[insertIndex - 1].distanceSq) {
        candidates[insertIndex] = candidates[insertIndex - 1];
        insertIndex--;
    }
    candidates[insertIndex] = candidate;
}

void sps_resolver_parse_tag_rules(out SpsTagRules rules) {
    rules.includeTag0 = _SPS_TagInclude1;
    rules.includeTag1 = _SPS_TagInclude2;
    rules.includeTag2 = _SPS_TagInclude3;
    rules.includeTag3 = _SPS_TagInclude4;
    rules.includeSelf0 = sps_to_bool(_SPS_TagInclude1Self);
    rules.includeOthers0 = sps_to_bool(_SPS_TagInclude1Others);
    rules.includeSelf1 = sps_to_bool(_SPS_TagInclude2Self);
    rules.includeOthers1 = sps_to_bool(_SPS_TagInclude2Others);
    rules.includeSelf2 = sps_to_bool(_SPS_TagInclude3Self);
    rules.includeOthers2 = sps_to_bool(_SPS_TagInclude3Others);
    rules.includeSelf3 = sps_to_bool(_SPS_TagInclude4Self);
    rules.includeOthers3 = sps_to_bool(_SPS_TagInclude4Others);

    rules.excludeTag0 = _SPS_TagExclude1;
    rules.excludeTag1 = _SPS_TagExclude2;
    rules.excludeTag2 = _SPS_TagExclude3;
    rules.excludeTag3 = _SPS_TagExclude4;
    rules.excludeSelf0 = sps_to_bool(_SPS_TagExclude1Self);
    rules.excludeOthers0 = sps_to_bool(_SPS_TagExclude1Others);
    rules.excludeSelf1 = sps_to_bool(_SPS_TagExclude2Self);
    rules.excludeOthers1 = sps_to_bool(_SPS_TagExclude2Others);
    rules.excludeSelf2 = sps_to_bool(_SPS_TagExclude3Self);
    rules.excludeOthers2 = sps_to_bool(_SPS_TagExclude3Others);
    rules.excludeSelf3 = sps_to_bool(_SPS_TagExclude4Self);
    rules.excludeOthers3 = sps_to_bool(_SPS_TagExclude4Others);
}

void sps_read_candidates(
    SpsTexture tex,
    Candidate candidates[SPS_CANDIDATE_COUNT],
    int candidateCount,
    out CellData cells[SPS_CANDIDATE_COUNT]
) {
    for (int candidateIndex = 0; candidateIndex < SPS_CANDIDATE_COUNT; candidateIndex++) {
        if (candidateIndex >= candidateCount) break;
        Candidate candidate = candidates[candidateIndex];
        CellData cellData = sps_read_cell(tex, candidate.cellIndex);
        cellData.distanceSq = candidate.distanceSq;

        cells[candidateIndex] = cellData;
    }
}

// Saves ~0.5s vs filtering after reading full CellData
void sps_filter_candidates(
    SpsTexture tex,
    inout int candidateCount,
    inout Candidate candidates[SPS_CANDIDATE_COUNT],
    uint resolverPlayerId,
    SpsTagRules rules
) {
    int writeIndex = 0;
    [loop]
    for (int candidateIndex = 0; candidateIndex < SPS_CANDIDATE_COUNT; candidateIndex++) {
        if (candidateIndex >= candidateCount) break;

        Candidate candidate = candidates[candidateIndex];
        if (candidate.cellIndex < 0) {
            // Lights are always accepted
            candidates[writeIndex] = candidate;
            writeIndex++;
            continue;
        }

        uint candidatePlayerId = candidate.playerId;
        // Saves ~1.0s vs materializing socket tag data
        SpsCell socketCell = sps_get_cell(tex, (uint)candidate.cellIndex);

        // Saves ~0.5s vs checking rule applicability inside the tag loop
        bool samePlayer = candidatePlayerId == resolverPlayerId;
        uint includeTag0 = (samePlayer ? rules.includeSelf0 : rules.includeOthers0) ? rules.includeTag0 : 0u;
        uint includeTag1 = (samePlayer ? rules.includeSelf1 : rules.includeOthers1) ? rules.includeTag1 : 0u;
        uint includeTag2 = (samePlayer ? rules.includeSelf2 : rules.includeOthers2) ? rules.includeTag2 : 0u;
        uint includeTag3 = (samePlayer ? rules.includeSelf3 : rules.includeOthers3) ? rules.includeTag3 : 0u;
        uint excludeTag0 = (samePlayer ? rules.excludeSelf0 : rules.excludeOthers0) ? rules.excludeTag0 : 0u;
        uint excludeTag1 = (samePlayer ? rules.excludeSelf1 : rules.excludeOthers1) ? rules.excludeTag1 : 0u;
        uint excludeTag2 = (samePlayer ? rules.excludeSelf2 : rules.excludeOthers2) ? rules.excludeTag2 : 0u;
        uint excludeTag3 = (samePlayer ? rules.excludeSelf3 : rules.excludeOthers3) ? rules.excludeTag3 : 0u;

        bool matchedInclude = false;
        bool excluded = false;
        [loop]
        for (uint tagIndex = 0u; tagIndex < SPS_SOCKET_PAYLOAD_TAG_COUNT; tagIndex++) {
            uint tag = socketCell.read_uint(sps_cell_pixel_index_from_payload_index(SPS_SOCKET_PAYLOAD_TAG_START + tagIndex));
            if (!matchedInclude) {
                matchedInclude =
                    (includeTag0 != 0u && tag == includeTag0)
                    || (includeTag1 != 0u && tag == includeTag1)
                    || (includeTag2 != 0u && tag == includeTag2)
                    || (includeTag3 != 0u && tag == includeTag3);
            }
            excluded =
                (excludeTag0 != 0u && tag == excludeTag0)
                || (excludeTag1 != 0u && tag == excludeTag1)
                || (excludeTag2 != 0u && tag == excludeTag2)
                || (excludeTag3 != 0u && tag == excludeTag3);
            if (excluded) break;
        }
        if (!matchedInclude) continue;
        if (excluded) continue;

        candidates[writeIndex] = candidate;
        writeIndex++;
    }
    candidateCount = writeIndex;
}

void sps_collect_legacy_candidates(
    float3 plugWorld,
    inout int candidateCount,
    inout Candidate candidates[SPS_CANDIDATE_COUNT],
    inout uint debugFlags
) {
    if (!sps_to_bool(_SPS_Legacy)) return;

    uint lightType[4];
    float3 lightWorld[4];
    // Saves ~0.6s vs unrolled legacy light scans
    [loop]
    for (uint i = 0; i < 4; i++) {
        lightType[i] = sps_light_type(i);
        lightWorld[i] = sps_light_world(i);
    }

    bool rootUsed[4];
    [loop]
    for (int ri = 0; ri < 4; ri++) rootUsed[ri] = false;

    [loop]
    for (int rank = 0; rank < SPS_LEGACY_SLOT_COUNT; rank++) {
        float bestDistanceSq = 0;
        int rootIndex = -1;
        [loop]
        for (int i = 0; i < 4; i++) {
            if (rootUsed[i]) continue;
            if (lightType[i] != SPS_LEGACY_LIGHT_HOLE && lightType[i] != SPS_LEGACY_LIGHT_RING) continue;
            float3 offset = lightWorld[i] - plugWorld;
            float distanceSq = sps_length_sq(offset);
            if (rootIndex < 0 || distanceSq < bestDistanceSq) {
                bestDistanceSq = distanceSq;
                rootIndex = i;
            }
        }
        if (rootIndex < 0) break;
        rootUsed[rootIndex] = true;

        int frontIndex = 0;
        bool frontFound = false;
        float bestFrontDistanceSq = 0.01;
        [loop]
        for (int li = 0; li < 4; li++) {
            if (lightType[li] != SPS_LEGACY_LIGHT_FRONT) continue;
            float3 frontOffset = lightWorld[li] - lightWorld[rootIndex];
            float distanceSq = sps_length_sq(frontOffset);
            if (distanceSq < bestFrontDistanceSq) {
                frontFound = true;
                frontIndex = li;
                bestFrontDistanceSq = distanceSq;
            }
        }
        float3 frontOffset = lightWorld[frontIndex] - lightWorld[rootIndex];
        frontFound = frontFound && sps_length_sq(frontOffset) >= 0.0000000025;
        if (!frontFound) frontIndex = rootIndex;

        Candidate candidate;
        candidate.cellIndex = -1 - (rootIndex * 4 + frontIndex);
        candidate.distanceSq = sps_length_sq(lightWorld[rootIndex] - plugWorld);
        candidate.id = sps_hash_world(lightWorld[rootIndex], 1);
        candidate.playerId = 0u;
        sps_insert_candidate(candidate, candidateCount, candidates, debugFlags);
    }
}

inline void sps_collect_cell(
    float3 plugWorld,
    inout int candidateCount,
    inout Candidate candidates[SPS_CANDIDATE_COUNT],
    inout uint debugFlags,
    SpsCell cell,
    uint cellIndex
) {
    SPS_DEBUG_SET(debugFlags, SPS_DEBUG_FLAG_HEADER_ENTRY);

    float3 world = sps_cell_header_world(cell);
    float3 offset = world - plugWorld;
    float distanceSq = sps_length_sq(offset);
    if (candidateCount >= SPS_CANDIDATE_COUNT
        && distanceSq >= candidates[candidateCount - 1].distanceSq) return;

    // Saves ~0.4s vs building full CellData just to read id/playerId
    uint cellId = sps_cell_header_unique_id(cell);
    uint cellPlayerId = sps_cell_header_player_id(cell);
    [unroll]
    for (int candidateIndex = 0; candidateIndex < candidateCount; candidateIndex++) {
        if (candidates[candidateIndex].id == cellId
            && candidates[candidateIndex].playerId == cellPlayerId) return;
    }

    Candidate candidate;
    candidate.cellIndex = (int)cellIndex;
    candidate.distanceSq = distanceSq;
    candidate.id = cellId;
    candidate.playerId = cellPlayerId;
    sps_insert_candidate(candidate, candidateCount, candidates, debugFlags);
}

void sps_collect_raw_candidates(
    SpsTexture tex,
    float3 plugWorld,
    out int candidateCount,
    out Candidate rawCandidates[SPS_CANDIDATE_COUNT],
    inout uint debugFlags,
    uint productFilter
) {
    uint slotCount = sps_socket_slot_count();
    uint columns = (uint)sps_cell_grid_columns();
    candidateCount = 0;
    uint groupCount = min(
        SPS_CELL_DICTIONARY_GROUP_COUNT,
        (slotCount + SPS_CELL_DICTIONARY_GROUP_SIZE - 1u) / SPS_CELL_DICTIONARY_GROUP_SIZE
    );

    // This is an EXTREMELY hot loop, reduce any unnessicary calls and excess logic during filtering
    [loop]
    for (uint group = 0u; group < groupCount; group++) {
        bool groupUsed = all(SPS_READ_TEX(
            tex,
            uint2(group % SPS_CELL_DICTIONARY_GROUP_SIZE, group / SPS_CELL_DICTIONARY_GROUP_SIZE)
        ) == SPS_CELL_DICTIONARY_MAGIC);
        if (!groupUsed) continue;
        uint startIndex = group * SPS_CELL_DICTIONARY_GROUP_SIZE;
        [loop]
        for (uint groupMember = 0u; groupMember < SPS_CELL_DICTIONARY_GROUP_SIZE; groupMember++) {
            uint cellIndex = startIndex + groupMember;
            if (cellIndex >= slotCount) continue;
            uint physicalIndex = cellIndex + 1u;
            uint2 physicalCell = uint2(physicalIndex % columns, physicalIndex / columns);
            uint2 cellOffset = physicalCell * uint2(SPS_CELL_WIDTH, SPS_CELL_HEIGHT);
            if (!sps_cell_check_magic(tex, cellOffset)) continue;
            SpsCell cell = sps_get_cell_raw(tex, cellOffset);
            cell.size = uint2(SPS_CELL_WIDTH, SPS_CELL_HEIGHT);
            if (cell.read_uint(SPS_HEADER_VENDOR_INDEX) != SPS_VENDOR_SPS) continue;
            if (productFilter > 0u && cell.read_uint(SPS_HEADER_PRODUCT_INDEX) != productFilter) continue;
            sps_collect_cell(plugWorld, candidateCount, rawCandidates, debugFlags, cell, cellIndex);
        }
    }

    sps_collect_legacy_candidates(plugWorld, candidateCount, rawCandidates, debugFlags);
}

#endif
