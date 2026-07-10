Shader "Hidden/VRCFury/SpsResolver" {
    Properties {
        [Header(Flags)]
        [Toggle] _SPS_Legacy("Allow Legacy Lights", Float) = 1
        _SPS_Enabled("Apply Fraction", Float) = 1

        [Header(Tags)]
        _SPS_TagInclude1Low("Include 1 Low", Float) = 0
        _SPS_TagInclude1High("Include 1 High", Float) = 0
        [Toggle] _SPS_TagInclude1Self("Include 1 Self", Float) = 0
        [Toggle] _SPS_TagInclude1Others("Include 1 Others", Float) = 0
        _SPS_TagInclude2Low("Include 2 Low", Float) = 0
        _SPS_TagInclude2High("Include 2 High", Float) = 0
        [Toggle] _SPS_TagInclude2Self("Include 2 Self", Float) = 0
        [Toggle] _SPS_TagInclude2Others("Include 2 Others", Float) = 0
        _SPS_TagInclude3Low("Include 3 Low", Float) = 0
        _SPS_TagInclude3High("Include 3 High", Float) = 0
        [Toggle] _SPS_TagInclude3Self("Include 3 Self", Float) = 0
        [Toggle] _SPS_TagInclude3Others("Include 3 Others", Float) = 0
        _SPS_TagInclude4Low("Include 4 Low", Float) = 1337
        _SPS_TagInclude4High("Include 4 High", Float) = 0
        [Toggle] _SPS_TagInclude4Self("Include 4 Self", Float) = 1
        [Toggle] _SPS_TagInclude4Others("Include 4 Others", Float) = 1
        _SPS_TagExclude1Low("Exclude 1 Low", Float) = 0
        _SPS_TagExclude1High("Exclude 1 High", Float) = 0
        [Toggle] _SPS_TagExclude1Self("Exclude 1 Self", Float) = 0
        [Toggle] _SPS_TagExclude1Others("Exclude 1 Others", Float) = 0
        _SPS_TagExclude2Low("Exclude 2 Low", Float) = 0
        _SPS_TagExclude2High("Exclude 2 High", Float) = 0
        [Toggle] _SPS_TagExclude2Self("Exclude 2 Self", Float) = 0
        [Toggle] _SPS_TagExclude2Others("Exclude 2 Others", Float) = 0
        _SPS_TagExclude3Low("Exclude 3 Low", Float) = 0
        _SPS_TagExclude3High("Exclude 3 High", Float) = 0
        [Toggle] _SPS_TagExclude3Self("Exclude 3 Self", Float) = 0
        [Toggle] _SPS_TagExclude3Others("Exclude 3 Others", Float) = 0
        _SPS_TagExclude4Low("Exclude 4 Low", Float) = 0
        _SPS_TagExclude4High("Exclude 4 High", Float) = 0
        [Toggle] _SPS_TagExclude4Self("Exclude 4 Self", Float) = 0
        [Toggle] _SPS_TagExclude4Others("Exclude 4 Others", Float) = 0

        [Header(Unique ID)]
        _SPS_Configured("ID Configured", Float) = 0
        _SPS_IdLow("ID Low", Float) = 0
        _SPS_IdHigh("ID High", Float) = 0
        _SPS_PlayerIdLow("Player ID Low", Float) = 0
        _SPS_PlayerIdHigh("Player ID High", Float) = 0

        [Header(Bake)]
        _SPS_BakedLength("Baked length (m)", Float) = 0
        _SPS_BakedRadius("Baked radius (m)", Float) = 0
        [HDR]_SPS_MetadataColor("Metadata color", Color) = (0,0,0,0)
        _SPS_BakedRadiusSamples0("Baked radius samples 0", Vector) = (0,0,0,0)
        _SPS_BakedRadiusSamples1("Baked radius samples 1", Vector) = (0,0,0,0)
        _SPS_BakedRadiusSamples2("Baked radius samples 2", Vector) = (0,0,0,0)
        _SPS_BakedRadiusSamples3("Baked radius samples 3", Vector) = (0,0,0,0)
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
            #pragma exclude_renderers metal
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
    SubShader {
        Tags {
            "Queue" = "Background-944"
            "RenderType" = "Opaque"
            "IgnoreProjector" = "True"
            "VRCFallback" = "Hidden"
        }
        Pass {
            Cull Off
            ZWrite Off
            ZTest Always
            ColorMask 0

            CGPROGRAM
            #pragma target 2.0
            #pragma vertex vert
            #pragma fragment frag
            #pragma only_renderers metal

            struct appdata {
                float4 vertex : POSITION;
            };

            struct v2f {
                float4 vertex : SV_POSITION;
            };

            v2f vert(appdata v) {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                return o;
            }

            float4 frag(v2f input) : SV_Target {
                return 0;
            }
            ENDCG
        }
    }
}
