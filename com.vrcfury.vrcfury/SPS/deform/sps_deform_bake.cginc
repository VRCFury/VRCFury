#include "sps_deform_globals.cginc"
#include "../common/sps_utils.cginc"

#define SPS_MAX_BLENDSHAPES 16

inline float4 sps_bake_read_rgba(uint index) {
    const uint width = (uint)round(abs(1.0 / _SPS_Bake_TexelSize.x));
    return _SPS_Bake.Load(int3(index % width, index / width, 0));
}

inline uint sps_bake_decode_uint(float4 rgba) {
    uint4 bytes = (uint4)round(saturate(rgba) * 255.0);
    return bytes.x | (bytes.y << 8) | (bytes.z << 16) | (bytes.w << 24);
}

inline float sps_bake_read_float(uint index) {
    return asfloat(sps_bake_decode_uint(sps_bake_read_rgba(index)));
}

inline float3 sps_bake_read_float3(uint index) {
    return float3(
        sps_bake_read_float(index),
        sps_bake_read_float(index + 1),
        sps_bake_read_float(index + 2)
    );
}

inline void SpsApplyBlendshape(uint vertexId, inout float3 position, inout float3 normal, inout float3 tangent, float blendshapeValue, int blendshapeId) {
    const int vertCount = sps_to_uint(_SPS_BlendshapeVertCount);
    const uint bytesPerBlendshapeVertex = 9;
    const uint bytesPerBlendshape = vertCount * bytesPerBlendshapeVertex + 1;
    const uint blendshapeOffset = 1 + (vertCount * 10) + bytesPerBlendshape * blendshapeId;
    const uint vertexOffset = blendshapeOffset + 1 + (vertexId * bytesPerBlendshapeVertex);
    const float blendshapeValueAtBake = sps_bake_read_float(blendshapeOffset);
    const float blendshapeValueNow = blendshapeValue;
    const float change = (blendshapeValueNow - blendshapeValueAtBake) * 0.01;
    position += sps_bake_read_float3(vertexOffset) * change;
    normal += sps_bake_read_float3(vertexOffset + 3u) * change;
    tangent += sps_bake_read_float3(vertexOffset + 6u) * change;
}

void SpsGetBakedPosition(uint vertexId, out float3 position, out float3 normal, out float3 tangent, out float active) {
    const uint bakeIndex = 1 + vertexId * 10;
    position = sps_bake_read_float3(bakeIndex);
    normal = sps_bake_read_float3(bakeIndex + 3u);
    tangent = sps_bake_read_float3(bakeIndex + 6u);
    active = sps_bake_read_float(bakeIndex + 9u);
    if (position.z < 0) active = 0;

    #ifdef SPS_HAS_BLENDSHAPES
    // Saves ~0.2s per vertex variant.
    float blendshapeValues[SPS_MAX_BLENDSHAPES] = {
        _SPS_Blendshape0, _SPS_Blendshape1, _SPS_Blendshape2, _SPS_Blendshape3,
        _SPS_Blendshape4, _SPS_Blendshape5, _SPS_Blendshape6, _SPS_Blendshape7,
        _SPS_Blendshape8, _SPS_Blendshape9, _SPS_Blendshape10, _SPS_Blendshape11,
        _SPS_Blendshape12, _SPS_Blendshape13, _SPS_Blendshape14, _SPS_Blendshape15,
    };

    uint count = sps_to_uint(_SPS_BlendshapeCount);
    for (uint i = 0; i < SPS_MAX_BLENDSHAPES; i++) {
        if (i >= count) break;
        SpsApplyBlendshape(vertexId, position, normal, tangent, blendshapeValues[i], i);
    }
    #endif
}
