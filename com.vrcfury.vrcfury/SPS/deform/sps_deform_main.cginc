#ifdef SHADER_TARGET_SURFACE_ANALYSIS
void sps_apply(inout SpsInputs o){}
#else

#include "sps_deform_globals.cginc"
#include "../resolver/sps_resolver_payload.cginc"
#include "../common/sps_utils.cginc"
#include "sps_deform_bake.cginc"
#include "sps_deform_curve.cginc"

// SPS Penetration Shader
void sps_apply_real(
	inout SPS_STRUCT_POSITION_TYPE vertex,
	inout SPS_STRUCT_NORMAL_TYPE normal,
	inout SPS_STRUCT_TANGENT_TYPE tangent,
	uint vertexId
) {
	const float3 origVertex = vertex.xyz;
	const float3 origNormal = normal.xyz;
	const float3 origTangent = tangent.xyz;
	float3 bakedVertex;
	float3 bakedNormal;
	float3 bakedTangent;
	float active;
	SpsGetBakedPosition(vertexId, bakedVertex, bakedNormal, bakedTangent, active);

	if (active == 0) return;

	uint resolverId = sps_id();
	uint resolverPlayerId = sps_player_id();

	int resolverSlotIndex;
	SpsTexture resolverTex = SPS_GET_TEX(_VFGridFinal);
	SpsCell resolvedCell;
	bool hasResolvedPath = sps_try_find_cell(
		resolverTex,
		sps_hash_id(resolverId, resolverPlayerId),
		resolverId,
		resolverPlayerId,
		SPS_PRODUCT_PLUG,
		resolverSlotIndex,
		resolvedCell
	);
	if (!hasResolvedPath) return;
	float resolvedApplyLerp = saturate(resolvedCell.read_float(sps_cell_pixel_index_from_payload_index(SPS_RESOLVER_PAYLOAD_APPLY_LERP_INDEX)));
	float bakeScale = sps_cell_header_scale(resolvedCell);
	float currentLength = _SPS_BakedLength * bakeScale;

	bakedVertex *= (currentLength / _SPS_BakedLength);

	float applyLerp = resolvedApplyLerp;
	float dumbLerp = sps_saturated_map(applyLerp, 0, 0.2) * active;

	uint terminalFlags = 0u;
	float3 sampledWorldPos;
	float3 sampledWorldForward;
	float3 sampledWorldUp;
	float walkedLength = 0;
	float pathDistance = max(bakedVertex.z, 0);
	sps_deform_walk_chain(
		resolvedCell,
		currentLength,
		pathDistance,
		walkedLength,
		terminalFlags,
		sampledWorldPos,
		sampledWorldForward,
		sampledWorldUp
	);

	float overshootDistance = max(pathDistance - walkedLength, 0);
	if (overshootDistance > 0) {
		sampledWorldPos += sampledWorldForward * overshootDistance;
	}

	float3 localPos = sps_toLocal(sampledWorldPos);
	float3 localForward = sps_toLocal(sampledWorldPos + sampledWorldForward) - localPos;
	float3 localUp = sps_toLocal(sampledWorldPos + sampledWorldUp) - localPos;
	if (length(localForward) <= 0) localForward = float3(0, 0, 1);
	float3 bezierPos = localPos;
	float3 bezierForward = sps_normalize(localForward);
	float3 bezierUp = sps_nearest_normal(bezierForward, localUp);
	float3 bezierRight = sps_normalize(cross(bezierUp, bezierForward));

	float holeShrink = 1;
	if (sps_has_flag(terminalFlags, SPS_SOCKET_FLAG_HOLE)) {
		float collapseStart = currentLength * 0.05;
		float collapseEnd = currentLength * 0.1;
		if (overshootDistance > collapseEnd) {
			bezierPos -= bezierForward * (overshootDistance - collapseEnd);
		}
		holeShrink = sps_saturated_map(
			overshootDistance,
			collapseEnd,
			collapseStart
		);
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
		o.SPS_STRUCT_SV_VertexID_NAME
	);
	//#endif

}

#endif
