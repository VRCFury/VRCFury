#ifndef SPS_UTILS
#define SPS_UTILS

float sps_map(float value, float min1, float max1, float min2, float max2) {
    return min2 + (value - min1) * (max2 - min2) / (max1 - min1);
}

float sps_saturated_map(float value, float min, float max) {
    return saturate(sps_map(value, min, max, 0, 1));
}

#define sps_angle_between(a,b) acos(dot(normalize(a),normalize(b)))

#endif
