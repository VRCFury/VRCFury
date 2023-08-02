#ifndef SPS_UTILS
#define SPS_UTILS

float sps_map(float value, float min1, float max1, float min2, float max2) {
    return min2 + (value - min1) * (max2 - min2) / (max1 - min1);
}

float sps_saturated_map(float value, float min, float max) {
    return saturate(sps_map(value, min, max, 0, 1));
}

// https://keithmaggio.wordpress.com/2011/02/15/math-magician-lerp-slerp-and-nlerp/
float3 sps_slerp(float3 start, float3 end, float percent) {
    if (length(start - end) < 0.001) return start;
    float dot_ = dot(start, end);
    dot_ = clamp(dot_, -1.0, 1.0);
    float theta = acos(dot_) * percent;
    float3 relativeVec = normalize(end - start*dot_);
    return ((start*cos(theta)) + (relativeVec*sin(theta)));
}

#define sps_angle_between(a,b) acos(dot(normalize(a),normalize(b)))

#endif
