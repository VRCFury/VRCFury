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

bool sps_chain_contains_id(
    ChainEntry chain[SPS_CHAIN_MAX_SOCKETS],
    int chainCount,
    uint targetId,
    uint targetPlayerId
) {
    [unroll]
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
    bool used[SPS_CANDIDATE_COUNT];
    [unroll]
    for (int usedIndex = 0; usedIndex < SPS_CANDIDATE_COUNT; usedIndex++) {
        used[usedIndex] = false;
    }

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
        float3 sourceForward = -previous.traversalNormal;

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
            SocketData linkedSocketData = sps_read_positive_socket(linkedCell);

            float unusedDistanceSq;
            float3 traversalNormal;
            float3 traversalUp;
            uint rejectionFlags;
            bool candidateEligible = sps_resolver_check_socket(
                linkedCellData,
                linkedSocketData,
                previous.world,
                sourceForward,
                true,
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

            float3 linkedTargetWorld = sps_resolver_socket_target_world(linkedCellData, linkedSocketData.flags);
            chain[chainIndex] = sps_make_chain_entry(
                linkedCellData.cellIndex,
                dot(traversalNormal, linkedCellData.normal) < 0,
                true,
                linkedTargetWorld,
                traversalNormal,
                traversalUp,
                linkedSocketData.flags,
                linkedCellData.id,
                linkedSocketData.nextId,
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
        SocketData bestSocketData = sps_make_empty_socket();
        for (int candidateIndex = 0; candidateIndex < SPS_CANDIDATE_COUNT; candidateIndex++) {
            if (candidateIndex >= candidateCount) break;
            if (used[candidateIndex]) continue;
            CellData candidate = candidates[candidateIndex];
            SocketData candidateSocketData = sps_read_socket(socketTex, candidate.cellIndex);
            if (sps_chain_contains_id(chain, chainIndex, candidate.id, candidate.playerId)) {
                SPS_DEBUG_SET(debugFlags, SPS_DEBUG_FLAG_DUPLICATE_REJECT);
                continue;
            }

            float distanceSq;
            float3 traversalNormal;
            float3 traversalUp;
            uint rejectionFlags;
            bool candidateEligible = sps_resolver_check_socket(
                candidate,
                candidateSocketData,
                previous.world,
                sourceForward,
                false,
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
                bestSocketData = candidateSocketData;
            }
        }
        if (bestCandidateIndex < 0) break;

        used[bestCandidateIndex] = true;
        CellData best = candidates[bestCandidateIndex];
        float3 bestWorld = sps_resolver_socket_target_world(best, bestSocketData.flags);
        chain[chainCount] = sps_make_chain_entry(
            best.cellIndex,
            dot(bestTraversalNormal, best.normal) < 0,
            false,
            bestWorld,
            bestTraversalNormal,
            bestTraversalUp,
            bestSocketData.flags,
            best.id,
            bestSocketData.nextId,
            best.playerId
        );
        previous = chain[chainCount];
        chainCount++;
    }
    return chainCount;
}

#endif
