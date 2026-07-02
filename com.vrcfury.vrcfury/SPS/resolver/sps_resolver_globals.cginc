#ifndef SPS_INC_RESOLVER_GLOBALS
#define SPS_INC_RESOLVER_GLOBALS

#include "sps_resolver_payload.cginc"

UNITY_INSTANCING_BUFFER_START(SpsResolverGlobals)
    UNITY_DEFINE_INSTANCED_PROP(float, _SPS_BakedLength)
    UNITY_DEFINE_INSTANCED_PROP(float, _SPS_BakedRadius)
    UNITY_DEFINE_INSTANCED_PROP(float4, _SPS_MetadataColor)
    UNITY_DEFINE_INSTANCED_PROP(float4, _SPS_BakedRadiusSamples0)
    UNITY_DEFINE_INSTANCED_PROP(float4, _SPS_BakedRadiusSamples1)
    UNITY_DEFINE_INSTANCED_PROP(float4, _SPS_BakedRadiusSamples2)
    UNITY_DEFINE_INSTANCED_PROP(float4, _SPS_BakedRadiusSamples3)
    UNITY_DEFINE_INSTANCED_PROP(float, _SPS_TagInclude1)
    UNITY_DEFINE_INSTANCED_PROP(float, _SPS_TagInclude2)
    UNITY_DEFINE_INSTANCED_PROP(float, _SPS_TagInclude3)
    UNITY_DEFINE_INSTANCED_PROP(float, _SPS_TagInclude4)
    UNITY_DEFINE_INSTANCED_PROP(float, _SPS_TagExclude1)
    UNITY_DEFINE_INSTANCED_PROP(float, _SPS_TagExclude2)
    UNITY_DEFINE_INSTANCED_PROP(float, _SPS_TagExclude3)
    UNITY_DEFINE_INSTANCED_PROP(float, _SPS_TagExclude4)
    UNITY_DEFINE_INSTANCED_PROP(float, _SPS_TagInclude1Self)
    UNITY_DEFINE_INSTANCED_PROP(float, _SPS_TagInclude1Others)
    UNITY_DEFINE_INSTANCED_PROP(float, _SPS_TagInclude2Self)
    UNITY_DEFINE_INSTANCED_PROP(float, _SPS_TagInclude2Others)
    UNITY_DEFINE_INSTANCED_PROP(float, _SPS_TagInclude3Self)
    UNITY_DEFINE_INSTANCED_PROP(float, _SPS_TagInclude3Others)
    UNITY_DEFINE_INSTANCED_PROP(float, _SPS_TagInclude4Self)
    UNITY_DEFINE_INSTANCED_PROP(float, _SPS_TagInclude4Others)
    UNITY_DEFINE_INSTANCED_PROP(float, _SPS_TagExclude1Self)
    UNITY_DEFINE_INSTANCED_PROP(float, _SPS_TagExclude1Others)
    UNITY_DEFINE_INSTANCED_PROP(float, _SPS_TagExclude2Self)
    UNITY_DEFINE_INSTANCED_PROP(float, _SPS_TagExclude2Others)
    UNITY_DEFINE_INSTANCED_PROP(float, _SPS_TagExclude3Self)
    UNITY_DEFINE_INSTANCED_PROP(float, _SPS_TagExclude3Others)
    UNITY_DEFINE_INSTANCED_PROP(float, _SPS_TagExclude4Self)
    UNITY_DEFINE_INSTANCED_PROP(float, _SPS_TagExclude4Others)
UNITY_INSTANCING_BUFFER_END(SpsResolverGlobals)

#define SPS_RESOLVER_GLOBAL_PROP(name) UNITY_ACCESS_INSTANCED_PROP(SpsResolverGlobals, name)
#define _SPS_BakedLength SPS_RESOLVER_GLOBAL_PROP(_SPS_BakedLength)
#define _SPS_BakedRadius SPS_RESOLVER_GLOBAL_PROP(_SPS_BakedRadius)
#define _SPS_MetadataColor SPS_RESOLVER_GLOBAL_PROP(_SPS_MetadataColor)
#define _SPS_BakedRadiusSamples0 SPS_RESOLVER_GLOBAL_PROP(_SPS_BakedRadiusSamples0)
#define _SPS_BakedRadiusSamples1 SPS_RESOLVER_GLOBAL_PROP(_SPS_BakedRadiusSamples1)
#define _SPS_BakedRadiusSamples2 SPS_RESOLVER_GLOBAL_PROP(_SPS_BakedRadiusSamples2)
#define _SPS_BakedRadiusSamples3 SPS_RESOLVER_GLOBAL_PROP(_SPS_BakedRadiusSamples3)
#define _SPS_TagInclude1 SPS_RESOLVER_GLOBAL_PROP(_SPS_TagInclude1)
#define _SPS_TagInclude2 SPS_RESOLVER_GLOBAL_PROP(_SPS_TagInclude2)
#define _SPS_TagInclude3 SPS_RESOLVER_GLOBAL_PROP(_SPS_TagInclude3)
#define _SPS_TagInclude4 SPS_RESOLVER_GLOBAL_PROP(_SPS_TagInclude4)
#define _SPS_TagExclude1 SPS_RESOLVER_GLOBAL_PROP(_SPS_TagExclude1)
#define _SPS_TagExclude2 SPS_RESOLVER_GLOBAL_PROP(_SPS_TagExclude2)
#define _SPS_TagExclude3 SPS_RESOLVER_GLOBAL_PROP(_SPS_TagExclude3)
#define _SPS_TagExclude4 SPS_RESOLVER_GLOBAL_PROP(_SPS_TagExclude4)
#define _SPS_TagInclude1Self SPS_RESOLVER_GLOBAL_PROP(_SPS_TagInclude1Self)
#define _SPS_TagInclude1Others SPS_RESOLVER_GLOBAL_PROP(_SPS_TagInclude1Others)
#define _SPS_TagInclude2Self SPS_RESOLVER_GLOBAL_PROP(_SPS_TagInclude2Self)
#define _SPS_TagInclude2Others SPS_RESOLVER_GLOBAL_PROP(_SPS_TagInclude2Others)
#define _SPS_TagInclude3Self SPS_RESOLVER_GLOBAL_PROP(_SPS_TagInclude3Self)
#define _SPS_TagInclude3Others SPS_RESOLVER_GLOBAL_PROP(_SPS_TagInclude3Others)
#define _SPS_TagInclude4Self SPS_RESOLVER_GLOBAL_PROP(_SPS_TagInclude4Self)
#define _SPS_TagInclude4Others SPS_RESOLVER_GLOBAL_PROP(_SPS_TagInclude4Others)
#define _SPS_TagExclude1Self SPS_RESOLVER_GLOBAL_PROP(_SPS_TagExclude1Self)
#define _SPS_TagExclude1Others SPS_RESOLVER_GLOBAL_PROP(_SPS_TagExclude1Others)
#define _SPS_TagExclude2Self SPS_RESOLVER_GLOBAL_PROP(_SPS_TagExclude2Self)
#define _SPS_TagExclude2Others SPS_RESOLVER_GLOBAL_PROP(_SPS_TagExclude2Others)
#define _SPS_TagExclude3Self SPS_RESOLVER_GLOBAL_PROP(_SPS_TagExclude3Self)
#define _SPS_TagExclude3Others SPS_RESOLVER_GLOBAL_PROP(_SPS_TagExclude3Others)
#define _SPS_TagExclude4Self SPS_RESOLVER_GLOBAL_PROP(_SPS_TagExclude4Self)
#define _SPS_TagExclude4Others SPS_RESOLVER_GLOBAL_PROP(_SPS_TagExclude4Others)

float sps_resolver_length() {
    return _SPS_BakedLength * sps_object_scale_world();
}

float sps_resolver_radius() {
    return _SPS_BakedRadius * sps_object_scale_world();
}

float4 sps_resolver_radius_block(int blockIndex) {
    float4 block = _SPS_BakedRadiusSamples3;
    if (blockIndex == 0) block = _SPS_BakedRadiusSamples0;
    else if (blockIndex == 1) block = _SPS_BakedRadiusSamples1;
    else if (blockIndex == 2) block = _SPS_BakedRadiusSamples2;
    else if (blockIndex == 3) block = _SPS_BakedRadiusSamples3;
    return block;
}

float sps_resolver_baked_radius_sample_raw(int sampleIndex) {
    uint clampedIndex = (uint)clamp(sampleIndex, 0, (int)SPS_RESOLVER_RADIUS_SAMPLE_COUNT - 1);
    float4 block = sps_resolver_radius_block((int)(clampedIndex >> 2));
    uint component = clampedIndex & 3u;
    if (component == 0u) return block.x;
    if (component == 1u) return block.y;
    if (component == 2u) return block.z;
    return block.w;
}

float sps_resolver_baked_radius_sample(int sampleIndex) {
    float previous = sps_resolver_baked_radius_sample_raw(sampleIndex - 1);
    float current = sps_resolver_baked_radius_sample_raw(sampleIndex);
    float next = sps_resolver_baked_radius_sample_raw(sampleIndex + 1);
    return previous * 0.25 + current * 0.5 + next * 0.25;
}

float sps_resolver_radius_sample(int sampleIndex) {
    return sps_resolver_baked_radius_sample(sampleIndex) * sps_object_scale_world();
}

#endif
