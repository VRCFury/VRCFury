#ifndef SPS_INC_RESOLVER_LIGHT
#define SPS_INC_RESOLVER_LIGHT

#include "../common/sps_types.cginc"

#define SPS_LEGACY_LIGHT_FRONT 256u

// https://forum.unity.com/threads/point-light-in-v-f-shader.499717/#post-9052987
float sps_attenToRange(float atten) { return 5.0 * (1.0 / sqrt(atten)); }

bool sps_light_parse(int lightIndex, out float3 world, out uint flags) {
	const float range = sps_attenToRange(unity_4LightAtten0[lightIndex]);
	const half4 color = unity_LightColor[lightIndex];
	world = float3(
		unity_4LightPosX0[lightIndex],
		unity_4LightPosY0[lightIndex],
		unity_4LightPosZ0[lightIndex]
	);
	flags = 0u;

	if (range >= 0.5 || (length(color.rgb) > 0 && color.a > 0)) {
		// Outside of SPS range, or visible light
		return false;
	}

	const int secondDecimal = round((range % 0.1) * 100);
	if (secondDecimal == 1 || secondDecimal == 2 || secondDecimal == 5) {
		const float fourthDecimal = fmod(range, 0.001) * 10000;
		if (fourthDecimal >= 5 && fourthDecimal <= 7) {
			// This is a "legacy light" coming from another SPS2 user, so we can just
			// ignore it and use their atlas data instead.
			return false;
		}
	}

	if (secondDecimal == 1) flags = SPS_SOCKET_FLAG_HOLE;
	if (secondDecimal == 2) flags = SPS_SOCKET_FLAG_DOUBLE_SIDED;
	if (secondDecimal == 5) flags = SPS_LEGACY_LIGHT_FRONT;
	return secondDecimal == 1 || secondDecimal == 2 || secondDecimal == 5;
}

#endif
