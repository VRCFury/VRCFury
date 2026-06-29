#ifndef SPS_INC_CELL_GEOM
#define SPS_INC_CELL_GEOM

#include "sps_cell_layout.cginc"

#define SPS_CELL_VERTEX_STREAM(value, stream) \
    [unroll] \
    for (int spsCellVertexIndex = 0; spsCellVertexIndex < 3; spsCellVertexIndex++) { \
        value.vertex = sps_cell_geom_vertex(value.cellIndex, spsCellVertexIndex); \
        stream.Append(value); \
    } \
    stream.RestartStrip();

#define SPS_CELL_GEOM(value, stream) \
    uint resolverSlotSeed = sps_id_hash(); \
    for (int replica = 0; replica < SPS_CELL_REPLICA_COUNT; replica++) { \
        uint cellIndex = sps_hashed_screen_slot_index_from_id(resolverSlotSeed, (uint)replica); \
        value.cellIndex = (int)cellIndex; \
        SPS_CELL_VERTEX_STREAM(value, stream) \
    } \
    value.cellIndex = SPS_DICTIONARY_INDEX; \
    SPS_CELL_VERTEX_STREAM(value, stream)

inline float2 sps_cell_geom_uv(int vertexIndex) {
    const float overdraw = 0.01;
    return vertexIndex == 0
        ? float2(-overdraw, -overdraw)
        : vertexIndex == 1 ? float2(2 + overdraw, -overdraw) : float2(-overdraw, 2 + overdraw);
}

inline float4 sps_cell_geom_vertex(int index, int vertexIndex) {
    float2 pixel =
        float2(sps_cell_origin_from_index(index))
        + sps_cell_geom_uv(vertexIndex) * float2(SPS_CELL_WIDTH, SPS_CELL_HEIGHT);
    float2 ndc = pixel / _ScreenParams.xy * 2 - 1;
    return float4(ndc, 0, 1);
}

#endif
