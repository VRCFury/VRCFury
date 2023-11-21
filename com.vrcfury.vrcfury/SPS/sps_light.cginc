#include "UnityShaderVariables.cginc"
#include "sps_globals.cginc"
#include "sps_utils.cginc"

// Type: 0=invalid 1=hole 2=ring 3=front
void sps_light_parse(float range, half4 color, out int type) {
	if (range >= 0.5 || (length(color.rgb) > 0 && color.a > 0)) {
		type = 0;
		return;
	}
	if (_SPS_Target_LL_Lights < 0.5) {
		const float thousandths = range % 0.001;
		if (thousandths > 0.0001 && thousandths < 0.0003) {
			// Legacy light coming from a lightless SPS socket
			type = 0;
			return;
		}
	}

	const int legacyRange = round((range % 0.1) * 100);

	if (legacyRange == 1) type = 1;
	if (legacyRange == 2) type = 2;
	if (legacyRange == 5) type = 3;
}

// Find nearby socket lights
void sps_light_search(
	inout bool ioFound,
	inout float3 ioRootLocal,
	inout bool ioIsRing,
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
	} else {
	 	// There were no root lights nearby. If we find a front light, use it as a root instead
	 	float minDistance = -1;
	 	for(int i = 0; i < 4; i++) {
	 		const float distance = length(lightLocalPos[i]);
	 		if (lightType[i] == 3 && (distance < minDistance || minDistance < 0)) {
	 			rootFound = true;
	 			rootIndex = i;
	 			minDistance = distance;
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
	ioRootNormal = frontFound
		? lightLocalPos[frontIndex] - lightLocalPos[rootIndex]
		: -1 * lightLocalPos[rootIndex];
	ioRootNormal = sps_normalize(ioRootNormal);
}
