#include "UnityShaderVariables.cginc"
#include "sps_globals.cginc"
#include "sps_utils.cginc"
#include "sps_plus.cginc"

void sps_light_parse(float range, half4 color, out int type) {
	type = SPS_TYPE_INVALID;

	if (range >= 0.5 || (length(color.rgb) > 0 && color.a > 0)) {
		// Outside of SPS range, or visible light
		return;
	}

	const int secondDecimal = round((range % 0.1) * 100);

	if (_SPS_Plus_Enabled > 0.5) {
		if (secondDecimal == 1 || secondDecimal == 2) {
			const int fourthDecimal = round((range % 0.001) * 10000);
			if (fourthDecimal == 2) {
				type = SPS_TYPE_SPSPLUS;
				return;
			}
		}
	}

	if (secondDecimal == 1) type = SPS_TYPE_HOLE;
	if (secondDecimal == 2) type = SPS_TYPE_RING_TWOWAY;
	if (secondDecimal == 5) type = SPS_TYPE_FRONT;
}

// Find nearby socket lights
void sps_light_search(
	inout int ioType,
	inout float3 ioRootLocal,
	inout float3 ioRootNormal,
	inout float4 ioColor
) {
	// Collect useful info about all the nearby lights that unity tells us about
	// (usually the brightest 4)
	int lightType[4];
	float3 lightWorldPos[4];
	float3 lightLocalPos[4];
	{
		for(int i = 0; i < 4; i++) {
	 		const float range = sps_attenToRange(unity_4LightAtten0[i]);
			sps_light_parse(range, unity_LightColor[i], lightType[i]);
	 		lightWorldPos[i] = float3(unity_4LightPosX0[i], unity_4LightPosY0[i], unity_4LightPosZ0[i]);
	 		lightLocalPos[i] = sps_toLocal(lightWorldPos[i]);
	 	}
	}

	// Fill in SPS light info from contacts
	if (_SPS_Plus_Enabled > 0.5) {
		float spsPlusDistance;
		int spsPlusType;
		sps_plus_search(spsPlusDistance, spsPlusType);
		if (spsPlusType != SPS_TYPE_INVALID) {
			bool spsLightFound = false;
			int spsLightIndex = 0;
			float spsMinError = 0;
			for(int i = 0; i < 4; i++) {
				if (lightType[i] != SPS_TYPE_SPSPLUS) continue;
				const float3 myPos = lightLocalPos[i];
				const float3 otherPos = lightLocalPos[spsLightIndex];
				const float myError = abs(length(myPos) - spsPlusDistance);
				if (myError > 0.2) continue;
				const float otherError = spsMinError;

				bool imBetter = false;
				if (!spsLightFound) imBetter = true;
				else if (myError < 0.3 && myPos.z >= 0 && otherPos.z < 0) imBetter = true;
				else if (otherError < 0.3 && otherPos.z >= 0 && myPos.z < 0) imBetter = false;
				else if (myError < otherError) imBetter = true;

				if (imBetter) {
					spsLightFound = true;
					spsLightIndex = i;
					spsMinError = myError;
				}
			}
			if (spsLightFound) {
				lightType[spsLightIndex] = spsPlusType;
			}
		}
	}

	// Find nearest socket root
	int rootIndex = 0;
	bool rootFound = false;
	{
	 	float minDistance = -1;
	 	for(int i = 0; i < 4; i++) {
	 		const float distance = length(lightLocalPos[i]);
	 		const int type = lightType[i];
	 		if (distance < minDistance || minDistance < 0) {
	 			if (type == SPS_TYPE_HOLE || type == SPS_TYPE_RING_TWOWAY || type == SPS_TYPE_RING_ONEWAY) {
	 				rootFound = true;
	 				rootIndex = i;
	 				minDistance = distance;
	 			}
	 		}
	 	}
	}
	
	int frontIndex = 0;
	bool frontFound = false;
	if (rootFound) {
	 	// Find front (normal) light for socket root if available
	 	float minDistance = 0.1;
	 	for(int i = 0; i < 4; i++) {
	 		const float distFromRoot = length(lightWorldPos[i] - lightWorldPos[rootIndex]);
	 		if (lightType[i] == SPS_TYPE_FRONT && distFromRoot < minDistance) {
	 			frontFound = true;
	 			frontIndex = i;
	 			minDistance = distFromRoot;
	 		}
	 	}
	}

	// This can happen if the socket was misconfigured, or if it's on a first person head bone that's been shrunk down
	// Ignore the normal, since it'll be so close to the root that rounding error will cause problems
	if (frontFound && length(lightLocalPos[frontIndex] - lightLocalPos[rootIndex]) < 0.00005) {
		frontFound = false;
	}

	if (!rootFound) return;
	if (ioType != SPS_TYPE_INVALID && length(lightLocalPos[rootIndex]) >= length(ioRootLocal)) return;

	ioType = lightType[rootIndex];
	ioRootLocal = lightLocalPos[rootIndex];
	ioRootNormal = frontFound
		? lightLocalPos[frontIndex] - lightLocalPos[rootIndex]
		: -1 * lightLocalPos[rootIndex];
	ioRootNormal = sps_normalize(ioRootNormal);
}
