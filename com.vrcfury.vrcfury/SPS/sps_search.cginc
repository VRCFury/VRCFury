#include "UnityShaderVariables.cginc"

#define SPS_PI float(3.14159265359)
bool sps_isType(float range, float target) { return abs(range - target) < 0.005; }
bool sps_isHole(float range) { return sps_isType(range, 0.41); }
bool sps_isRing(float range) { return sps_isType(range, 0.42); }
bool sps_isFront(float range) { return sps_isType(range, 0.45); }
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
	float lightRange[4];
	float3 lightWorldPos[4];
	float3 lightLocalPos[4];
	{
		for(int i = 0; i < 4; i++)
	 	{
	 		lightRange[i] = sps_attenToRange(unity_4LightAtten0[i]);
	 		lightWorldPos[i] = float3(unity_4LightPosX0[i], unity_4LightPosY0[i], unity_4LightPosZ0[i]);
	 		lightLocalPos[i] = sps_toLocal(lightWorldPos[i]);
	 	}
	}
	
	// Find nearest socket root
	int rootIndex = 0;
	bool rootFound = false;
	{
	 	float minDistance = -1;
	 	for(int i = 0; i < 4; i++)
	 	{
	 		const float distance = length(lightLocalPos[i]);
	 		const bool isRing = sps_isRing(lightRange[i]);
	 		const bool isHole = sps_isHole(lightRange[i]);
	 		if ((isRing || isHole) && (distance < minDistance || minDistance < 0)) {
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
	 		const float distFromRoot = abs(lightWorldPos[i] - lightWorldPos[rootIndex]);
	 		if (sps_isFront(lightRange[i]) && distFromRoot < minDistance) {
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
	 		if (sps_isFront(lightRange[i]) && (distance < minDistance || minDistance < 0)) {
	 			rootFound = true;
	 			rootIndex = i;
	 			minDistance = distance;
	 		}
	 	}
	}
	
	if (rootFound) {
		rootLocal = rootFound ? lightLocalPos[rootIndex] : float3(0,0,0);
		isRing = rootFound ? !sps_isHole(lightRange[rootIndex]) : false;
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
