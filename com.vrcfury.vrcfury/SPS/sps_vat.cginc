// helper function to get current frame and next frames offset
float2 sps_vat_get_uv_offset(float frame, float y_cord, float x_cord)
{
    // progress describes how far the animation is along the VAT, from 0 to 1
    float progress = frame * (1/_SPS_VAT_FrameCount);
    float shifted_y_uv = 1 - (y_cord + progress);
    // uv postition with shifted offset on the y for current frame
    float2 frame_uv = float2(x_cord, shifted_y_uv);

    return frame_uv;
}
// both normal and optional tangents are stored in the same rotation map, this decodes that for each case
float3 sps_vat_decode_rotation_texture(float4 data, float3 key)
{
    float3 x1 = data.w * key;
    float3 x2 = cross(data.xyz, key);
    float3 x3 = x1 + x2;
    float3 x4 = cross(data.xyz, x3) * (2,2,2) + key;
    return x4;
}

void sps_vertex_animation_texture(inout float3 vertex, inout float3 normal, inout float3 tangent, in float2 uv, in float pen_amt)
{
    float x_uv_cord = uv.x;
    float y_uv_cord = (1 - uv.y);

    // find active frame
    float active_frame = floor(pen_amt * (_SPS_VAT_FrameCount - 1));

    // modulo by the frame range to find current frame, modified removes one to skip 1st frame
    float current_frame = fmod((active_frame), _SPS_VAT_FrameCount);
    // don't sub to find next frame
    float next_frame = fmod(active_frame+1, _SPS_VAT_FrameCount);

    // get the offset for curret and next frame
    float2 current_frame_uv = sps_vat_get_uv_offset(current_frame, y_uv_cord, x_uv_cord);
    float2 next_frame_uv = sps_vat_get_uv_offset(next_frame, y_uv_cord, x_uv_cord);

    // sample positiion texture with offset uv for current and next frame
    float4 pos_offset = tex2Dlod(_SPS_VAT_PosTexture, float4(current_frame_uv, 0, 0));
    float4 next_pos_offset = tex2Dlod(_SPS_VAT_PosTexture, float4(next_frame_uv, 0, 0));

    // sample rotation texture for normal
    float4 rotation_sample = tex2Dlod(_SPS_VAT_RotTexture, float4(current_frame_uv, 0, 0));
    float4 next_rotation_sample = tex2Dlod(_SPS_VAT_RotTexture, float4(next_frame_uv, 0, 0));

    // normal map from rotation texture
    float3 normal_offset = sps_vat_decode_rotation_texture(rotation_sample, float3(0,1,0));
    float3 next_normal_offset = sps_vat_decode_rotation_texture(next_rotation_sample, float3(0,1,0));

    // tangnet map from rotation texture
    float3 tangent_offset = sps_vat_decode_rotation_texture(rotation_sample, float3(-1,0,0));
    float3 next_tangent_offset = sps_vat_decode_rotation_texture(next_rotation_sample, float3(-1,0,0));

    // apply the offset to mesh

    // step function for conditonals,
    // 0 if false, 1 if true, using the modified houdini exporter, static points uv's are on the left side and should be ignored
    float static_point_conditional = step(0.0001, uv.x);

    // no interpolation
    if (!_SPS_VAT_Interpolate || (current_frame != 0 && next_frame == 0))
    {
        if (static_point_conditional) // only effect points that aren't static in the baked animation
        {
            vertex += pos_offset.xyz;
            normal = normalize(normal_offset);
            tangent = normalize(tangent_offset);
        }
        
    }
    // interpolation
    else
    {
        float interpolation_alpha = 0;
        interpolation_alpha = pen_amt * (_SPS_VAT_FrameCount);

        // find the fractional value for the lerp amount
        interpolation_alpha = frac(interpolation_alpha);
        // apply lerp value to offset
        float3 lerp_pos = lerp(pos_offset.xyz, next_pos_offset.xyz, interpolation_alpha);
        float3 lerp_normal = lerp(normal_offset, next_normal_offset, interpolation_alpha);
        float3 lerp_tangent = lerp(tangent_offset, next_tangent_offset, interpolation_alpha);

        if (static_point_conditional) // only effect points that aren't static in the baked animation
        {
            vertex += lerp_pos;
            normal = normalize(lerp_normal);
            tangent = normalize(lerp_tangent);
        }
    }
}