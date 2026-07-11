Shader "Hidden/VRCFury/SpsSocketMarker" {
    Properties {
        [Header(Flags)]
        [Toggle] _SPS_SocketHole("Hole", Float) = 0
        [Toggle] _SPS_SocketDoubleSided("Double Sided", Float) = 0
        //[Toggle] _SPS_SocketPortal("Portal", Float) = 0
        [Toggle] _SPS_SocketRadiusOffset("Radius Offset", Float) = 0
        [Toggle] _SPS_SocketUnlockLocalX("Unlock Local X", Float) = 0
        [Toggle] _SPS_SocketUnlockAll("Unlock All", Float) = 0
        _SPS_GuidedTargetIdLow("Guided Target Id Low", Float) = 0
        _SPS_GuidedTargetIdHigh("Guided Target Id High", Float) = 0
        [Toggle] _SPS_SocketUseTangentIn("Use Tangent In", Float) = 0
        [Toggle] _SPS_SocketUseTangentOut("Use Tangent Out", Float) = 0
        _SPS_SocketTangentIn("Tangent In", Vector) = (0,0,0,0)
        _SPS_SocketTangentOut("Tangent Out", Vector) = (0,0,0,0)
        [Header(Tags)]
        _SPS_SocketTag1Low("Tag 1 Low", Float) = 0
        _SPS_SocketTag1High("Tag 1 High", Float) = 0
        _SPS_SocketTag2Low("Tag 2 Low", Float) = 0
        _SPS_SocketTag2High("Tag 2 High", Float) = 0
        _SPS_SocketTag3Low("Tag 3 Low", Float) = 0
        _SPS_SocketTag3High("Tag 3 High", Float) = 0
        _SPS_SocketTag4Low("Tag 4 Low", Float) = 0
        _SPS_SocketTag4High("Tag 4 High", Float) = 0
        _SPS_SocketTag5Low("Tag 5 Low", Float) = 0
        _SPS_SocketTag5High("Tag 5 High", Float) = 0
        _SPS_SocketTag6Low("Tag 6 Low", Float) = 0
        _SPS_SocketTag6High("Tag 6 High", Float) = 0
        _SPS_SocketTag7Low("Tag 7 Low", Float) = 0
        _SPS_SocketTag7High("Tag 7 High", Float) = 0
        _SPS_SocketTag8Low("Tag 8 Low", Float) = 1337
        _SPS_SocketTag8High("Tag 8 High", Float) = 0
        [Header(Unique ID)]
        _SPS_Configured("ID Configured", Float) = 0
        _SPS_IdLow("ID Low", Float) = 0
        _SPS_IdHigh("ID High", Float) = 0
        _SPS_PlayerIdLow("Player ID Low", Float) = 0
        _SPS_PlayerIdHigh("Player ID High", Float) = 0
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
            #pragma exclude_renderers metal
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
                UNITY_DEFINE_INSTANCED_PROP(float, _SPS_SocketUnlockLocalX)
                UNITY_DEFINE_INSTANCED_PROP(float, _SPS_SocketUnlockAll)
                UNITY_DEFINE_INSTANCED_PROP(float, _SPS_GuidedTargetIdLow)
                UNITY_DEFINE_INSTANCED_PROP(float, _SPS_GuidedTargetIdHigh)
                UNITY_DEFINE_INSTANCED_PROP(float, _SPS_SocketUseTangentIn)
                UNITY_DEFINE_INSTANCED_PROP(float, _SPS_SocketUseTangentOut)
                UNITY_DEFINE_INSTANCED_PROP(float4, _SPS_SocketTangentIn)
                UNITY_DEFINE_INSTANCED_PROP(float4, _SPS_SocketTangentOut)
                UNITY_DEFINE_INSTANCED_PROP(float, _SPS_SocketTag1Low)
                UNITY_DEFINE_INSTANCED_PROP(float, _SPS_SocketTag1High)
                UNITY_DEFINE_INSTANCED_PROP(float, _SPS_SocketTag2Low)
                UNITY_DEFINE_INSTANCED_PROP(float, _SPS_SocketTag2High)
                UNITY_DEFINE_INSTANCED_PROP(float, _SPS_SocketTag3Low)
                UNITY_DEFINE_INSTANCED_PROP(float, _SPS_SocketTag3High)
                UNITY_DEFINE_INSTANCED_PROP(float, _SPS_SocketTag4Low)
                UNITY_DEFINE_INSTANCED_PROP(float, _SPS_SocketTag4High)
                UNITY_DEFINE_INSTANCED_PROP(float, _SPS_SocketTag5Low)
                UNITY_DEFINE_INSTANCED_PROP(float, _SPS_SocketTag5High)
                UNITY_DEFINE_INSTANCED_PROP(float, _SPS_SocketTag6Low)
                UNITY_DEFINE_INSTANCED_PROP(float, _SPS_SocketTag6High)
                UNITY_DEFINE_INSTANCED_PROP(float, _SPS_SocketTag7Low)
                UNITY_DEFINE_INSTANCED_PROP(float, _SPS_SocketTag7High)
                UNITY_DEFINE_INSTANCED_PROP(float, _SPS_SocketTag8Low)
                UNITY_DEFINE_INSTANCED_PROP(float, _SPS_SocketTag8High)
            UNITY_INSTANCING_BUFFER_END(SpsSocketProps)

            #define SPS_SOCKET_PROP(name) UNITY_ACCESS_INSTANCED_PROP(SpsSocketProps, name)
            #define _SPS_SocketHole SPS_SOCKET_PROP(_SPS_SocketHole)
            #define _SPS_SocketDoubleSided SPS_SOCKET_PROP(_SPS_SocketDoubleSided)
            #define _SPS_SocketPortal SPS_SOCKET_PROP(_SPS_SocketPortal)
            #define _SPS_SocketRadiusOffset SPS_SOCKET_PROP(_SPS_SocketRadiusOffset)
            #define _SPS_SocketUnlockLocalX SPS_SOCKET_PROP(_SPS_SocketUnlockLocalX)
            #define _SPS_SocketUnlockAll SPS_SOCKET_PROP(_SPS_SocketUnlockAll)
            #define _SPS_GuidedTargetIdLow SPS_SOCKET_PROP(_SPS_GuidedTargetIdLow)
            #define _SPS_GuidedTargetIdHigh SPS_SOCKET_PROP(_SPS_GuidedTargetIdHigh)
            #define _SPS_GuidedTargetId SPS_MERGE_SPLIT(_SPS_GuidedTargetId)
            #define _SPS_SocketUseTangentIn SPS_SOCKET_PROP(_SPS_SocketUseTangentIn)
            #define _SPS_SocketUseTangentOut SPS_SOCKET_PROP(_SPS_SocketUseTangentOut)
            #define _SPS_SocketTangentIn SPS_SOCKET_PROP(_SPS_SocketTangentIn)
            #define _SPS_SocketTangentOut SPS_SOCKET_PROP(_SPS_SocketTangentOut)
            #define _SPS_SocketTag1Low SPS_SOCKET_PROP(_SPS_SocketTag1Low)
            #define _SPS_SocketTag1High SPS_SOCKET_PROP(_SPS_SocketTag1High)
            #define _SPS_SocketTag1 SPS_MERGE_SPLIT(_SPS_SocketTag1)
            #define _SPS_SocketTag2Low SPS_SOCKET_PROP(_SPS_SocketTag2Low)
            #define _SPS_SocketTag2High SPS_SOCKET_PROP(_SPS_SocketTag2High)
            #define _SPS_SocketTag2 SPS_MERGE_SPLIT(_SPS_SocketTag2)
            #define _SPS_SocketTag3Low SPS_SOCKET_PROP(_SPS_SocketTag3Low)
            #define _SPS_SocketTag3High SPS_SOCKET_PROP(_SPS_SocketTag3High)
            #define _SPS_SocketTag3 SPS_MERGE_SPLIT(_SPS_SocketTag3)
            #define _SPS_SocketTag4Low SPS_SOCKET_PROP(_SPS_SocketTag4Low)
            #define _SPS_SocketTag4High SPS_SOCKET_PROP(_SPS_SocketTag4High)
            #define _SPS_SocketTag4 SPS_MERGE_SPLIT(_SPS_SocketTag4)
            #define _SPS_SocketTag5Low SPS_SOCKET_PROP(_SPS_SocketTag5Low)
            #define _SPS_SocketTag5High SPS_SOCKET_PROP(_SPS_SocketTag5High)
            #define _SPS_SocketTag5 SPS_MERGE_SPLIT(_SPS_SocketTag5)
            #define _SPS_SocketTag6Low SPS_SOCKET_PROP(_SPS_SocketTag6Low)
            #define _SPS_SocketTag6High SPS_SOCKET_PROP(_SPS_SocketTag6High)
            #define _SPS_SocketTag6 SPS_MERGE_SPLIT(_SPS_SocketTag6)
            #define _SPS_SocketTag7Low SPS_SOCKET_PROP(_SPS_SocketTag7Low)
            #define _SPS_SocketTag7High SPS_SOCKET_PROP(_SPS_SocketTag7High)
            #define _SPS_SocketTag7 SPS_MERGE_SPLIT(_SPS_SocketTag7)
            #define _SPS_SocketTag8Low SPS_SOCKET_PROP(_SPS_SocketTag8Low)
            #define _SPS_SocketTag8High SPS_SOCKET_PROP(_SPS_SocketTag8High)
            #define _SPS_SocketTag8 SPS_MERGE_SPLIT(_SPS_SocketTag8)

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
                uint nextId = _SPS_GuidedTargetId;
                float3 tangentIn = sps_to_bool(_SPS_SocketUseTangentIn) ? sps_toWorld(_SPS_SocketTangentIn.xyz) : 0;
                float3 tangentOut = sps_to_bool(_SPS_SocketUseTangentOut) ? sps_toWorld(_SPS_SocketTangentOut.xyz) : 0;
                uint tagValues[SPS_SOCKET_PAYLOAD_TAG_COUNT] = {
                    _SPS_SocketTag1, _SPS_SocketTag2, _SPS_SocketTag3, _SPS_SocketTag4,
                    _SPS_SocketTag5, _SPS_SocketTag6, _SPS_SocketTag7, _SPS_SocketTag8
                };
                uint tags[SPS_SOCKET_PAYLOAD_TAG_COUNT];
                [unroll]
                for (uint tagIndex = 0u; tagIndex < SPS_SOCKET_PAYLOAD_TAG_COUNT; tagIndex++) {
                    tags[tagIndex] = tagValues[tagIndex];
                }
                uint flags = 0u;
                float flagValues[6] = {
                    _SPS_SocketHole, _SPS_SocketDoubleSided, _SPS_SocketPortal, _SPS_SocketRadiusOffset,
                    _SPS_SocketUnlockLocalX, _SPS_SocketUnlockAll
                };
                uint flagMasks[6] = {
                    SPS_SOCKET_FLAG_HOLE, SPS_SOCKET_FLAG_DOUBLE_SIDED, SPS_SOCKET_FLAG_PORTAL, SPS_SOCKET_FLAG_RADIUS_OFFSET,
                    SPS_SOCKET_FLAG_UNLOCK_LOCAL_X, SPS_SOCKET_FLAG_UNLOCK_ALL
                };
                [unroll]
                for (uint flagIndex = 0u; flagIndex < 6u; flagIndex++) {
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

    Fallback Off
}
