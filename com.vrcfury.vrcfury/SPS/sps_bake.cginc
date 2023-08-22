#include "sps_globals.cginc"

float4 SpsBakedDataRaw(Texture2D tex, uint offset)
{
    return SPS_TEX_DATA(tex, offset%8192, offset/8192);
}
int4 SpsBakedData(Texture2D tex, uint offset)
{
    return SpsBakedDataRaw(tex, offset) * 255;
}
float SpsBakedFloat(Texture2D tex, uint offset)
{
    const int4 data = SpsBakedData(tex, offset);
    return asfloat((data[3] << 24) | (data[2] << 16) | (data[1] << 8) | data[0]);
}
float3 SpsBakedVertex(Texture2D tex, uint offset)
{
    return float3(SpsBakedFloat(tex, offset), SpsBakedFloat(tex, offset+1), SpsBakedFloat(tex, offset+2));
}
void SpsApplyBlendshape(uint vertexId, inout float3 position, inout float3 normal, float blendshapeValue, int blendshapeId)
{
    if (blendshapeId >= _SPS_BlendshapeCount) return;
    int vertCount = (int)_SPS_BlendshapeVertCount;
    uint bytesPerBlendshape = vertCount * 6 + 1;
    uint blendshapeOffset = 1 + (vertCount * 7) + bytesPerBlendshape * blendshapeId;
    uint vertexOffset = blendshapeOffset + 1 + (vertexId * 6);
    float blendshapeValueAtBake = SpsBakedFloat(_SPS_Bake, blendshapeOffset);
    float blendshapeValueNow = blendshapeValue;
    float change = (blendshapeValueNow - blendshapeValueAtBake) * 0.01;
    position += SpsBakedVertex(_SPS_Bake, vertexOffset) * change;
    normal += SpsBakedVertex(_SPS_Bake, vertexOffset + 3) * change;
}
void SpsGetBakedPosition(uint vertexId, out float3 position, out float3 normal, out float active) {
    const uint bakeIndex = 1 + vertexId * 7;
    position = SpsBakedVertex(_SPS_Bake, bakeIndex) * (_SPS_Length / _SPS_BakedLength);
    normal = sps_normalize(SpsBakedVertex(_SPS_Bake, bakeIndex+3));
    active = SpsBakedFloat(_SPS_Bake, bakeIndex + 6);

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
}
