#ifndef SPS_UTILS
#define SPS_UTILS

float sps_map(float value, float min1, float max1, float min2, float max2) {
    return min2 + (value - min1) * (max2 - min2) / (max1 - min1);
}

float sps_saturated_map(float value, float min, float max) {
    return saturate(sps_map(value, min, max, 0, 1));
}

// normalize fails fatally and discards the vert if length == 0
#define sps_normalize(a) length(a) == 0 ? float3(0,0,1) : normalize(a)

#define sps_angle_between(a,b) acos(dot(sps_normalize(a),sps_normalize(b)))

#endif
