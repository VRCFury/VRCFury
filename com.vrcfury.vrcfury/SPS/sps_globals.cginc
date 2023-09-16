#ifndef SPS_GLOBALS
#define SPS_GLOBALS

#ifdef SHADER_TARGET_SURFACE_ANALYSIS
    #define SPS_TEX_DEFINE(name) float4 name##_TexelSize; sampler2D name;
    #define SPS_TEX_RAW_FLOAT4_XY(name,x,y) tex2Dlod(name, float4(uint2(x,y) * name##_TexelSize.xy, 0, 0))
#else
    #define SPS_TEX_DEFINE(name) Texture2D name;
    #define SPS_TEX_RAW_FLOAT4_XY(name,x,y) name[uint2(x,y)]
#endif
float SpsInt4ToFloat(int4 data) { return asfloat((data[3] << 24) | (data[2] << 16) | (data[1] << 8) | data[0]); }
#define SPS_TEX_RAW_FLOAT4(tex, offset) ((float4)(SPS_TEX_RAW_FLOAT4_XY(tex, (offset)%8192, (offset)/8192)))
#define SPS_TEX_RAW_INT4(tex, offset) ((int4)(SPS_TEX_RAW_FLOAT4(tex, offset) * 255))
#define SPS_TEX_FLOAT(tex, offset) SpsInt4ToFloat(SPS_TEX_RAW_INT4(tex, offset))
#define SPS_TEX_FLOAT3(tex, offset) float3(SPS_TEX_FLOAT(tex, offset), SPS_TEX_FLOAT(tex, (offset)+1), SPS_TEX_FLOAT(tex, (offset)+2))

float _SPS_Length;
float _SPS_BakedLength;
SPS_TEX_DEFINE(_SPS_Bake)
float _SPS_BlendshapeVertCount;
float _SPS_Blendshape0;
float _SPS_Blendshape1;
float _SPS_Blendshape2;
float _SPS_Blendshape3;
float _SPS_Blendshape4;
float _SPS_Blendshape5;
float _SPS_Blendshape6;
float _SPS_Blendshape7;
float _SPS_Blendshape8;
float _SPS_Blendshape9;
float _SPS_Blendshape10;
float _SPS_Blendshape11;
float _SPS_Blendshape12;
float _SPS_Blendshape13;
float _SPS_Blendshape14;
float _SPS_Blendshape15;
float _SPS_BlendshapeCount;

float _SPS_Enabled;
float _SPS_Channel;
float _SPS_Overrun;

#endif
