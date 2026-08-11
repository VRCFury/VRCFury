Shader "Hidden/VRCFury/VFGridBakGrabPass" {
    SubShader {
        Tags {
            "Queue" = "Background-951"
            "RenderType" = "Opaque"
            "IgnoreProjector" = "True"
            "VRCFallback" = "Hidden"
        }

        GrabPass { "_VFGridBak" }

        Pass {
            Cull Off
            ZWrite Off
            ZTest Always
            Blend One Zero
            ColorMask 0

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

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

            fixed4 frag(v2f i) : SV_Target {
                return 0;
            }
            ENDCG
        }
    }

    Fallback Off
}
