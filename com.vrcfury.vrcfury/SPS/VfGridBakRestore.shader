Shader "Hidden/VRCFury/VFGridBakRestore" {
    SubShader {
        Tags {
            "Queue" = "Background-939"
            "RenderType" = "Opaque"
            "IgnoreProjector" = "True"
            "VRCFallback" = "Hidden"
        }

        Pass {
            Cull Off
            ZWrite Off
            ZTest Always
            Blend One Zero

            CGPROGRAM
            #pragma target 4.0
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            #include "common/sps_texture.cginc"
            #include "common/sps_encode.cginc"

            SPS_INIT_TEX(_VFGridBak)

            struct v2f {
                float4 vertex : SV_POSITION;
            };

            v2f vert(uint vertexId : SV_VertexID) {
                v2f o;
                float2 pos = float2(
                    vertexId == 2 ? 3.0 : -1.0,
                    vertexId == 1 ? 3.0 : -1.0
                );
                o.vertex = float4(pos, 0, 1);
                return o;
            }

            fixed4 frag(v2f i) : SV_Target {
                SpsTexture tex = SPS_GET_TEX(_VFGridBak);
                uint2 pixel = uint2(
                    floor(i.vertex.x),
                    floor(_ScreenParams.y - i.vertex.y)
                );
                fixed4 col = SPS_READ_TEX(tex, pixel);
                col.a = 1;
                return col;
            }
            ENDCG
        }
    }

    Fallback Off
}
