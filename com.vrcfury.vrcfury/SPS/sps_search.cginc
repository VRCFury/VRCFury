#include "UnityShaderVariables.cginc"
#include "sps_globals.cginc"

#define SPS_PI float(3.14159265359)

// Type: 0=invalid 1=hole 2=ring 3=front
void sps_parse_light(float range, half4 color, out int type) {
	if (range >= 0.5 || (length(color.rgb) > 0 && color.a > 0)) {
		type = 0;
		return;
	}

	int legacyRange = round((range % 0.1) * 100);

	if (legacyRange == 1) type = 1;
	if (legacyRange == 2) type = 2;
	if (legacyRange == 5) type = 3;
}
float3 sps_toLocal(float3 v) { return mul(unity_WorldToObject, float4(v, 1)); }
float3 sps_toWorld(float3 v) { return mul(unity_ObjectToWorld, float4(v, 1)); }
// https://forum.unity.com/threads/point-light-in-v-f-shader.499717/#post-9052987
float sps_attenToRange(float atten) { return 5.0 * (1.0 / sqrt(atten)); }

float triangulate(float centerRange,float offsetRange,float distBetweenStations) {
	centerRange = (1-centerRange) * 3;
	offsetRange = (1-offsetRange) * 3;
	float inner = (distBetweenStations*distBetweenStations + centerRange*centerRange - offsetRange*offsetRange) / (2*distBetweenStations*centerRange);
	inner = clamp(inner, -1, 1);
	float ang = acos(inner);
	float offset = centerRange * sin(ang - SPS_PI/2);
	return -offset;
}

// Find nearby socket lights
bool sps_search(
	out float3 rootLocal,
	out bool isRing,
	out float3 rootNormal,
	inout float4 color
) {
	if (_SPS_Target_Center == 1 || _SPS_Target_Forward == 1 || _SPS_Target_Right == 1 || _SPS_Target_Up == 1
		|| _SPS_Front_Center == 1 || _SPS_Front_Forward == 1 || _SPS_Front_Right == 1 || _SPS_Front_Up == 1)
	{
		rootLocal = float3(0,0,0);
		isRing = false;
		rootNormal = float3(0,0,1);
		return true;
	}

	if (_SPS_Target_Center == 0 || _SPS_Target_Forward == 0 || _SPS_Target_Right == 0 || _SPS_Target_Up == 0
		|| _SPS_Front_Center == 0 || _SPS_Front_Forward == 0 || _SPS_Front_Right == 0 || _SPS_Front_Up == 0)
	{
		rootLocal = float3(0,0,0);
		isRing = false;
		rootNormal = float3(0,0,0);
		return false;
	}

	rootLocal = float3(
		triangulate(_SPS_Target_Center, _SPS_Target_Right, 0.01),
		triangulate(_SPS_Target_Center, _SPS_Target_Up, 0.01),
		triangulate(_SPS_Target_Center, _SPS_Target_Forward, 0.01)
	);
	isRing = false;
	float3 front = float3(
		triangulate(_SPS_Front_Center, _SPS_Front_Right, 0.01),
		triangulate(_SPS_Front_Center, _SPS_Front_Up, 0.01),
		triangulate(_SPS_Front_Center, _SPS_Front_Forward, 0.01)
	);
	rootNormal = sps_normalize(front - rootLocal);
	return true;
	
	// Collect useful info about all the nearby lights that unity tells us about
	// (usually the brightest 4)
	int lightType[4];
	float3 lightWorldPos[4];
	float3 lightLocalPos[4];
	{
		for(int i = 0; i < 4; i++) {
	 		const float range = sps_attenToRange(unity_4LightAtten0[i]);
			sps_parse_light(range, unity_LightColor[i], lightType[i]);
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

	if (rootFound) {
		rootLocal = lightLocalPos[rootIndex];
		isRing = lightType[rootIndex] != 1;
		rootNormal = frontFound
			? lightLocalPos[frontIndex] - lightLocalPos[rootIndex]
			: -1 * lightLocalPos[rootIndex];
		rootNormal = sps_normalize(rootNormal);
	} else {
	 	rootLocal = float3(0,0,0);
	 	isRing = false;
	 	rootNormal = float3(0,0,0);
	}
	
	return rootFound;
}
