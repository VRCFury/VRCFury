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
	inout SPS_VANILLA_VERT_PARAM_TYPE input,
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
	float worldLength = resolvedCell.read_float(sps_cell_pixel_index_from_payload_index(SPS_RESOLVER_METADATA_LENGTH_INDEX));
	float bakeScale = sps_cell_header_scale(resolvedCell);
	bakedVertex *= bakeScale;

	#ifdef SPS_MODIFY_BAKE
		float3 bakedWorldOrigin = sps_cell_header_world(resolvedCell);
		float3 bakedWorldForward = sps_normalize(sps_cell_header_forward(resolvedCell));
		float3 bakedWorldUp = sps_nearest_normal(bakedWorldForward, sps_cell_header_up(resolvedCell));
		float3 bakedWorldRight = sps_normalize(cross(bakedWorldUp, bakedWorldForward));

		float socketDist = 0;
		float3 stop1Forward = sps_read_resolver_chain_forward(resolvedCell, 1);
		if (!sps_is_zero(stop1Forward)) {
			float3 rootPos = sps_read_resolver_chain_world(resolvedCell, 0);
			float3 stop1Pos = sps_read_resolver_chain_world(resolvedCell, 1);
			socketDist = length(stop1Pos - rootPos);
		}

		#ifdef SPS_VANILLA_STRUCT_POSITION_NAME
			float3 bakedWorldVertex = bakedWorldOrigin + bakedWorldRight * bakedVertex.x + bakedWorldUp * bakedVertex.y + bakedWorldForward * bakedVertex.z;
			bakedWorldVertex = sps_toLocal(bakedWorldVertex);
			input.SPS_VANILLA_STRUCT_POSITION_NAME.xyz = bakedWorldVertex;
		#endif
		#ifdef SPS_VANILLA_STRUCT_NORMAL_NAME
			float3 bakedWorldNormal = bakedWorldRight * bakedNormal.x + bakedWorldUp * bakedNormal.y + bakedWorldForward * bakedNormal.z;
			bakedWorldNormal = sps_direction_toLocal(bakedWorldNormal);
			input.SPS_VANILLA_STRUCT_NORMAL_NAME.xyz = bakedWorldNormal;
		#endif
		#ifdef SPS_VANILLA_STRUCT_TANGENT_NAME
			float3 bakedWorldTangent = bakedWorldRight * bakedTangent.x + bakedWorldUp * bakedTangent.y + bakedWorldForward * bakedTangent.z;
			bakedWorldTangent = sps_direction_toLocal(bakedWorldTangent);
			input.SPS_VANILLA_STRUCT_TANGENT_NAME.xyz = bakedWorldTangent;
		#endif
		SPS_MODIFY_BAKE(input, socketDist, worldLength);
		#ifdef SPS_VANILLA_STRUCT_POSITION_NAME
			bakedWorldVertex = input.SPS_VANILLA_STRUCT_POSITION_NAME.xyz;
			bakedWorldVertex = sps_toWorld(bakedWorldVertex);
			float3 modifiedWorldVertex = bakedWorldVertex - bakedWorldOrigin;
			bakedVertex = float3(
				dot(modifiedWorldVertex, bakedWorldRight),
				dot(modifiedWorldVertex, bakedWorldUp),
				dot(modifiedWorldVertex, bakedWorldForward)
			);
		#endif
		#ifdef SPS_VANILLA_STRUCT_NORMAL_NAME
			bakedWorldNormal = input.SPS_VANILLA_STRUCT_NORMAL_NAME.xyz;
			bakedWorldNormal = sps_direction_toWorld(bakedWorldNormal);
			bakedNormal = float3(
				dot(bakedWorldNormal, bakedWorldRight),
				dot(bakedWorldNormal, bakedWorldUp),
				dot(bakedWorldNormal, bakedWorldForward)
			);
		#endif
		#ifdef SPS_VANILLA_STRUCT_TANGENT_NAME
			bakedWorldTangent = input.SPS_VANILLA_STRUCT_TANGENT_NAME.xyz;
			bakedWorldTangent = sps_direction_toWorld(bakedWorldTangent);
			bakedTangent = float3(
				dot(bakedWorldTangent, bakedWorldRight),
				dot(bakedWorldTangent, bakedWorldUp),
				dot(bakedWorldTangent, bakedWorldForward)
			);
		#endif
	#endif

	float firstSegmentLerp;
	float radiusMult = 1;
	float3 sampledWorldPos;
	float3 sampledWorldForward;
	float3 sampledWorldUp;
	float pathDistance = max(bakedVertex.z, 0);
	sps_deform_walk_chain(
		resolvedCell,
		worldLength,
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
		normal.xyz = lerp(origNormal, sps_direction_toLocal(deformedWorldNormal), dumbLerp);
	}
	if (!sps_is_zero(bakedTangent)) {
		float3 deformedWorldTangent = sampledWorldRight * bakedTangent.x + sampledWorldUp * bakedTangent.y + sampledWorldForward * bakedTangent.z;
		tangent.xyz = lerp(origTangent, sps_direction_toLocal(deformedWorldTangent), dumbLerp);
	}
}

void sps_apply(inout SpsInputs o) {

	// When VERTEXLIGHT_ON is missing, there are no lights nearby, and the 4light arrays will be full of junk
	// Temporarily disable this check since apparently it causes some passes to not apply SPS
	//#ifdef VERTEXLIGHT_ON
	SPS_VANILLA_VERT_PARAM_TYPE input = (SPS_VANILLA_VERT_PARAM_TYPE)o;
	sps_apply_real(
		input,
		o.SPS_STRUCT_POSITION_NAME,
		o.SPS_STRUCT_NORMAL_NAME,
		o.SPS_STRUCT_TANGENT_NAME,
		o.SPS_STRUCT_SV_VertexID_NAME
	);
	//#endif

}

#endif
