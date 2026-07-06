Shader "Hidden/VRCFury/SpsSceneViewRestore" {
    Properties {
        _VRCFurySceneViewSpsSaved("Saved Scene View", 2D) = "black" {}
    }
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
            #pragma vertex vert_restore
            #pragma fragment frag_restore
            #include "UnityCG.cginc"

            sampler2D _VRCFurySceneViewSpsSaved;

            struct appdata {
                float4 vertex : POSITION;
            };

            struct v2f {
                float4 vertex : SV_POSITION;
                float4 grabPos : TEXCOORD0;
            };

            v2f vert_restore(appdata v) {
                v2f o;
                o.vertex = float4(v.vertex.xy, 0, 1);
                o.grabPos = ComputeGrabScreenPos(o.vertex);
                return o;
            }

            fixed4 frag_restore(v2f i) : SV_Target {
                fixed4 col = tex2Dproj(_VRCFurySceneViewSpsSaved, UNITY_PROJ_COORD(i.grabPos));
                col.a = 1;
                return col;
            }
            ENDCG
        }
    }
}
