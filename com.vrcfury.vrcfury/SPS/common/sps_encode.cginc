#ifndef SPS_INC_SCREEN_CODEC
#define SPS_INC_SCREEN_CODEC

// These helper functions are to be used when encoding or decoding raw
// data that goes through a render texture (aka, the screen). Otherwise,
// not all values will survive due to gamma offset.

inline float sps_gamma_to_linear_exact(float value) {
    if (value <= 0.04045F) return value / 12.92F;
    if (value < 1.0F) return pow((value + 0.055F) / 1.055F, 2.4F);
    return pow(value, 2.2F);
}

inline float sps_linear_to_gamma_exact(float value) {
    if (value <= 0.0F) return 0.0F;
    if (value <= 0.0031308F) return 12.92F * value;
    if (value < 1.0F) return 1.055F * pow(value, 0.4166667F) - 0.055F;
    return pow(value, 0.45454545F);
}

float sps_decode_channel(float value) {
    #ifdef UNITY_COLORSPACE_GAMMA
        return saturate(value);
    #else
        return saturate(sps_linear_to_gamma_exact(value));
    #endif
}

float4 sps_encode_uint(uint value) {
    uint4 shifts = uint4(0, 8, 16, 24);
    float4 bytes = float4((value >> shifts) & 255u) / 255.0;
    #ifdef UNITY_COLORSPACE_GAMMA
        return saturate(bytes);
    #else
        return saturate(float4(
            sps_gamma_to_linear_exact(bytes.r),
            sps_gamma_to_linear_exact(bytes.g),
            sps_gamma_to_linear_exact(bytes.b),
            sps_gamma_to_linear_exact(bytes.a)
        ));
    #endif
}

float4 sps_encode_float(float value) {
    return sps_encode_uint(asuint(value));
}

#endif
