Shader "Hidden/VRCFury/SpsSocketMarker" {
    Properties {
        [Header(Flags)]
        [Toggle] _SPS_SocketHole("Hole", Float) = 0
        [Toggle] _SPS_SocketDoubleSided("Double Sided", Float) = 0
        //[Toggle] _SPS_SocketPortal("Portal", Float) = 0
        [Toggle] _SPS_SocketRadiusOffset("Radius Offset", Float) = 0
        _SPS_SocketNextId("Restrict Next Socket Id", Float) = 0
        [Toggle] _SPS_SocketUseTangentIn("Use Tangent In", Float) = 0
        [Toggle] _SPS_SocketUseTangentOut("Use Tangent Out", Float) = 0
        _SPS_SocketTangentIn("Tangent In", Vector) = (0,0,0,0)
        _SPS_SocketTangentOut("Tangent Out", Vector) = (0,0,0,0)
        [Header(Tags)]
        _SPS_SocketTag1("Tag 1", Float) = 0
        _SPS_SocketTag2("Tag 2", Float) = 0
        _SPS_SocketTag3("Tag 3", Float) = 0
        _SPS_SocketTag4("Tag 4", Float) = 0
        _SPS_SocketTag5("Tag 5", Float) = 0
        _SPS_SocketTag6("Tag 6", Float) = 0
        _SPS_SocketTag7("Tag 7", Float) = 0
        _SPS_SocketTag8("Tag 8", Float) = 1337
        [Header(Unique ID)]
        _SPS_Configured("ID Configured", Float) = 0
        _SPS_Id("ID", Float) = 0
        _SPS_PlayerId("Player ID", Float) = 0
    }
    SubShader {
        Tags {
            "Queue" = "Background-948"
            "RenderType" = "Opaque"
            "IgnoreProjector" = "True"
            "VRCFallback" = "Hidden"
        }
        Pass {
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
            #include "../common/sps_cell_frag.cginc"
            #include "../common/sps_cell_geom.cginc"
            #include "../common/sps_cell_hash.cginc"
            #include "../common/sps_types.cginc"
            #include "../common/sps_utils.cginc"
            #include "sps_socket_frag.cginc"

            UNITY_INSTANCING_BUFFER_START(SpsSocketProps)
                UNITY_DEFINE_INSTANCED_PROP(float, _SPS_SocketHole)
                UNITY_DEFINE_INSTANCED_PROP(float, _SPS_SocketDoubleSided)
                UNITY_DEFINE_INSTANCED_PROP(float, _SPS_SocketPortal)
                UNITY_DEFINE_INSTANCED_PROP(float, _SPS_SocketRadiusOffset)
                UNITY_DEFINE_INSTANCED_PROP(float, _SPS_SocketNextId)
                UNITY_DEFINE_INSTANCED_PROP(float, _SPS_SocketUseTangentIn)
                UNITY_DEFINE_INSTANCED_PROP(float, _SPS_SocketUseTangentOut)
                UNITY_DEFINE_INSTANCED_PROP(float4, _SPS_SocketTangentIn)
                UNITY_DEFINE_INSTANCED_PROP(float4, _SPS_SocketTangentOut)
                UNITY_DEFINE_INSTANCED_PROP(float, _SPS_SocketTag1)
                UNITY_DEFINE_INSTANCED_PROP(float, _SPS_SocketTag2)
                UNITY_DEFINE_INSTANCED_PROP(float, _SPS_SocketTag3)
                UNITY_DEFINE_INSTANCED_PROP(float, _SPS_SocketTag4)
                UNITY_DEFINE_INSTANCED_PROP(float, _SPS_SocketTag5)
                UNITY_DEFINE_INSTANCED_PROP(float, _SPS_SocketTag6)
                UNITY_DEFINE_INSTANCED_PROP(float, _SPS_SocketTag7)
                UNITY_DEFINE_INSTANCED_PROP(float, _SPS_SocketTag8)
            UNITY_INSTANCING_BUFFER_END(SpsSocketProps)

            #define SPS_SOCKET_PROP(name) UNITY_ACCESS_INSTANCED_PROP(SpsSocketProps, name)
            #define _SPS_SocketHole SPS_SOCKET_PROP(_SPS_SocketHole)
            #define _SPS_SocketDoubleSided SPS_SOCKET_PROP(_SPS_SocketDoubleSided)
            #define _SPS_SocketPortal SPS_SOCKET_PROP(_SPS_SocketPortal)
            #define _SPS_SocketRadiusOffset SPS_SOCKET_PROP(_SPS_SocketRadiusOffset)
            #define _SPS_SocketNextId SPS_SOCKET_PROP(_SPS_SocketNextId)
            #define _SPS_SocketUseTangentIn SPS_SOCKET_PROP(_SPS_SocketUseTangentIn)
            #define _SPS_SocketUseTangentOut SPS_SOCKET_PROP(_SPS_SocketUseTangentOut)
            #define _SPS_SocketTangentIn SPS_SOCKET_PROP(_SPS_SocketTangentIn)
            #define _SPS_SocketTangentOut SPS_SOCKET_PROP(_SPS_SocketTangentOut)
            #define _SPS_SocketTag1 SPS_SOCKET_PROP(_SPS_SocketTag1)
            #define _SPS_SocketTag2 SPS_SOCKET_PROP(_SPS_SocketTag2)
            #define _SPS_SocketTag3 SPS_SOCKET_PROP(_SPS_SocketTag3)
            #define _SPS_SocketTag4 SPS_SOCKET_PROP(_SPS_SocketTag4)
            #define _SPS_SocketTag5 SPS_SOCKET_PROP(_SPS_SocketTag5)
            #define _SPS_SocketTag6 SPS_SOCKET_PROP(_SPS_SocketTag6)
            #define _SPS_SocketTag7 SPS_SOCKET_PROP(_SPS_SocketTag7)
            #define _SPS_SocketTag8 SPS_SOCKET_PROP(_SPS_SocketTag8)

            struct appdata {
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2g {
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            struct g2f {
                float4 vertex : SV_POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
                nointerpolation int cellIndex : TEXCOORD0;
                nointerpolation float3 rootWorld : TEXCOORD1;
                nointerpolation float3 normalWorld : TEXCOORD2;
                nointerpolation float3 upWorld : TEXCOORD3;
            };

            v2g vert(appdata v) {
                UNITY_SETUP_INSTANCE_ID(v);
                v2g o;
                o.uv = v.uv;
                UNITY_TRANSFER_INSTANCE_ID(v, o);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                return o;
            }

            [maxvertexcount((SPS_CELL_REPLICA_COUNT + 1) * 3)]
            void geom(triangle v2g input[3], inout TriangleStream<g2f> stream) {
                UNITY_SETUP_INSTANCE_ID(input[0]);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input[0]);
                if (sps_should_abort()) return;

                float3 rootWorld = sps_object_origin_world();
                float3 normalWorld = sps_object_forward_world();
                float3 upWorld = sps_object_up_world();

                g2f o;
                o.rootWorld = rootWorld;
                o.normalWorld = normalWorld;
                o.upWorld = upWorld;
                UNITY_TRANSFER_INSTANCE_ID(input[0], o);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                SPS_CELL_GEOM(o, stream)
            }

            float4 frag(g2f i) : SV_Target {
                UNITY_SETUP_INSTANCE_ID(i);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                uint pixelIndex;
                float4 rgba = 0;
                if (sps_cell_frag(
                    i.cellIndex,
                    i.vertex,
                    pixelIndex,
                    rgba
                )) return rgba;
                uint uniqueId = sps_id();
                if (uniqueId == 0u) uniqueId = sps_hash_world(i.rootWorld, 0u);
                uint playerId = sps_player_id();
                uint nextId = sps_to_uint(_SPS_SocketNextId);
                float3 tangentIn = sps_to_bool(_SPS_SocketUseTangentIn) ? sps_toWorld(_SPS_SocketTangentIn.xyz) : 0;
                float3 tangentOut = sps_to_bool(_SPS_SocketUseTangentOut) ? sps_toWorld(_SPS_SocketTangentOut.xyz) : 0;
                float tagValues[SPS_SOCKET_PAYLOAD_TAG_COUNT] = {
                    _SPS_SocketTag1, _SPS_SocketTag2, _SPS_SocketTag3, _SPS_SocketTag4,
                    _SPS_SocketTag5, _SPS_SocketTag6, _SPS_SocketTag7, _SPS_SocketTag8
                };
                uint tags[SPS_SOCKET_PAYLOAD_TAG_COUNT];
                [unroll]
                for (uint tagIndex = 0u; tagIndex < SPS_SOCKET_PAYLOAD_TAG_COUNT; tagIndex++) {
                    tags[tagIndex] = sps_to_uint(tagValues[tagIndex]);
                }
                uint flags = 0u;
                float flagValues[4] = { _SPS_SocketHole, _SPS_SocketDoubleSided, _SPS_SocketPortal, _SPS_SocketRadiusOffset };
                uint flagMasks[4] = { SPS_SOCKET_FLAG_HOLE, SPS_SOCKET_FLAG_DOUBLE_SIDED, SPS_SOCKET_FLAG_PORTAL, SPS_SOCKET_FLAG_RADIUS_OFFSET };
                [unroll]
                for (uint flagIndex = 0u; flagIndex < 4u; flagIndex++) {
                    if (sps_to_bool(flagValues[flagIndex])) flags |= flagMasks[flagIndex];
                }
                if (sps_try_get_slot_header_rgba(
                    pixelIndex,
                    uniqueId,
                    playerId,
                    SPS_PRODUCT_SOCKET,
                    i.rootWorld,
                    i.normalWorld,
                    i.upWorld,
                    sps_object_scale_world(),
                    0,
                    rgba
                )) return rgba;
                if (sps_try_get_socket_payload_rgba(
                    pixelIndex,
                    flags,
                    nextId,
                    tags,
                    tangentIn,
                    tangentOut,
                    rgba
                )) {
                    return rgba;
                }
                return 0;
            }
            ENDCG
        }
    }
}
