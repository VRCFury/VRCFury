#include "UnityShaderVariables.cginc"
#include "sps_globals.cginc"
#include "sps_utils.cginc"
#include "sps_plus.cginc"

// Type: 0=invalid 1=hole 2=ring 3=front 4=SPS+
void sps_light_parse(float range, half4 color, out int type) {
	if (range >= 0.5 || (length(color.rgb) > 0 && color.a > 0)) {
		type = 0;
		return;
	}

	const int legacyRange = round((range % 0.1) * 100);

	if (_SPS_Plus_Enabled > 0.5) {
		if (legacyRange == 1 || legacyRange == 2) {
			const float thousandths = range % 0.001;
			if (thousandths > 0.0001 && thousandths < 0.0003) {
				type = 4;
				return;
			}
		}
	}

	if (legacyRange == 1) type = 1;
	if (legacyRange == 2) type = 2;
	if (legacyRange == 5) type = 3;
	
	subtype = round((range % 0.001) * 10000);
}

// Find nearby socket lights
void sps_light_search(
	inout bool ioFound,
	inout float3 ioRootLocal,
	inout bool ioIsRing,
	inout bool ioIsReversible,
	inout float3 ioRootNormal,
	inout float4 ioColor
) {
	// Collect useful info about all the nearby lights that unity tells us about
	// (usually the brightest 4)
	int lightType[4];
	int lightSubType[4];
	float3 lightWorldPos[4];
	float3 lightLocalPos[4];
	{
		for(int i = 0; i < 4; i++) {
	 		const float range = sps_attenToRange(unity_4LightAtten0[i]);
			sps_light_parse(range, unity_LightColor[i], lightType[i], lightSubType[i]);
	 		lightWorldPos[i] = float3(unity_4LightPosX0[i], unity_4LightPosY0[i], unity_4LightPosZ0[i]);
	 		lightLocalPos[i] = sps_toLocal(lightWorldPos[i]);
	 	}
	}

	// Fill in SPS light info from contacts
	if (_SPS_Plus_Enabled > 0.5) {
		float spsTriDistance;
		int spsTriType;
		sps_plus_search(spsTriDistance, spsTriType);
		if (spsTriType > 0) {
			bool spsLightFound = false;
			int spsLightIndex = 0;
			float spsMinError = 0;
			for(int i = 0; i < 4; i++) {
				if (lightType[i] != 4) continue;
				const float3 myPos = lightLocalPos[i];
				const float3 otherPos = lightLocalPos[spsLightIndex];
				const float myError = abs(length(myPos) - spsTriDistance);
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
				lightType[spsLightIndex] = spsTriType;
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
	 		if ((type == 1 || type == 2) && (distance < minDistance || minDistance < 0)) {
	 			rootFound = true;
	 			rootIndex = i;
	 			minDistance = distance;
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
	 		if (lightType[i] == 3 && distFromRoot < minDistance) {
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
	if (ioFound && length(lightLocalPos[rootIndex]) >= length(ioRootLocal)) return;

	ioFound = true;
	ioRootLocal = lightLocalPos[rootIndex];
	ioIsRing = lightType[rootIndex] != 1;
	ioIsReversible = lightSubType[rootIndex] & 1;
	ioRootNormal = frontFound
		? lightLocalPos[frontIndex] - lightLocalPos[rootIndex]
		: -1 * lightLocalPos[rootIndex];
	ioRootNormal = sps_normalize(ioRootNormal);
}
