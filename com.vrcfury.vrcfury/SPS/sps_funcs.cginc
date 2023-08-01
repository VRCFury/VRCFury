#include "sps_bezier.cginc"
#include "sps_search.cginc"
#include "sps_bake.cginc"

// SPS Penetration Shader
void sps_apply_real(inout float3 vertex, inout float3 normal, uint vertexId, inout float4 color)
{
	const float worldLength = _SPS_Length;
	const float3 origVertex = vertex;
	const float3 origNormal = normal;
	const float3 bakeIndex = 1 + vertexId * 7;
	const float3 restingVertex = SpsBakedVertex(bakeIndex) * (_SPS_Length / _SPS_BakedLength);
	const float3 restingNormal = SpsBakedVertex(bakeIndex+3);
	const float active = SpsBakedFloat(bakeIndex + 6) > 0 ? 1 : 0;

	if (vertex.z < 0 || active == 0) return;

	float3 rootPos;
	bool isRing;
	float3 frontNormal;
	const bool found = sps_search(rootPos, isRing, frontNormal, color);
	if (!found) return;

	float orfDistance = length(rootPos);
	float entranceAngle = SPS_PI - acos(dot(frontNormal, normalize(rootPos)));
	float targetAngle = acos(dot(normalize(rootPos), float3(0,0,1)));

	// Flip backward rings
	if (isRing && entranceAngle > SPS_PI/2) {
		frontNormal *= -1;
		entranceAngle = SPS_PI - entranceAngle;
	}

	// Decide if we should cancel deformation due to extreme angles, long distance, etc
	float bezierLerp;
	float dumbLerp;
	{
		float applyLerp = 1;
		// Cancel if base angle is too sharp
		const float targetAngleTooSharp = saturate(sps_map(targetAngle, SPS_PI*0.5, SPS_PI*0.6, 0, 1));
		applyLerp = min(applyLerp, 1-targetAngleTooSharp);

		// Cancel if the entrance angle is too sharp
		float entranceAngleTooSharp = isRing
			? saturate(sps_map(entranceAngle, SPS_PI*0.4, SPS_PI*0.5, 0, 1))
			: saturate(sps_map(entranceAngle, SPS_PI*0.5, SPS_PI*0.6, 0, 1));
		applyLerp = min(applyLerp, 1-entranceAngleTooSharp);

		if (!isRing) {
			// Uncancel if hilted in a hole
			const float hilted = saturate(sps_map(orfDistance, worldLength*0.5, worldLength*0.4, 0, 1));
			applyLerp = max(applyLerp, hilted);
		} else {
			// Cancel if hilted in a ring
			const float hilted = saturate(sps_map(orfDistance, worldLength*0.05, 0, 0, 1));
			applyLerp = min(applyLerp, 1-hilted);
		}

		// Cancel if too far away
		const float tooFar = saturate(sps_map(orfDistance, worldLength*1.5, worldLength*2.5, 0, 1));
		applyLerp = min(applyLerp, 1-tooFar);

		applyLerp = applyLerp * active * saturate(_SPS_Enabled);

		dumbLerp = saturate(sps_map(applyLerp, 0, 0.2, 0, 1));
		bezierLerp = saturate(sps_map(applyLerp, 0, 1, 0, 1));
	}

	const float3 p0 = float3(0,0,0);
	float defaultP1Dist = rootPos.z / 4;
	float p1Dist = sps_map(bezierLerp, 0, 1, worldLength * 5, defaultP1Dist);
	const float3 p1 = float3(0,0,p1Dist);
	const float3 p2 = rootPos + frontNormal * defaultP1Dist;
	const float3 p3 = rootPos;
	float curveLength;
	float t = sps_bezierFindT(p0, p1, p2, p3, restingVertex.z, curveLength);

	float3 bezierPos = sps_bezier(p0,p1,p2,p3,t);
	float3 bezierDerivative = sps_bezierDerivative(p0,p1,p2,p3,t);
	float3 bezierForward = normalize(bezierDerivative);
	float3 bezierUp = normalize(cross(bezierForward, float3(1,0,0)));
	float3 bezierRight = normalize(cross(bezierUp, bezierForward));

	// Handle holes and rings
	float holeShrink = 1;
	if (isRing) {
		if (restingVertex.z >= curveLength) {
			// Straighten if past socket
			bezierPos += (restingVertex.z - curveLength) * bezierForward;
		}
	} else {
		const float holeRecessDistance = worldLength * 0.05;
		const float holeRecessDistance2 = worldLength * 0.1;
		holeShrink = saturate(sps_map(
			restingVertex.z,
			curveLength + holeRecessDistance, curveLength + holeRecessDistance2,
			1, 0));
		if(_SPS_Overrun > 0) {
			if (restingVertex.z >= curveLength + holeRecessDistance2) {
				// If way past socket, condense to point
				bezierPos += holeRecessDistance2 * bezierForward;
			} else if (restingVertex.z >= curveLength) {
				// Straighten if past socket
				bezierPos += (restingVertex.z - curveLength) * bezierForward;
			}
		}
	}

	const float3 deformedVertex = bezierPos + bezierRight * restingVertex.x * holeShrink + bezierUp * restingVertex.y * holeShrink;
	const float3 deformedNormal = bezierRight * restingNormal.x + bezierUp * restingNormal.y + bezierForward * restingNormal.z;

	vertex = lerp(origVertex, deformedVertex, dumbLerp);
	normal = lerp(origNormal, deformedNormal, dumbLerp);
}
void sps_apply(inout float3 vertex, inout float3 normal, uint vertexId, inout float4 color) {
	// When VERTEXLIGHT_ON is missing, there are no lights nearby, and the 4light arrays will be full of junk
	// Temporarily disable this check since apparently it causes some passes to not apply SPS
	//#ifdef VERTEXLIGHT_ON
	sps_apply_real(vertex, normal, vertexId, color);
	//#endif
}
