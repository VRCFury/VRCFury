#include "sps_globals.cginc"

void SpsApplyBlendshape(uint vertexId, inout float3 position, inout float3 normal, float blendshapeValue, int blendshapeId)
{
    if (blendshapeId >= _SPS_BlendshapeCount) return;
    int vertCount = (int)_SPS_BlendshapeVertCount;
    uint bytesPerBlendshape = vertCount * 6 + 1;
    uint blendshapeOffset = 1 + (vertCount * 7) + bytesPerBlendshape * blendshapeId;
    uint vertexOffset = blendshapeOffset + 1 + (vertexId * 6);
    float blendshapeValueAtBake = SPS_TEX_FLOAT(_SPS_Bake, blendshapeOffset);
    float blendshapeValueNow = blendshapeValue;
    float change = (blendshapeValueNow - blendshapeValueAtBake) * 0.01;
    position += SPS_TEX_FLOAT3(_SPS_Bake, vertexOffset) * change;
    normal += SPS_TEX_FLOAT3(_SPS_Bake, vertexOffset + 3) * change;
}
void SpsGetBakedPosition(uint vertexId, out float3 position, out float3 normal, out float active) {
    const uint bakeIndex = 1 + vertexId * 7;
    position = SPS_TEX_FLOAT3(_SPS_Bake, bakeIndex);
    normal = sps_normalize(SPS_TEX_FLOAT3(_SPS_Bake, bakeIndex+3));
    active = SPS_TEX_FLOAT(_SPS_Bake, bakeIndex + 6);
    if (position.z < 0) active = 0;

    SpsApplyBlendshape(vertexId, position, normal, _SPS_Blendshape0, 0);
    SpsApplyBlendshape(vertexId, position, normal, _SPS_Blendshape1, 1);
    SpsApplyBlendshape(vertexId, position, normal, _SPS_Blendshape2, 2);
    SpsApplyBlendshape(vertexId, position, normal, _SPS_Blendshape3, 3);
    SpsApplyBlendshape(vertexId, position, normal, _SPS_Blendshape4, 4);
    SpsApplyBlendshape(vertexId, position, normal, _SPS_Blendshape5, 5);
    SpsApplyBlendshape(vertexId, position, normal, _SPS_Blendshape6, 6);
    SpsApplyBlendshape(vertexId, position, normal, _SPS_Blendshape7, 7);
    SpsApplyBlendshape(vertexId, position, normal, _SPS_Blendshape8, 8);
    SpsApplyBlendshape(vertexId, position, normal, _SPS_Blendshape9, 9);
    SpsApplyBlendshape(vertexId, position, normal, _SPS_Blendshape10, 10);
    SpsApplyBlendshape(vertexId, position, normal, _SPS_Blendshape11, 11);
    SpsApplyBlendshape(vertexId, position, normal, _SPS_Blendshape12, 12);
    SpsApplyBlendshape(vertexId, position, normal, _SPS_Blendshape13, 13);
    SpsApplyBlendshape(vertexId, position, normal, _SPS_Blendshape14, 14);
    SpsApplyBlendshape(vertexId, position, normal, _SPS_Blendshape15, 15);

    position *= (_SPS_Length / _SPS_BakedLength);
}
