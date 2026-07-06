#ifndef SPS_INC_SCREEN_CODEC
#define SPS_INC_SCREEN_CODEC

// These helper functions are to be used when encoding or decoding raw
// data that goes through a render texture (aka, the screen). Otherwise,
// not all values will survive due to gamma offset.

#include "UnityCG.cginc"

float sps_decode_channel(float value) {
    #ifdef UNITY_COLORSPACE_GAMMA
        return saturate(value);
    #else
        return saturate(LinearToGammaSpaceExact(value));
    #endif
}

float4 sps_encode_uint(uint value) {
    uint4 shifts = uint4(0, 8, 16, 24);
    float4 bytes = float4((value >> shifts) & 255u) / 255.0;
    #ifdef UNITY_COLORSPACE_GAMMA
        return saturate(bytes);
    #else
        return saturate(float4(
            GammaToLinearSpaceExact(bytes.r),
            GammaToLinearSpaceExact(bytes.g),
            GammaToLinearSpaceExact(bytes.b),
            GammaToLinearSpaceExact(bytes.a)
        ));
    #endif
}

float4 sps_encode_float(float value) {
    return sps_encode_uint(asuint(value));
}

#endif
