#include "sps_deform_globals.cginc"
#include "../common/sps_cell_layout.cginc"

#define SPS_MAX_BLENDSHAPES 16

inline SpsTexture sps_get_bake_tex() {
    return MakeSpsTexture(_SPS_Bake, _SPS_Bake_TexelSize);
}

inline float4 sps_bake_read_rgba(SpsTexture tex, uint index) {
    const uint width = (uint)round(abs(1.0 / _SPS_Bake_TexelSize.x));
    const uint2 pixel = uint2(index % width, index / width);
    return SPS_READ_TEX(tex, pixel);
}

inline float sps_bake_read_float(SpsTexture tex, uint index) {
    return asfloat(sps_decode_uint_raw(sps_bake_read_rgba(tex, index)));
}

inline float3 sps_bake_read_float3(SpsTexture tex, uint index) {
    return float3(
        sps_bake_read_float(tex, index),
        sps_bake_read_float(tex, index + 1),
        sps_bake_read_float(tex, index + 2)
    );
}

inline void SpsApplyBlendshape(SpsTexture bakeTex, uint vertexId, inout float3 position, inout float3 normal, inout float3 tangent, float blendshapeValue, int blendshapeId) {
    const int vertCount = sps_to_uint(_SPS_BlendshapeVertCount);
    const uint bytesPerBlendshapeVertex = 9;
    const uint bytesPerBlendshape = vertCount * bytesPerBlendshapeVertex + 1;
    const uint blendshapeOffset = 1 + (vertCount * 10) + bytesPerBlendshape * blendshapeId;
    const uint vertexOffset = blendshapeOffset + 1 + (vertexId * bytesPerBlendshapeVertex);
    const float blendshapeValueAtBake = sps_bake_read_float(bakeTex, blendshapeOffset);
    const float blendshapeValueNow = blendshapeValue;
    const float change = (blendshapeValueNow - blendshapeValueAtBake) * 0.01;
    position += sps_bake_read_float3(bakeTex, vertexOffset) * change;
    normal += sps_bake_read_float3(bakeTex, vertexOffset + 3u) * change;
    tangent += sps_bake_read_float3(bakeTex, vertexOffset + 6u) * change;
}

void SpsGetBakedPosition(uint vertexId, out float3 position, out float3 normal, out float3 tangent, out float active) {
    SpsTexture bakeTex = sps_get_bake_tex();
    const uint bakeIndex = 1 + vertexId * 10;
    position = sps_bake_read_float3(bakeTex, bakeIndex);
    normal = sps_bake_read_float3(bakeTex, bakeIndex + 3u);
    tangent = sps_bake_read_float3(bakeTex, bakeIndex + 6u);
    active = sps_bake_read_float(bakeTex, bakeIndex + 9u);
    if (position.z < 0) active = 0;

    float blendshapeValues[SPS_MAX_BLENDSHAPES] = {
        _SPS_Blendshape0, _SPS_Blendshape1, _SPS_Blendshape2, _SPS_Blendshape3,
        _SPS_Blendshape4, _SPS_Blendshape5, _SPS_Blendshape6, _SPS_Blendshape7,
        _SPS_Blendshape8, _SPS_Blendshape9, _SPS_Blendshape10, _SPS_Blendshape11,
        _SPS_Blendshape12, _SPS_Blendshape13, _SPS_Blendshape14, _SPS_Blendshape15,
    };

    uint count = sps_to_uint(_SPS_BlendshapeCount);
    for (uint i = 0; i < SPS_MAX_BLENDSHAPES; i++) {
        if (i >= count) break;
        SpsApplyBlendshape(bakeTex, vertexId, position, normal, tangent, blendshapeValues[i], i);
    }
}
