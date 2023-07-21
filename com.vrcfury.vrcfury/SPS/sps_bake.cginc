#include "sps_globals.cginc"

float4 SpsBakedDataRaw(uint offset)
{
    return SPS_BAKEDATA(offset%8192, offset/8192);
}
int4 SpsBakedData(uint offset)
{
    return SpsBakedDataRaw(offset) * 255;
}
float SpsBakedFloat(uint offset)
{
    const int4 data = SpsBakedData(offset);
    return asfloat((data[3] << 24) | (data[2] << 16) | (data[1] << 8) | data[0]);
}
float3 SpsBakedVertex(uint offset)
{
    return float3(SpsBakedFloat(offset), SpsBakedFloat(offset+1), SpsBakedFloat(offset+2));
}
