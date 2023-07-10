#include "sps_bezier.cginc"
#include "sps_search.cginc"
#include "sps_bake.cginc"

float _SPS_Length;
float _SPS_BakedLength;

// SPS Penetration Shader
void sps_apply(inout float3 vertex, inout float3 normal, inout float4 color, uint vertexId)
{
	const float worldLength = _SPS_Length;
	const float averageLength = 0.28;
	const float scaleAdjustment = worldLength / averageLength;

	const float3 origVertex = vertex;
	const float3 origNormal = normal;
	const float3 bakeIndex = 1 + vertexId * 6;
	const float3 restingVertex = SpsBakedVertex(bakeIndex) * (_SPS_Length / _SPS_BakedLength);
	const float3 restingNormal = SpsBakedVertex(bakeIndex+3);

	if (vertex.z < 0) return;

	float3 rootPos;
	bool isRing;
	float3 frontNormal;
	float entranceAngle;
	float targetAngle;
	bool found = sps_search(rootPos, isRing, frontNormal, entranceAngle, targetAngle);
	if (!found) return;

	float orfDistance = length(rootPos);

	const float3 p0 = float3(0,0,0);
	const float3 p1 = float3(0,0,orfDistance/4);
	const float3 p2 = rootPos + frontNormal * (orfDistance/2);
	const float3 p3 = rootPos;
	float t = saturate(restingVertex.z / orfDistance);
	t = sps_bezierAdjustT(p0, p1, p2, p3, t);

	float3 bezierPos = sps_bezier(p0,p1,p2,p3,t);
	float3 bezierDerivative = sps_bezierDerivative(p0,p1,p2,p3,t);
	float3 bezierForward = normalize(bezierDerivative);
	float3 bezierUp = normalize(cross(bezierForward, float3(1,0,0)));
	float3 bezierRight = normalize(cross(bezierUp, bezierForward));

	// Handle holes and rings
	float holeShrink = 1;
	if (isRing) {
		if (restingVertex.z >= orfDistance) {
			// Straighten if past socket
			bezierPos += (restingVertex.z - orfDistance) * bezierForward;
		}
	} else {
		float holeRecessDistance = 0.02 * scaleAdjustment; // 2cm
		float holeRecessDistance2 = 0.04 * scaleAdjustment; // 4cm
		holeShrink = saturate(sps_map(
			restingVertex.z,
			orfDistance + holeRecessDistance, orfDistance + holeRecessDistance2,
			1, 0));
		if (restingVertex.z >= orfDistance + holeRecessDistance2) {
			// If way past socket, condense to point
			bezierPos += holeRecessDistance2 * bezierForward;
		} else if (restingVertex.z >= orfDistance) {
			// Straighten if past socket
			bezierPos += (restingVertex.z - orfDistance) * bezierForward;
		}
	}

	float3 deformedVertex = bezierPos + bezierRight * restingVertex.x * holeShrink + bezierUp * restingVertex.y * holeShrink;
	float3 deformedNormal = bezierRight * restingNormal.x + bezierUp * restingNormal.y + bezierForward * restingNormal.z;

	// Cancel if the entrance angle is too sharp
	float entranceAngleTooSharp = saturate(sps_map(entranceAngle, SPS_PI*0.65, SPS_PI*0.5, 0, 1));
	float applyLerp = 1-entranceAngleTooSharp;

	// Cancel if base angle is too sharp
	float targetAngleTooSharp = saturate(sps_map(targetAngle, SPS_PI*0.3, SPS_PI*0.4, 0, 1));
	applyLerp = min(applyLerp, 1-targetAngleTooSharp);

	// Uncancel if hilted in a hole
	if (!isRing)
	{
		float hilted = saturate(sps_map(orfDistance, worldLength*0.5, worldLength*0.4, 0, 1));
		applyLerp = max(applyLerp, hilted);
	}

	// Cancel if too far away
	float tooFar = saturate(sps_map(orfDistance, worldLength*1.5, worldLength*2.5, 0, 1));
	applyLerp = min(applyLerp, 1-tooFar);

	vertex = lerp(origVertex, deformedVertex, applyLerp);
	normal = lerp(origNormal, deformedNormal, applyLerp);
}
