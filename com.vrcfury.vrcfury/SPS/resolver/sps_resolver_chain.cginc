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
    float applyLerp,
    float3 world,
    float3 traversalNormal,
    uint flags,
    uint id,
    uint nextId,
    uint playerId
) {
    ChainEntry entry = (ChainEntry)0;
    entry.cellIndex = cellIndex;
    entry.flipped = flipped;
    entry.applyLerp = applyLerp;
    entry.world = world;
    entry.traversalNormal = sps_normalize(traversalNormal);
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

float sps_evaluate_chain_candidate(
    CellData candidate,
    SocketData socketData,
    ChainEntry previous,
    float3 sourceForward,
    out float distanceSq,
    out float3 traversalNormal,
    out uint rejectionFlags
) {
    float3 entryOffset = sps_resolver_socket_target_world(candidate, socketData.flags) - previous.world;
    distanceSq = sps_length_sq(entryOffset);
    traversalNormal = sps_normalize(candidate.normal);
    rejectionFlags = 0u;
    // if (sps_has_flag(previous.flags, SPS_SOCKET_FLAG_PORTAL)) {
    //     return 1;
    // }
    return sps_prepare_and_evaluate_socket(
        entryOffset,
        sourceForward,
        distanceSq,
        socketData.flags,
        traversalNormal,
        rejectionFlags
    );
}

int sps_build_chain(
    SpsTexture socketTex,
    float3 plugWorld,
    float3 plugForward,
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
        0,
        plugWorld,
        -plugForward,
        0u,
        0u,
        0u,
        0u
    );

    int chainCount = 0;
    ChainEntry previous = plugEntry;

    for (int chainIndex = 0; chainIndex < SPS_CHAIN_MAX_SOCKETS; chainIndex++) {
        if (sps_has_flag(previous.flags, SPS_SOCKET_FLAG_HOLE)) break;

        float3 sourceForward = sps_normalize(-previous.traversalNormal);

        if (previous.nextId > 0) {
            CellData linkedCellData;
            SocketData linkedSocketData;
            if (!sps_try_find_socket_data(socketTex, previous.nextId, previous.playerId, linkedCellData, linkedSocketData)) {
                break;
            }

            float unusedDistanceSq;
            float3 traversalNormal;
            uint rejectionFlags;
            float candidateLerp = sps_evaluate_chain_candidate(
                linkedCellData,
                linkedSocketData,
                previous,
                sourceForward,
                unusedDistanceSq,
                traversalNormal,
                rejectionFlags
            );
            if (candidateLerp <= 0) {
                SPS_DEBUG_SET(debugFlags, SPS_DEBUG_FLAG_ELIGIBILITY);
                SPS_DEBUG_SET(debugFlags, rejectionFlags);
                break;
            }

            chain[chainIndex] = sps_make_chain_entry(
                linkedCellData.cellIndex,
                dot(traversalNormal, sps_normalize(linkedCellData.normal)) < 0,
                candidateLerp,
                sps_resolver_socket_target_world(linkedCellData, linkedSocketData.flags),
                traversalNormal,
                linkedSocketData.flags,
                linkedCellData.id,
                linkedSocketData.nextId,
                linkedCellData.playerId
            );
            previous = chain[chainCount];
            chainCount++;
            continue;
        }

        float bestDistanceSq = 0;
        int bestCandidateIndex = -1;
        float bestCandidateLerp = 0;
        float3 bestTraversalNormal = float3(0, 0, 1);
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
            uint rejectionFlags;
            float candidateLerp = sps_evaluate_chain_candidate(
                candidate,
                candidateSocketData,
                previous,
                sourceForward,
                distanceSq,
                traversalNormal,
                rejectionFlags
            );
            if (candidateLerp <= 0) {
                SPS_DEBUG_SET(debugFlags, SPS_DEBUG_FLAG_ELIGIBILITY);
                SPS_DEBUG_SET(debugFlags, rejectionFlags);
                continue;
            }

            if (bestCandidateIndex < 0 || distanceSq < bestDistanceSq) {
                bestCandidateIndex = candidateIndex;
                bestCandidateLerp = candidateLerp;
                bestDistanceSq = distanceSq;
                bestTraversalNormal = traversalNormal;
                bestSocketData = candidateSocketData;
            }
        }
        if (bestCandidateIndex < 0) break;

        used[bestCandidateIndex] = true;
        CellData best = candidates[bestCandidateIndex];
        float3 bestWorld = sps_resolver_socket_target_world(best, bestSocketData.flags);
        chain[chainCount] = sps_make_chain_entry(
            best.cellIndex,
            dot(bestTraversalNormal, sps_normalize(best.normal)) < 0,
            bestCandidateLerp,
            bestWorld,
            bestTraversalNormal,
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
