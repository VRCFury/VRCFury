Shader "Hidden/VRCFury/SpsResolver" {
    Properties {
        [Header(Flags)]
        [Toggle] _SPS_Legacy("Allow Legacy Lights", Float) = 1
        _SPS_Enabled("Apply Fraction", Float) = 1

        [Header(Tags)]
        _SPS_TagInclude1("Include 1", Float) = 0
        [Toggle] _SPS_TagInclude1Self("Include 1 Self", Float) = 0
        [Toggle] _SPS_TagInclude1Others("Include 1 Others", Float) = 0
        _SPS_TagInclude2("Include 2", Float) = 0
        [Toggle] _SPS_TagInclude2Self("Include 2 Self", Float) = 0
        [Toggle] _SPS_TagInclude2Others("Include 2 Others", Float) = 0
        _SPS_TagInclude3("Include 3", Float) = 0
        [Toggle] _SPS_TagInclude3Self("Include 3 Self", Float) = 0
        [Toggle] _SPS_TagInclude3Others("Include 3 Others", Float) = 0
        _SPS_TagInclude4("Include 4", Float) = 1337
        [Toggle] _SPS_TagInclude4Self("Include 4 Self", Float) = 1
        [Toggle] _SPS_TagInclude4Others("Include 4 Others", Float) = 1
        _SPS_TagExclude1("Exclude 1", Float) = 0
        [Toggle] _SPS_TagExclude1Self("Exclude 1 Self", Float) = 0
        [Toggle] _SPS_TagExclude1Others("Exclude 1 Others", Float) = 0
        _SPS_TagExclude2("Exclude 2", Float) = 0
        [Toggle] _SPS_TagExclude2Self("Exclude 2 Self", Float) = 0
        [Toggle] _SPS_TagExclude2Others("Exclude 2 Others", Float) = 0
        _SPS_TagExclude3("Exclude 3", Float) = 0
        [Toggle] _SPS_TagExclude3Self("Exclude 3 Self", Float) = 0
        [Toggle] _SPS_TagExclude3Others("Exclude 3 Others", Float) = 0
        _SPS_TagExclude4("Exclude 4", Float) = 0
        [Toggle] _SPS_TagExclude4Self("Exclude 4 Self", Float) = 0
        [Toggle] _SPS_TagExclude4Others("Exclude 4 Others", Float) = 0

        [Header(Unique ID)]
        _SPS_Configured("ID Configured", Float) = 0
        _SPS_Id("ID", Float) = 0
        _SPS_PlayerId("Player ID", Float) = 0

        [Header(Bake)]
        _SPS_BakedLength("Baked length (m)", Float) = 0
        _SPS_BakedRadius("Baked radius (m)", Float) = 0
    }
    SubShader {
        Tags {
            "Queue" = "Background-944"
            "RenderType" = "Opaque"
            "IgnoreProjector" = "True"
            "VRCFallback" = "Hidden"
        }
        GrabPass { "_VFGrid56" }
        Pass {
            Tags { "LightMode" = "ForwardBase" }
            Cull Off
            ZWrite Off
            ZTest Always
            Blend One Zero
            ColorMask RGBA

            CGPROGRAM
            #pragma target 4.0
            #pragma vertex vert
            #pragma geometry geom
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "../common/sps_cell_layout.cginc"
            SPS_INIT_TEX(_VFGrid56)

            #include "sps_resolver_types.cginc"
            #include "sps_resolver_shader_types.cginc"
            #include "sps_resolver_geom.cginc"
            #include "sps_resolver_frag.cginc"

            v2g vert(appdata v) {
                UNITY_SETUP_INSTANCE_ID(v);
                v2g o;
                o.vertex = v.vertex;
                UNITY_TRANSFER_INSTANCE_ID(v, o);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                return o;
            }

            float4 frag(v2f input) : SV_Target {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                SpsTexture tex = SPS_GET_TEX(_VFGrid56);
                return sps_resolver_frag(tex, input);
            }
            ENDCG
        }
    }
}
