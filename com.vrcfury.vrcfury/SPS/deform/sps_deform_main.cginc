#ifdef SHADER_TARGET_SURFACE_ANALYSIS
void sps_apply(inout SpsInputs o){}
#else

#include "sps_deform_globals.cginc"
#include "../resolver/sps_resolver_payload.cginc"
#include "../common/sps_utils.cginc"
#include "sps_deform_bake.cginc"
#include "sps_deform_control_points.cginc"
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
	bool hasResolvedPath = sps_try_find_cell(
		resolverTex,
		sps_hash_id(resolverId, resolverPlayerId),
		resolverId,
		resolverPlayerId,
		SPS_PRODUCT_PLUG,
		resolverSlotIndex
	);
	if (!hasResolvedPath) return;
	SpsCell resolvedCell = sps_get_cell(resolverTex, resolverSlotIndex);
	float currentLength = resolvedCell.read_float(sps_cell_pixel_index_from_payload_index(SPS_RESOLVER_METADATA_LENGTH_INDEX));
	float bakeScale = sps_cell_header_scale(resolvedCell);

	bakedVertex *= bakeScale;

	float firstSegmentLerp;
	float radiusMult = 1;
	float3 sampledWorldPos;
	float3 sampledWorldForward;
	float3 sampledWorldUp;
	float pathDistance = max(bakedVertex.z, 0);
	sps_deform_walk_chain(
		resolvedCell,
		currentLength,
		pathDistance,
		firstSegmentLerp,
		radiusMult,
		sampledWorldPos,
		sampledWorldForward,
		sampledWorldUp
	);
	float dumbLerp = sps_saturated_map(firstSegmentLerp, 0, 0.2) * active;
	float3 sampledWorldRight = cross(sampledWorldUp, sampledWorldForward);

	float3 deformedWorldVertex = sampledWorldPos + sampledWorldRight * bakedVertex.x * radiusMult + sampledWorldUp * bakedVertex.y * radiusMult;
	vertex.xyz = lerp(origVertex, sps_toLocal(deformedWorldVertex), dumbLerp);
	if (!sps_is_zero(bakedNormal)) {
		float3 deformedWorldNormal = sampledWorldRight * bakedNormal.x + sampledWorldUp * bakedNormal.y + sampledWorldForward * bakedNormal.z;
		normal.xyz = lerp(origNormal, sps_normalize(sps_direction_toLocal(deformedWorldNormal)), dumbLerp);
	}
	if (!sps_is_zero(bakedTangent)) {
		float3 deformedWorldTangent = sampledWorldRight * bakedTangent.x + sampledWorldUp * bakedTangent.y + sampledWorldForward * bakedTangent.z;
		tangent.xyz = lerp(origTangent, sps_normalize(sps_direction_toLocal(deformedWorldTangent)), dumbLerp);
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
