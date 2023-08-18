#ifndef SPS_UTILS
#define SPS_UTILS

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

#define sps_angle_between(a,b) acos(dot(sps_normalize(a),sps_normalize(b)))

float3 sps_nearest_normal(float3 forward, float3 approximate) {
    return sps_normalize(cross(forward, cross(approximate, forward)));
}

#endif
