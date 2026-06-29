#ifndef SPS_INC_UTILS
#define SPS_INC_UTILS

#include "UnityShaderVariables.cginc"

float sps_map(float value, float min1, float max1, float min2, float max2) {
    return min2 + (value - min1) * (max2 - min2) / (max1 - min1);
}

float sps_saturated_map(float value, float min, float max) {
    return saturate(sps_map(value, min, max, 0, 1));
}

// normalize fails fatally and discards the vert if length == 0
float3 sps_normalize(float3 a) {
    return length(a) == 0 ? float3(0,0,1) : normalize(a);
}

float3 sps_nearest_normal(float3 forward, float3 approximate) {
    return sps_normalize(cross(forward, cross(approximate, forward)));
}

float sps_length_sq(float3 v) {
    return dot(v, v);
}

bool sps_to_bool(float v) {
    return v > 0.5;
}

uint sps_to_uint(float v) {
    return (uint)round(v);
}

void sps_clip_rect(int2 local, int2 size) {
    clip(float4(
        local.x,
        local.y,
        size.x - 1 - local.x,
        size.y - 1 - local.y
    ) + 0.5);
}

float3 sps_object_origin_world() {
    return mul(unity_ObjectToWorld, float4(0, 0, 0, 1)).xyz;
}

float3 sps_object_direction_world(float3 localDirection) {
    return mul((float3x3)unity_ObjectToWorld, localDirection);
}

float3 sps_object_forward_world() {
    return sps_normalize(sps_object_direction_world(float3(0, 0, 1)));
}

float3 sps_object_up_world() {
    return sps_normalize(sps_object_direction_world(float3(0, 1, 0)));
}

float sps_object_scale_world() {
    return length(sps_object_direction_world(float3(0, 0, 1)));
}

float3 sps_rotate_around_axis(float3 v, float3 axis, float angle) {
    float sine = sin(angle);
    float cosine = cos(angle);
    return v * cosine + cross(axis, v) * sine + axis * dot(axis, v) * (1 - cosine);
}

float3 sps_toLocal(float3 v) { return mul(unity_WorldToObject, float4(v, 1)).xyz; }

#endif
