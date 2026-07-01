#ifndef SPS_INC_RESOLVER_SHADER_TYPES
#define SPS_INC_RESOLVER_SHADER_TYPES

#include "sps_resolver_types.cginc"

struct appdata {
    float4 vertex : POSITION;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct v2g {
    float4 vertex : SV_POSITION;
    UNITY_VERTEX_INPUT_INSTANCE_ID
    UNITY_VERTEX_OUTPUT_STEREO
};

struct v2f {
    float4 vertex : SV_POSITION;
    UNITY_VERTEX_INPUT_INSTANCE_ID
    UNITY_VERTEX_OUTPUT_STEREO
    nointerpolation int cellIndex : TEXCOORD0;
    #if SPS_RESOLVER_DEBUG
    nointerpolation float4 debug : TEXCOORD1;
    #endif
    nointerpolation int chainSlotIndex[SPS_CHAIN_MAX_SOCKETS] : TEXCOORD11;
    nointerpolation bool chainFlipped[SPS_CHAIN_MAX_SOCKETS] : TEXCOORD21;
};

#endif
