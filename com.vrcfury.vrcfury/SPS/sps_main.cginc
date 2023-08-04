#include "sps_bezier.cginc"
#include "sps_search.cginc"
#include "sps_bake.cginc"
#include "sps_utils.cginc"

// SPS Penetration Shader
void sps_apply_real(inout float3 vertex, inout float3 normal, uint vertexId, inout float4 color)
{
	const float worldLength = _SPS_Length;
	const float3 origVertex = vertex;
	const float3 origNormal = normal;
	const float3 bakeIndex = 1 + vertexId * 7;
	const float3 restingVertex = SpsBakedVertex(bakeIndex) * (_SPS_Length / _SPS_BakedLength);
	const float3 restingNormal = SpsBakedVertex(bakeIndex+3);
	const float active = SpsBakedFloat(bakeIndex + 6);

	if (vertex.z < 0 || active == 0) return;

	float3 rootPos;
	bool isRing;
	float3 frontNormal;
	const bool found = sps_search(rootPos, isRing, frontNormal, color);
	if (!found) return;

	float orfDistance = length(rootPos);
	float exitAngle = sps_angle_between(rootPos, float3(0,0,1));
	float entranceAngle = SPS_PI - sps_angle_between(frontNormal, rootPos);

	// Flip backward rings
	if (isRing && entranceAngle > SPS_PI/2) {
		frontNormal *= -1;
		entranceAngle = SPS_PI - entranceAngle;
	}

	// Decide if we should cancel deformation due to extreme angles, long distance, etc
	float bezierLerp;
	float dumbLerp;
	float shrinkLerp = 0;
	{
		float applyLerp = 1;
		// Cancel if base angle is too sharp
		const float allowedExitAngle = 0.6;
		const float exitAngleTooSharp = sps_saturated_map(
			exitAngle,
			SPS_PI*(allowedExitAngle*0.8), SPS_PI*allowedExitAngle
		);
		applyLerp = min(applyLerp, 1-exitAngleTooSharp);

		// Cancel if the entrance angle is too sharp
		const float allowedEntranceAngle = isRing ? 0.5 : 0.6;
		const float entranceAngleTooSharp = sps_saturated_map(
			entranceAngle,
			SPS_PI*(allowedEntranceAngle*0.8), SPS_PI*allowedEntranceAngle
		);
		applyLerp = min(applyLerp, 1-entranceAngleTooSharp);

		const float isBehind = sps_saturated_map(rootPos.z, worldLength*0.05, 0);
		if (!isRing) {
			// Uncancel if hilted in a hole
			const float hiltedSphereRadius = 0.6;
			const float inSphere = sps_saturated_map(
				orfDistance,
				worldLength*hiltedSphereRadius, worldLength*(hiltedSphereRadius*0.8)
			);
			const float hilted = min(isBehind, inSphere);
			shrinkLerp = hilted;
			applyLerp = max(applyLerp, hilted);
		} else {
			// Cancel if ring is near or behind base
			applyLerp = min(applyLerp, 1-isBehind);
		}

		// Cancel if too far away
		const float tooFar = sps_saturated_map(orfDistance, worldLength*1.5, worldLength*2.5);
		applyLerp = min(applyLerp, 1-tooFar);

		applyLerp = applyLerp * saturate(_SPS_Enabled);

		dumbLerp = sps_saturated_map(applyLerp, 0, 0.2) * active;
		bezierLerp = sps_saturated_map(applyLerp, 0, 1);
		shrinkLerp = sps_saturated_map(applyLerp, 0.8, 1) * shrinkLerp;
	}

	rootPos *= (1-shrinkLerp);
	orfDistance *= (1-shrinkLerp);

	float3 bezierPos;
	float3 bezierForward;
	float3 bezierRight;
	float3 bezierUp;
	float curveLength;
	if (length(rootPos) == 0) {
		bezierPos = float3(0,0,0);
		bezierForward = float3(0,0,1);
		bezierRight = float3(1,0,0);
		bezierUp = float3(0,1,0);
		curveLength = 0;
	} else{
		const float3 p0 = float3(0,0,0);
		const float p1Dist = min(orfDistance, max(worldLength / 8, rootPos.z / 4));
		const float p1DistWithPullout = sps_map(bezierLerp, 0, 1, worldLength * 5, p1Dist);
		const float3 p1 = float3(0,0,p1DistWithPullout);
		const float3 p2 = rootPos + frontNormal * p1Dist;
		const float3 p3 = rootPos;
		const float t = sps_bezierFindT(p0, p1, p2, p3, restingVertex.z, curveLength);

		bezierPos = sps_bezier(p0,p1,p2,p3,t);
		const float3 bezierDerivative = sps_bezierDerivative(p0,p1,p2,p3,t);
		bezierForward = sps_normalize(bezierDerivative);

		const float3 keepForwardUp = sps_normalize(cross(bezierForward, float3(1,0,0)));
		const float3 keepUprightUp = sps_normalize(cross(bezierForward, cross(float3(0,1,0), bezierForward)));

		// Keep forward only if closer to the middle horizontally
		float keepForward = sps_saturated_map(abs(sps_normalize(rootPos.xy).x), 1, 0);
		// Keep forward only if targeting in near or behind the base
		keepForward *= sps_saturated_map(rootPos.z, 0.3, 0.2);
		bezierUp = sps_normalize(lerp(keepUprightUp, keepForwardUp, keepForward));
		bezierRight = sps_normalize(cross(bezierUp, bezierForward));
	}

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
		holeShrink = sps_saturated_map(
			restingVertex.z,
			curveLength + holeRecessDistance2,
			curveLength + holeRecessDistance
		);
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

	float3 deformedVertex = bezierPos + bezierRight * restingVertex.x * holeShrink + bezierUp * restingVertex.y * holeShrink;
	float3 deformedNormal = bezierRight * restingNormal.x + bezierUp * restingNormal.y + bezierForward * restingNormal.z;

	vertex = lerp(origVertex, deformedVertex, dumbLerp);
	normal = sps_normalize(lerp(origNormal, deformedNormal, dumbLerp));
}
void sps_apply(inout float3 vertex, inout float3 normal, uint vertexId, inout float4 color) {
	// When VERTEXLIGHT_ON is missing, there are no lights nearby, and the 4light arrays will be full of junk
	// Temporarily disable this check since apparently it causes some passes to not apply SPS
	//#ifdef VERTEXLIGHT_ON
	sps_apply_real(vertex, normal, vertexId, color);
	//#endif
}
