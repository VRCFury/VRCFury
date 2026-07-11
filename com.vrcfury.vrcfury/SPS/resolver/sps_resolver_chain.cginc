#ifndef SPS_INC_RESOLVER_CHAIN
#define SPS_INC_RESOLVER_CHAIN

#include "../common/sps_types.cginc"
#include "../common/sps_utils.cginc"
#include "sps_resolver_read.cginc"
#include "sps_resolver_socket.cginc"
#include "sps_resolver_types.cginc"

inline ChainEntry sps_make_chain_entry(
    int cellIndex,
    bool flipped,
    bool isGuideTarget,
    float3 world,
    float3 traversalNormal,
    float3 up,
    uint flags,
    uint id,
    uint nextId,
    uint playerId
) {
    ChainEntry entry = (ChainEntry)0;
    entry.cellIndex = cellIndex;
    entry.flipped = flipped;
    entry.isGuideTarget = isGuideTarget;
    entry.world = world;
    entry.traversalNormal = traversalNormal;
    entry.up = up;
    entry.flags = flags;
    entry.id = id;
    entry.nextId = nextId;
    entry.playerId = playerId;
    return entry;
}

// Saves ~0.6s vs multi-status duplicate chain scan
bool sps_chain_contains_id(
    ChainEntry chain[SPS_CHAIN_MAX_SOCKETS],
    int chainCount,
    uint targetId,
    uint targetPlayerId
) {
    [loop]
    for (int priorIndex = 0; priorIndex < SPS_CHAIN_MAX_SOCKETS; priorIndex++) {
        if (priorIndex >= chainCount) break;
        if (chain[priorIndex].id == targetId
            && chain[priorIndex].playerId == targetPlayerId) {
            return true;
        }
    }
    return false;
}

int sps_build_chain(
    SpsTexture socketTex,
    float3 plugWorld,
    float3 plugForward,
    float3 plugUp,
    int candidateCount,
    CellData candidates[SPS_CANDIDATE_COUNT],
    out ChainEntry chain[SPS_CHAIN_MAX_SOCKETS],
    inout uint debugFlags
) {
    ChainEntry plugEntry = sps_make_chain_entry(
        SPS_CHAIN_REF_INVALID,
        false,
        false,
        plugWorld,
        -plugForward,
        plugUp,
        0u,
        0u,
        0u,
        0u
    );

    int chainCount = 0;
    ChainEntry previous = plugEntry;

    for (int chainIndex = 0; chainIndex < SPS_CHAIN_MAX_SOCKETS; chainIndex++) {
        // Saves ~0.8s vs normalizing sourceForward per socket check
        float3 sourceForward = sps_normalize(-previous.traversalNormal);

        if (previous.nextId > 0) {
            int linkedCellIndex = -1;
            if (!sps_try_find_cell(
                socketTex,
                sps_hash_id(previous.nextId, previous.playerId),
                previous.nextId,
                previous.playerId,
                SPS_PRODUCT_SOCKET,
                linkedCellIndex
            )) {
                break;
            }
            SpsCell linkedCell = sps_get_cell(socketTex, linkedCellIndex);
            CellData linkedCellData = sps_read_positive_cell(linkedCell, linkedCellIndex);
            uint linkedSocketFlags = linkedCell.read_uint(sps_cell_pixel_index_from_payload_index(SPS_SOCKET_PAYLOAD_FLAGS));
            uint linkedSocketNextId = linkedCell.read_uint(sps_cell_pixel_index_from_payload_index(SPS_SOCKET_PAYLOAD_NEXT_ID));

            float unusedDistanceSq;
            float3 traversalNormal;
            float3 traversalUp;
            uint rejectionFlags;
            bool candidateEligible = sps_resolver_check_socket(
                linkedCellData,
                linkedSocketFlags,
                previous.world,
                sourceForward,
                true,
                false,
                unusedDistanceSq,
                traversalNormal,
                traversalUp,
                rejectionFlags
            );
            if (!candidateEligible) {
                SPS_DEBUG_SET(debugFlags, SPS_DEBUG_FLAG_ELIGIBILITY);
                SPS_DEBUG_SET(debugFlags, rejectionFlags);
                break;
            }

            float3 linkedTargetWorld = sps_resolver_socket_target_world(linkedCellData, linkedSocketFlags);
            chain[chainIndex] = sps_make_chain_entry(
                linkedCellData.cellIndex,
                dot(traversalNormal, linkedCellData.normal) < 0,
                true,
                linkedTargetWorld,
                traversalNormal,
                traversalUp,
                linkedSocketFlags,
                linkedCellData.id,
                linkedSocketNextId,
                linkedCellData.playerId
            );
            previous = chain[chainCount];
            chainCount++;
            continue;
        }

        if (sps_has_flag(previous.flags, SPS_SOCKET_FLAG_HOLE)) {
            break;
        }

        float bestDistanceSq = 0;
        int bestCandidateIndex = -1;
        float3 bestTraversalNormal = float3(0, 0, 1);
        float3 bestTraversalUp = float3(0, 1, 0);
        uint bestSocketFlags = 0u;
        uint bestSocketNextId = 0u;
        for (int candidateIndex = 0; candidateIndex < SPS_CANDIDATE_COUNT; candidateIndex++) {
            if (candidateIndex >= candidateCount) break;
            CellData candidate = candidates[candidateIndex];
            if (sps_chain_contains_id(
                chain,
                chainIndex,
                candidate.id,
                candidate.playerId
            )) {
                SPS_DEBUG_SET(debugFlags, SPS_DEBUG_FLAG_DUPLICATE_REJECT);
                continue;
            }
            uint candidateSocketFlags = 0u;
            uint candidateSocketNextId = 0u;
            if (candidate.cellIndex < 0) {
                uint type = sps_light_type((int)(((uint)(-1 - candidate.cellIndex)) >> 2));
                if (type == SPS_LEGACY_LIGHT_HOLE) candidateSocketFlags = SPS_SOCKET_FLAG_HOLE;
                else if (type == SPS_LEGACY_LIGHT_RING) candidateSocketFlags = SPS_SOCKET_FLAG_DOUBLE_SIDED;
            } else {
                SpsCell candidateCell = sps_get_cell(socketTex, (uint)candidate.cellIndex);
                candidateSocketFlags = candidateCell.read_uint(sps_cell_pixel_index_from_payload_index(SPS_SOCKET_PAYLOAD_FLAGS));
                candidateSocketNextId = candidateCell.read_uint(sps_cell_pixel_index_from_payload_index(SPS_SOCKET_PAYLOAD_NEXT_ID));
            }

            float distanceSq;
            float3 traversalNormal;
            float3 traversalUp;
            uint rejectionFlags;
            bool candidateEligible = sps_resolver_check_socket(
                candidate,
                candidateSocketFlags,
                previous.world,
                sourceForward,
                false,
                chainIndex == 0,
                distanceSq,
                traversalNormal,
                traversalUp,
                rejectionFlags
            );
            if (!candidateEligible) {
                SPS_DEBUG_SET(debugFlags, SPS_DEBUG_FLAG_ELIGIBILITY);
                SPS_DEBUG_SET(debugFlags, rejectionFlags);
                continue;
            }

            if (bestCandidateIndex < 0 || distanceSq < bestDistanceSq) {
                bestCandidateIndex = candidateIndex;
                bestDistanceSq = distanceSq;
                bestTraversalNormal = traversalNormal;
                bestTraversalUp = traversalUp;
                bestSocketFlags = candidateSocketFlags;
                bestSocketNextId = candidateSocketNextId;
            }
        }
        if (bestCandidateIndex < 0) break;

        CellData best = candidates[bestCandidateIndex];
        float3 bestWorld = sps_resolver_socket_target_world(best, bestSocketFlags);
        chain[chainCount] = sps_make_chain_entry(
            best.cellIndex,
            dot(bestTraversalNormal, best.normal) < 0,
            false,
            bestWorld,
            bestTraversalNormal,
            bestTraversalUp,
            bestSocketFlags,
            best.id,
            bestSocketNextId,
            best.playerId
        );
        previous = chain[chainCount];
        chainCount++;
    }
    return chainCount;
}

#endif
