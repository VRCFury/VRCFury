#ifndef SPS_INC_RESOLVER_CANDIDATES
#define SPS_INC_RESOLVER_CANDIDATES

#include "../common/sps_cell_hash.cginc"
#include "../common/sps_types.cginc"
#include "../common/sps_utils.cginc"
#include "sps_resolver_read.cginc"
#include "sps_resolver_light.cginc"
#include "sps_resolver_types.cginc"

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

void sps_resolver_parse_tag_rules(
    out uint includeTags[4],
    out uint includeFlags[4],
    out uint excludeTags[4],
    out uint excludeFlags[4]
) {
    float includeTagValues[4] = { _SPS_TagInclude1, _SPS_TagInclude2, _SPS_TagInclude3, _SPS_TagInclude4 };
    float excludeTagValues[4] = { _SPS_TagExclude1, _SPS_TagExclude2, _SPS_TagExclude3, _SPS_TagExclude4 };
    float includeSelfValues[4] = { _SPS_TagInclude1Self, _SPS_TagInclude2Self, _SPS_TagInclude3Self, _SPS_TagInclude4Self };
    float includeOtherValues[4] = { _SPS_TagInclude1Others, _SPS_TagInclude2Others, _SPS_TagInclude3Others, _SPS_TagInclude4Others };
    float excludeSelfValues[4] = { _SPS_TagExclude1Self, _SPS_TagExclude2Self, _SPS_TagExclude3Self, _SPS_TagExclude4Self };
    float excludeOtherValues[4] = { _SPS_TagExclude1Others, _SPS_TagExclude2Others, _SPS_TagExclude3Others, _SPS_TagExclude4Others };

    [unroll]
    for (uint i = 0u; i < 4u; i++) {
        includeTags[i] = sps_to_uint(includeTagValues[i]);
        excludeTags[i] = sps_to_uint(excludeTagValues[i]);

        includeFlags[i] = 0u;
        excludeFlags[i] = 0u;

        if (sps_to_bool(includeSelfValues[i])) includeFlags[i] |= SPS_TAG_MATCH_SELF;
        if (sps_to_bool(includeOtherValues[i])) includeFlags[i] |= SPS_TAG_MATCH_OTHERS;
        if (sps_to_bool(excludeSelfValues[i])) excludeFlags[i] |= SPS_TAG_MATCH_SELF;
        if (sps_to_bool(excludeOtherValues[i])) excludeFlags[i] |= SPS_TAG_MATCH_OTHERS;
    }
}

bool sps_tag_rule_applies_to_candidate(uint flags, uint resolverPlayerId, uint candidatePlayerId) {
    bool samePlayer = candidatePlayerId == resolverPlayerId;
    if (samePlayer && sps_has_flag(flags, SPS_TAG_MATCH_SELF)) return true;
    if (!samePlayer && sps_has_flag(flags, SPS_TAG_MATCH_OTHERS)) return true;
    return false;
}

bool sps_candidate_has_tag(SocketData socketData, uint tag) {
    if (tag == 0u) return false;

    [unroll]
    for (uint tagIndex = 0u; tagIndex < SPS_SOCKET_PAYLOAD_TAG_COUNT; tagIndex++) {
        if (socketData.tags[tagIndex] == tag) {
            return true;
        }
    }
    return false;
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

void sps_filter_cells(
    SpsTexture tex,
    inout int candidateCount,
    inout CellData cells[SPS_CANDIDATE_COUNT],
    uint resolverPlayerId,
    uint includeTags[4],
    uint includeFlags[4],
    uint excludeTags[4],
    uint excludeFlags[4]
) {
    int writeIndex = 0;
    [loop]
    for (int candidateIndex = 0; candidateIndex < SPS_CANDIDATE_COUNT; candidateIndex++) {
        if (candidateIndex >= candidateCount) break;
        CellData candidate = cells[candidateIndex];
        uint candidatePlayerId = candidate.playerId;
        SocketData socketData = sps_read_socket(tex, candidate.cellIndex);

        bool matchedInclude = false;
        [loop]
        for (uint slot = 0u; slot < 4u; slot++) {
            uint includeTag = includeTags[slot];
            if (includeTag == 0u) continue;
            if (!sps_tag_rule_applies_to_candidate(includeFlags[slot], resolverPlayerId, candidatePlayerId)) continue;

            if (sps_candidate_has_tag(socketData, includeTag)) {
                matchedInclude = true;
                break;
            }
        }
        if (!matchedInclude) continue;

        bool excluded = false;
        [loop]
        for (uint slot2 = 0u; slot2 < 4u; slot2++) {
            uint excludeTag = excludeTags[slot2];
            if (excludeTag == 0u) continue;
            if (!sps_tag_rule_applies_to_candidate(excludeFlags[slot2], resolverPlayerId, candidatePlayerId)) continue;

            excluded = sps_candidate_has_tag(socketData, excludeTag);
            if (excluded) break;
        }
        if (excluded) continue;

        cells[writeIndex] = candidate;
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

    uint lightFlags[4];
    bool lightValid[4];
    float3 lightWorld[4];
    for (int i = 0; i < 4; i++) {
        lightValid[i] = sps_light_parse(i, lightWorld[i], lightFlags[i]);
    }

    bool rootUsed[4];
    for (int ri = 0; ri < 4; ri++) rootUsed[ri] = false;

    [loop]
    for (int rank = 0; rank < SPS_LEGACY_SLOT_COUNT; rank++) {
        float bestDistanceSq = 0;
        int rootIndex = -1;
        for (int i = 0; i < 4; i++) {
            if (rootUsed[i]) continue;
            if (!lightValid[i] || lightFlags[i] == SPS_LEGACY_LIGHT_FRONT) continue;
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
        for (int li = 0; li < 4; li++) {
            if (!lightValid[li]) continue;
            float3 frontOffset = lightWorld[li] - lightWorld[rootIndex];
            float distanceSq = sps_length_sq(frontOffset);
            if (lightFlags[li] == SPS_LEGACY_LIGHT_FRONT && distanceSq < bestFrontDistanceSq) {
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

    CellData cellData = sps_read_positive_cell(cell, (int)cellIndex);
    [unroll]
    for (int candidateIndex = 0; candidateIndex < candidateCount; candidateIndex++) {
        if (candidates[candidateIndex].id == cellData.id
            && candidates[candidateIndex].playerId == cellData.playerId) return;
    }

    Candidate candidate;
    candidate.cellIndex = (int)cellIndex;
    candidate.distanceSq = distanceSq;
    candidate.id = cellData.id;
    candidate.playerId = cellData.playerId;
    sps_insert_candidate(candidate, candidateCount, candidates, debugFlags);
}

void sps_collect_candidates(
    SpsTexture tex,
    float3 plugWorld,
    out int candidateCount,
    out CellData cells[SPS_CANDIDATE_COUNT],
    inout uint debugFlags,
    uint productFilter
) {
    Candidate rawCandidates[SPS_CANDIDATE_COUNT];
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
    sps_read_candidates(tex, rawCandidates, candidateCount, cells);
}

#endif
