#include "sps_bezier.cginc"
#include "sps_search.cginc"

float _SPS_PenetratorLength;

// SPS Penetration Shader
void sps_apply(inout float3 vertex, inout float3 normal, inout float4 color)
{
	float worldLength = _SPS_PenetratorLength;
	float averageLength = 0.28;
	float scaleAdjustment = worldLength / averageLength;
	
	float3 origVertex = vertex;
	float3 origNormal = normal;

	if (vertex.z < 0) return;

	float3 rootPos;
	bool isRing;
	float3 frontNormal;
	float entranceAngle;
	float targetAngle;
	bool found = sps_search(rootPos, isRing, frontNormal, entranceAngle, targetAngle);
	if (!found) return;

	float orfDistance = length(rootPos);

	float3 p0 = float3(0,0,0);
	float3 p1 = float3(0,0,orfDistance/4);
	float3 p2 = rootPos + frontNormal * (orfDistance/2);
	float3 p3 = rootPos;
	float t = saturate(origVertex.z / orfDistance);
	t = sps_bezierAdjustT(p0, p1, p2, p3, t);

	float3 bezierPos = sps_bezier(p0,p1,p2,p3,t);
	float3 bezierDerivative = sps_bezierDerivative(p0,p1,p2,p3,t);
	float3 bezierForward = normalize(bezierDerivative);
	float3 bezierUp = normalize(cross(bezierForward, float3(1,0,0)));
	float3 bezierRight = normalize(cross(bezierUp, bezierForward));

	// Handle holes and rings
	float holeShrink = 1;
	if (isRing) {
		if (origVertex.z >= orfDistance) {
			// Straighten if past socket
			bezierPos += (origVertex.z - orfDistance) * bezierForward;
		}
	} else {
		float holeRecessDistance = 0.02 * scaleAdjustment; // 2cm
		float holeRecessDistance2 = 0.04 * scaleAdjustment; // 4cm
		holeShrink = saturate(sps_map(
			origVertex.z,
			orfDistance + holeRecessDistance, orfDistance + holeRecessDistance2,
			1, 0));
		if (origVertex.z >= orfDistance + holeRecessDistance2) {
			// If way past socket, condense to point
			bezierPos += holeRecessDistance2 * bezierForward;
		} else if (origVertex.z >= orfDistance) {
			// Straighten if past socket
			bezierPos += (origVertex.z - orfDistance) * bezierForward;
		}
	}

	vertex = bezierPos + bezierRight * origVertex.x * holeShrink + bezierUp * origVertex.y * holeShrink;
	normal = bezierRight * normal.x + bezierUp * normal.y + bezierForward * normal.z;

	float tooFar = saturate(sps_map(orfDistance, worldLength*1.5, worldLength*2.5, 1, 0));
	float entranceAngleTooSharp = saturate(sps_map(entranceAngle, SPS_PI*0.65, SPS_PI*0.5, 1, 0));
	float targetAngleTooSharp = saturate(sps_map(targetAngle, SPS_PI*0.3, SPS_PI*0.4, 1, 0));
	float cancelLerp = entranceAngleTooSharp;
	cancelLerp = min(cancelLerp, targetAngleTooSharp);
	if (!isRing)
	{
		float hilted = saturate(sps_map(orfDistance, worldLength*0.5, worldLength*0.4, 0, 1));
		cancelLerp = max(cancelLerp, hilted);
	}
	cancelLerp = min(cancelLerp, tooFar);
	vertex = lerp(origVertex, vertex, cancelLerp);
	normal = lerp(origNormal, normal, cancelLerp);
}
