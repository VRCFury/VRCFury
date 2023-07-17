#include "UnityShaderVariables.cginc"

#define SPS_PI float(3.14159265359)

// Type: 0=invalid 1=hole 2=ring 3=front
void sps_parse_light(float alpha, float range, out int type, int myChannel) {
	if (range >= 0.5) {
		type = 0;
		return;
	}

	int legacyRange = round((range % 0.1) * 100);
	bool isHoleRange = legacyRange == 1;
	bool isRingRange = legacyRange == 2;
	bool isFrontRange = legacyRange == 5;
	bool isEnhancedRange = 0.451 < range && range < 0.485;
	bool isEnhanced = false;
	int alphaBits = round(alpha * 255);
	if (legacyRange == 1 || legacyRange == 2 || legacyRange == 5 || isEnhancedRange) {
		isEnhanced = (alphaBits >> 6) & 3 == 2;
	}

	int channel = 0;
	if (isEnhanced) {
		if (isEnhancedRange) {
			channel = round((range - 0.452) / 0.002) + 1;
		} else {
			channel = 0;
		}
		type = ((alphaBits >> 4) & 3) + 1;
		if (type == 4) {
			type = 0;
		}
	} else {
		if (isHoleRange) type = 1;
		if (isRingRange) type = 2;
		if (isFrontRange) type = 3;
	}

	if (channel != myChannel) {
		type = 0;
		return;
	}
}
float3 sps_toLocal(float3 v) { return mul(unity_WorldToObject, float4(v, 1)); }
float3 sps_toWorld(float3 v) { return mul(unity_ObjectToWorld, float4(v, 1)); }
// https://forum.unity.com/threads/point-light-in-v-f-shader.499717/#post-3250460
float sps_attenToRange(float atten) { return (0.005 * sqrt(1000000.0 - atten)) / sqrt(atten); }

// Find nearby socket lights
bool sps_search(
	out float3 rootLocal,
	out bool isRing,
	out float3 rootNormal,
	out float entranceAngle,
	out float targetAngle
) {
	// Collect useful info about all the nearby lights that unity tells us about
	// (usually the brightest 4)
	int lightType[4];
	int myChannel = 0;
	float3 lightWorldPos[4];
	float3 lightLocalPos[4];
	{
		for(int i = 0; i < 4; i++) {
			const float alpha = unity_LightColor[i];
	 		const float range = sps_attenToRange(unity_4LightAtten0[i]);
			sps_parse_light(alpha, range, lightType[i], myChannel);
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
	
	if (rootFound) {
		rootLocal = rootFound ? lightLocalPos[rootIndex] : float3(0,0,0);
		isRing = rootFound ? lightType[rootIndex] != 1 : false;
		rootNormal = frontFound
			? normalize(lightLocalPos[frontIndex] - lightLocalPos[rootIndex])
			: -1 * normalize(lightLocalPos[rootIndex]);
		entranceAngle = acos(dot(rootNormal, float3(0,0,1)));
		targetAngle = acos(dot(normalize(rootLocal), float3(0,0,1)));

		if (entranceAngle < SPS_PI/2) {
			// facing away
			if (isRing) {
				rootNormal *= -1;
				entranceAngle = SPS_PI - entranceAngle;
			} else {
				rootFound = false;
			}
		}
	} else {
	 	rootLocal = float3(0,0,0);
	 	isRing = false;
	 	rootNormal = float3(0,0,0);
	 	entranceAngle = 0;
	 	targetAngle = 0;
	}
	
	return rootFound;
}
