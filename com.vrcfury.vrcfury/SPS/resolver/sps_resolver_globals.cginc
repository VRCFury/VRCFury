#ifndef SPS_INC_RESOLVER_GLOBALS
#define SPS_INC_RESOLVER_GLOBALS

#include "../common/sps_id.cginc"

UNITY_INSTANCING_BUFFER_START(SpsResolverGlobals)
    UNITY_DEFINE_INSTANCED_PROP(float, _SPS_BakedLength)
    UNITY_DEFINE_INSTANCED_PROP(float, _SPS_BakedRadius)
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

#endif
