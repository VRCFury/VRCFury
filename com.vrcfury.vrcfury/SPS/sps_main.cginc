#include "sps_bezier.cginc"
#include "sps_light.cginc"
#include "sps_plus.cginc"
#include "sps_bake.cginc"
#include "sps_utils.cginc"

// SPS Penetration Shader
void sps_apply_real(
	inout SPS_STRUCT_POSITION_TYPE vertex,
	inout SPS_STRUCT_NORMAL_TYPE normal,
	inout SPS_STRUCT_TANGENT_TYPE tangent,
	uint vertexId,
	inout SPS_STRUCT_COLOR_TYPE color
) {
	const float worldLength = _SPS_Length;
	const float3 origVertex = vertex.xyz;
	const float3 origNormal = normal.xyz;
	const float3 origTangent = tangent.xyz;
	float3 bakedVertex;
	float3 bakedNormal;
	float3 bakedTangent;
	float active;
	SpsGetBakedPosition(vertexId, bakedVertex, bakedNormal, bakedTangent, active);

	bakedVertex *= (_SPS_Length / _SPS_BakedLength);
	
	if (active == 0) return;

	float3 rootPos;
	int type = SPS_TYPE_INVALID;
	float3 frontNormal;
	sps_light_search(type, rootPos, frontNormal);
	if (type == SPS_TYPE_INVALID) return;

	float orfDistance = length(rootPos);
	float exitAngle = sps_angle_between(rootPos, float3(0,0,1));
	float entranceAngle = SPS_PI - sps_angle_between(frontNormal, rootPos);

	// Flip backward bidirectional rings
	if (type == SPS_TYPE_RING_TWOWAY && entranceAngle > SPS_PI/2) {
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
		const float exitAngleTooSharp = exitAngle > SPS_PI*allowedExitAngle ? 1 : 0;
		applyLerp = min(applyLerp, 1-exitAngleTooSharp);

		// Cancel if the entrance angle is too sharp
		if (type != SPS_TYPE_RING_TWOWAY) {
			const float allowedEntranceAngle = 0.8;
			const float entranceAngleTooSharp = entranceAngle > SPS_PI*allowedEntranceAngle ? 1 : 0;
			applyLerp = min(applyLerp, 1-entranceAngleTooSharp);
		}

		if (type == SPS_TYPE_HOLE) {
			// Uncancel if hilted in a hole
			const float hiltedSphereRadius = 0.5;
			const float inSphere = orfDistance > worldLength*hiltedSphereRadius ? 0 : 1;
			//const float hilted = min(isBehind, inSphere);
			//shrinkLerp = hilted;
			const float hilted = inSphere;
			applyLerp = max(applyLerp, hilted);
		} else {
			// Cancel if ring is near or behind base
			const float isBehind = rootPos.z > 0 ? 0 : 1;
			applyLerp = min(applyLerp, 1-isBehind);
		}

		// Cancel if too far away
		const float tooFar = sps_saturated_map(orfDistance, worldLength*1.2, worldLength*1.6);
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
		sps_bezierSolve(p0, p1, p2, p3, bakedVertex.z, curveLength, bezierPos, bezierForward, bezierUp);
		bezierRight = sps_normalize(cross(bezierUp, bezierForward));
	}

	// Handle holes and rings
	float holeShrink = 1;
	if (type == SPS_TYPE_RING_TWOWAY || type == SPS_TYPE_RING_ONEWAY) {
		if (bakedVertex.z >= curveLength) {
			// Straighten if past socket
			bezierPos += (bakedVertex.z - curveLength) * bezierForward;
		}
	} else {
		const float holeRecessDistance = worldLength * 0.05;
		const float holeRecessDistance2 = worldLength * 0.1;
		holeShrink = sps_saturated_map(
			bakedVertex.z,
			curveLength + holeRecessDistance2,
			curveLength + holeRecessDistance
		);
		if(_SPS_Overrun > 0) {
			if (bakedVertex.z >= curveLength + holeRecessDistance2) {
				// If way past socket, condense to point
				bezierPos += holeRecessDistance2 * bezierForward;
			} else if (bakedVertex.z >= curveLength) {
				// Straighten if past socket
				bezierPos += (bakedVertex.z - curveLength) * bezierForward;
			}
		}
	}

	float3 deformedVertex = bezierPos + bezierRight * bakedVertex.x * holeShrink + bezierUp * bakedVertex.y * holeShrink;
	vertex.xyz = lerp(origVertex, deformedVertex, dumbLerp);
	if (length(bakedNormal) != 0) {
		float3 deformedNormal = bezierRight * bakedNormal.x + bezierUp * bakedNormal.y + bezierForward * bakedNormal.z;
		normal.xyz = lerp(origNormal, deformedNormal, dumbLerp);
	}
	if (length(bakedTangent) != 0) {
		float3 deformedTangent = bezierRight * bakedTangent.x + bezierUp * bakedTangent.y + bezierForward * bakedTangent.z;
		tangent.xyz = lerp(origTangent, deformedTangent, dumbLerp);
	}
}

void sps_apply(inout SpsInputs o) {

	// When VERTEXLIGHT_ON is missing, there are no lights nearby, and the 4light arrays will be full of junk
	// Temporarily disable this check since apparently it causes some passes to not apply SPS
	//#ifdef VERTEXLIGHT_ON
	sps_apply_real(
		o.SPS_STRUCT_POSITION_NAME,
		o.SPS_STRUCT_NORMAL_NAME,
		o.SPS_STRUCT_TANGENT_NAME,
		o.SPS_STRUCT_SV_VertexID_NAME,
		o.SPS_STRUCT_COLOR_NAME
	);
	//#endif

}
