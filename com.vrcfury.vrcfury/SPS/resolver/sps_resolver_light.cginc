#ifndef SPS_INC_RESOLVER_LIGHT
#define SPS_INC_RESOLVER_LIGHT

#include "../common/sps_types.cginc"

#define SPS_LEGACY_LIGHT_NONE 0
#define SPS_LEGACY_LIGHT_HOLE 1
#define SPS_LEGACY_LIGHT_RING 2
#define SPS_LEGACY_LIGHT_FRONT 3

// https://forum.unity.com/threads/point-light-in-v-f-shader.499717/#post-9052987
float sps_attenToRange(float atten) { return 5.0 * rsqrt(atten); }

inline float3 sps_light_world(uint lightIndex) {
	return float3(
		unity_4LightPosX0[lightIndex],
		unity_4LightPosY0[lightIndex],
		unity_4LightPosZ0[lightIndex]
	);
}

inline float sps_light_range(uint lightIndex) {
	return sps_attenToRange(unity_4LightAtten0[lightIndex]);
}

inline uint sps_light_type(uint lightIndex) {
	const float range = sps_light_range(lightIndex);
	if (range >= 0.5) return SPS_LEGACY_LIGHT_NONE;
	const half4 color = unity_LightColor[lightIndex];
	if (!sps_is_zero(color.rgb) && color.a > 0) return SPS_LEGACY_LIGHT_NONE;

	const int secondDecimal = round((range % 0.1) * 100);
	if (secondDecimal == 1 || secondDecimal == 2 || secondDecimal == 5) {
		const float fourthDecimal = fmod(range, 0.001) * 10000;
		if (fourthDecimal >= 5 && fourthDecimal <= 7) {
			// This is a "legacy light" coming from another SPS2 user, so we can just
			// ignore it and use their atlas data instead.
			return SPS_LEGACY_LIGHT_NONE;
		}
	}

	if (secondDecimal == 1) return SPS_LEGACY_LIGHT_HOLE;
	if (secondDecimal == 2) return SPS_LEGACY_LIGHT_RING;
	if (secondDecimal == 5) return SPS_LEGACY_LIGHT_FRONT;
	return SPS_LEGACY_LIGHT_NONE;
}

#endif
