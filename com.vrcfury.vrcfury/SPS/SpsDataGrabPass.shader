Shader "Hidden/VRCFury/SpsDataGrabPass" {
    Properties {
        _SPS_Configured("ID Configured", Float) = 0
        _SPS_IdLow("ID Low", Float) = 0
        _SPS_IdHigh("ID High", Float) = 0
        _SPS_PlayerIdLow("Player ID Low", Float) = 0
        _SPS_PlayerIdHigh("Player ID High", Float) = 0
    }
    SubShader {
        Tags {
            "Queue" = "Background-940"
            "RenderType" = "Opaque"
            "IgnoreProjector" = "True"
            "VRCFallback" = "Hidden"
        }
        GrabPass { "_VFGridFinal" }

        Pass {
            Cull Off
            ZWrite Off
            ZTest Always
            Blend One Zero
            ColorMask RGBA
            CGPROGRAM
            #pragma vertex vert
            #pragma geometry geom
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "common/sps_texture.cginc"
            #include "common/sps_cell_frag.cginc"
            #include "common/sps_cell_geom.cginc"
            #include "common/sps_id.cginc"

            struct appdata {
                float4 vertex : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            struct v2g {
                float4 vertex : SV_POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };
            struct g2f {
                float4 vertex : SV_POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
                nointerpolation int cellIndex : TEXCOORD0;
            };

            v2g vert (appdata v) {
                UNITY_SETUP_INSTANCE_ID(v);
                v2g o;
                o.vertex = v.vertex;
                UNITY_TRANSFER_INSTANCE_ID(v, o);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                return o;
            }

            [maxvertexcount((SPS_CELL_REPLICA_COUNT + 1) * 3)]
            void geom(triangle v2g input[3], inout TriangleStream<g2f> stream) {
                UNITY_SETUP_INSTANCE_ID(input[0]);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input[0]);
                if (sps_should_abort()) return;

                g2f o;
                UNITY_TRANSFER_INSTANCE_ID(input[0], o);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                SPS_CELL_GEOM(o, stream)
            }

            fixed4 frag (g2f i) : SV_Target {
                UNITY_SETUP_INSTANCE_ID(i);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                if (sps_should_abort()) clip(-1);
                return 0;
            }
            ENDCG
        }
    }
}
