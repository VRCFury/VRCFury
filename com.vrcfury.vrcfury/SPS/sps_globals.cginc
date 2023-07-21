#ifndef SPS_GLOBALS
#define SPS_GLOBALS

float _SPS_Length;
float _SPS_BakedLength;
#ifdef SHADER_TARGET_SURFACE_ANALYSIS
    float4 _SPS_Bake_TexelSize;
    sampler2D _SPS_Bake;
    #define SPS_BAKEDATA(x,y) tex2Dlod(_SPS_Bake, float4(uint2(x,y) * _SPS_Bake_TexelSize.xy, 0, 0))
#else
    Texture2D _SPS_Bake;
    #define SPS_BAKEDATA(x,y) _SPS_Bake[uint2(x,y)]
#endif
float _SPS_Enabled;
//float _SPS_Channel;
float _SPS_Overrun;

#endif
