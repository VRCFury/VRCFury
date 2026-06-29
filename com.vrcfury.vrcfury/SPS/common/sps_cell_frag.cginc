#ifndef SPS_INC_CELL_FRAG
#define SPS_INC_CELL_FRAG

#include "sps_dictionary.cginc"
#include "sps_utils.cginc"

inline bool sps_cell_frag(
    int cellIndex,
    float4 vertex,
    out uint pixelIndex,
    out float4 rgba
) {
    pixelIndex = 0u;
    rgba = 0;
    if (sps_should_abort()) {
        clip(-1);
        return true;
    }

    int2 local = int2(
        floor(vertex.x),
        floor(_ScreenParams.y - vertex.y)
    ) - sps_cell_origin_from_index(cellIndex);
    sps_clip_rect(local, int2(SPS_CELL_WIDTH, SPS_CELL_HEIGHT));
    pixelIndex = (uint)local.x + (uint)local.y * (uint)SPS_CELL_WIDTH;

    if (sps_dictionary_frag(cellIndex, pixelIndex, rgba)) {
        return true;
    }

    return false;
}

#endif
