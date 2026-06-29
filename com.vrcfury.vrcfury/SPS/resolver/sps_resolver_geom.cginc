#ifndef SPS_RESOLVER_GEOM
#define SPS_RESOLVER_GEOM

#include "../common/sps_dictionary.cginc"
#include "../common/sps_cell_geom.cginc"
#include "../common/sps_cell_hash.cginc"
#include "../common/sps_cell_layout.cginc"
#include "../common/sps_types.cginc"
#include "../common/sps_utils.cginc"
#include "sps_resolver_candidates.cginc"
#include "sps_resolver_chain.cginc"
#include "sps_resolver_globals.cginc"
#include "sps_resolver_shader_types.cginc"
#include "sps_resolver_types.cginc"

#if SPS_RESOLVER_DEBUG
float4 sps_resolver_debug_color(uint debugFlags, bool found) {
    if (sps_id() == 0u) {
        return float4(0, 0, 1, 1);
    }
    if (found) {
        return float4(0, 1, 0, 1);
    }
    if (sps_has_flag(debugFlags, SPS_DEBUG_FLAG_FETCH_REJECT)) {
        return float4(0.75, 0.5, 1, 1);
    }
    if (sps_has_flag(debugFlags, SPS_DEBUG_FLAG_DISTANCE_CULL)) {
        return float4(1, 0.25, 0.75, 1);
    }
    if (sps_has_flag(debugFlags, SPS_DEBUG_FLAG_EXIT_REJECT)) {
        return float4(1, 0.2, 0.2, 1);
    }
    if (sps_has_flag(debugFlags, SPS_DEBUG_FLAG_ENTRANCE_REJECT)) {
        return float4(1, 0.6, 0.2, 1);
    }
    if (sps_has_flag(debugFlags, SPS_DEBUG_FLAG_BEHIND_REJECT)) {
        return float4(0.2, 0.8, 1, 1);
    }
    if (sps_has_flag(debugFlags, SPS_DEBUG_FLAG_TOO_FAR_REJECT)) {
        return float4(0.8, 0.2, 1, 1);
    }
    if (sps_has_flag(debugFlags, SPS_DEBUG_FLAG_ELIGIBILITY)) {
        return float4(1, 0.75, 0, 1);
    }
    if (sps_has_flag(debugFlags, SPS_DEBUG_FLAG_DUPLICATE_REJECT)) {
        return float4(0.5, 1, 0, 1);
    }
    if (sps_has_flag(debugFlags, SPS_DEBUG_FLAG_HEADER_ENTRY)) {
        return float4(1, 0, 1, 1);
    }
    return float4(1, 0, 0, 1);
}
#endif

void sps_emit_resolver(
    v2g input,
    int chainCount,
    ChainEntry chain[SPS_CHAIN_MAX_SOCKETS],
    uint debugFlags,
    inout TriangleStream<v2f> stream
) {
    bool found = chainCount > 0;
    #if SPS_RESOLVER_DEBUG
        float4 debugColor = sps_resolver_debug_color(debugFlags, found);
    #endif

    v2f output;
    UNITY_TRANSFER_INSTANCE_ID(input, output);
    UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
    for (int segmentIndex = 0; segmentIndex < SPS_CHAIN_MAX_SOCKETS; segmentIndex++) {
        if (found && segmentIndex < chainCount) {
            output.chainSlotIndex[segmentIndex] = chain[segmentIndex].slotIndex;
            output.chainFlipped[segmentIndex] = chain[segmentIndex].flipped;
            output.chainApplyLerp[segmentIndex] = chain[segmentIndex].applyLerp;
        } else {
            output.chainSlotIndex[segmentIndex] = SPS_CHAIN_REF_INVALID;
            output.chainFlipped[segmentIndex] = false;
            output.chainApplyLerp[segmentIndex] = 0;
        }
    }
    #if SPS_RESOLVER_DEBUG
        output.debug = debugColor;
    #endif

    SPS_CELL_GEOM(output, stream)
}

[maxvertexcount((SPS_CELL_REPLICA_COUNT + 1) * 3)]
void geom(triangle v2g input[3], inout TriangleStream<v2f> stream) {
    UNITY_SETUP_INSTANCE_ID(input[0]);
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input[0]);
    if (sps_should_abort()) return;

    float3 plugWorld = sps_object_origin_world();
    float3 plugForward = sps_object_forward_world();
    uint debugFlags = 0;
    int candidateCount;
    CellData cells[SPS_CANDIDATE_COUNT];

    SpsTexture tex = SPS_GET_TEX(_VFGrid56);
    sps_collect_candidates(tex, plugWorld, candidateCount, cells, debugFlags, SPS_PRODUCT_SOCKET);

    uint includeTags[4];
    uint includeFlags[4];
    uint excludeTags[4];
    uint excludeFlags[4];
    sps_resolver_parse_tag_rules(includeTags, includeFlags, excludeTags, excludeFlags);
    sps_filter_cells(
        tex,
        candidateCount,
        cells,
        sps_player_id(),
        includeTags,
        includeFlags,
        excludeTags,
        excludeFlags
    );

    ChainEntry chain[SPS_CHAIN_MAX_SOCKETS];
    int chainCount = sps_build_chain(
        tex,
        plugWorld,
        plugForward,
        candidateCount,
        cells,
        chain,
        debugFlags
    );
    sps_emit_resolver(input[0], chainCount, chain, debugFlags, stream);
}

#endif
