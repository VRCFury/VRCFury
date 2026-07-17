#ifndef SPS_INC_UTILS
#define SPS_INC_UTILS

#include "UnityShaderVariables.cginc"

#define SPS_SPLIT_ID_BASE (1u << 16)
#define SPS_MERGE_SPLIT(name) sps_join_id(name##Low, name##High)

inline float sps_map(float value, float min1, float max1, float min2, float max2) {
    return min2 + (value - min1) * (max2 - min2) / (max1 - min1);
}

inline float sps_saturated_map(float value, float min, float max) {
    return saturate(sps_map(value, min, max, 0, 1));
}

inline bool sps_is_zero(float3 v) {
    return !any(v != 0);
}

inline bool sps_is_zero(float4 v) {
    return !any(v != 0);
}

// normalize fails fatally and discards the vert if length == 0
inline float3 sps_normalize(float3 a) {
    return sps_is_zero(a) ? float3(0,0,1) : normalize(a);
}

inline float3 sps_nearest_normal(float3 forward, float3 approximate) {
    return sps_normalize(cross(forward, cross(approximate, forward)));
}

inline float sps_length_sq(float3 v) {
    return dot(v, v);
}

inline bool sps_to_bool(float v) {
    return v > 0.5;
}

inline uint sps_to_uint(float v) {
    return (uint)round(v);
}

inline uint sps_join_id(float low, float high) {
    return sps_to_uint(low) | (sps_to_uint(high) << 16);
}

inline void sps_clip_rect(int2 local, int2 size) {
    clip(float4(
        local.x,
        local.y,
        size.x - 1 - local.x,
        size.y - 1 - local.y
    ) + 0.5);
}

inline float3 sps_object_origin_world() {
    return mul(unity_ObjectToWorld, float4(0, 0, 0, 1)).xyz;
}

inline float3 sps_object_direction_world(float3 localDirection) {
    return mul((float3x3)unity_ObjectToWorld, localDirection);
}

inline float3 sps_object_forward_world() {
    return sps_normalize(sps_object_direction_world(float3(0, 0, 1)));
}

inline float3 sps_object_up_world() {
    return sps_normalize(sps_object_direction_world(float3(0, 1, 0)));
}

inline float sps_object_scale_world() {
    return length(sps_object_direction_world(float3(0, 0, 1)));
}

inline float3 sps_rotate_around_axis(float3 v, float3 axis, float angle) {
    float sine = sin(angle);
    float cosine = cos(angle);
    return v * cosine + cross(axis, v) * sine + axis * dot(axis, v) * (1 - cosine);
}

inline float3 sps_toLocal(float3 v) { return mul(unity_WorldToObject, float4(v, 1)).xyz; }
inline float3 sps_toWorld(float3 v) { return mul(unity_ObjectToWorld, float4(v, 1)).xyz; }
inline float3 sps_direction_toWorld(float3 v) { return mul((float3x3)unity_ObjectToWorld, v); }
inline float3 sps_direction_toLocal(float3 v) { return mul((float3x3)unity_WorldToObject, v); }

#endif
