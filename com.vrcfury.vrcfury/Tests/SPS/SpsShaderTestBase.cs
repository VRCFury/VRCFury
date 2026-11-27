using System;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Rendering;
using UnityEngine;
using UnityEngine.Rendering;
using VF.Builder.Haptics;
using Object = UnityEngine.Object;

namespace VF.Tests {
    internal abstract class SpsShaderTestBase {
        protected const float DefaultPlugRadius = .02f;
        protected const float TaperFullRadiusLength = .05f;
        protected const float StationaryTaperLength = .1f;

        protected const float SmoothCollapseLength = .05f;

        protected Shader shader;
        protected Shader pathShader;
        protected Shader deformationShader;
        protected Shader alternateTaperShader;
        protected Material material;
        protected RenderTexture target;
        protected Texture2D pixels;
        protected Texture2D defaultProfile;

        [OneTimeSetUp]
        public void SetUp() {
#if !VRCSDK_HAS_MATERIAL_CONTACTS
            Assert.Ignore("SPS requires material contacts.");
#endif
            var source = @"
Shader ""Hidden/VRCFury/Tests/SpsBend"" {
    SubShader { Pass { ZTest Always Cull Off ZWrite Off
        HLSLPROGRAM
        #pragma target 4.5
        #pragma vertex vert_img
        #pragma fragment frag
        #include ""UnityCG.cginc""
        #include ""Packages/com.vrcfury.vrcfury/SPS/deform/sps_config.cginc""
        // Exercise both phases even when the project config uses a zero-length collapse.
        #undef SPS_TAPER_INSTANT_FULL_RADIUS_LENGTH
        #undef SPS_TAPER_INSTANT_COLLAPSE_LENGTH
        #define SPS_TAPER_INSTANT_FULL_RADIUS_LENGTH .05
        #define SPS_TAPER_INSTANT_COLLAPSE_LENGTH .05
        float _BendRoom;
        #undef SPS_BEND_ROOM
        #define SPS_BEND_ROOM _BendRoom
        float _AdvanceMax;
        #undef SPS_ENTRY_ADVANCE_MAX
        #define SPS_ENTRY_ADVANCE_MAX _AdvanceMax
        float _VerticalAngle;
        #undef SPS_CONE_VERTICAL_HALF_ANGLE_DEGREES
        #define SPS_CONE_VERTICAL_HALF_ANGLE_DEGREES _VerticalAngle
        float _HorizontalAngle;
        #undef SPS_CONE_HORIZONTAL_HALF_ANGLE_DEGREES
        #define SPS_CONE_HORIZONTAL_HALF_ANGLE_DEGREES _HorizontalAngle
        #include ""Packages/com.vrcfury.vrcfury/SPS/deform/sps_types.cginc""
        #include ""Packages/com.vrcfury.vrcfury/SPS/deform/sps_utils.cginc""
        #include ""Packages/com.vrchat.base/ShaderLibrary/VRCContacts.cginc""
        #define SPS_INC_GLOBALS
        float _SPS_OthersChannelTagCount, _SPS_OthersLegacyTags, _SPS_UseLegacyLights;
        #include ""Packages/com.vrcfury.vrcfury/SPS/deform/sps_contacts.cginc""
        float4 _TestLightAtten, _TestLightX, _TestLightZ;
        #define unity_4LightAtten0 _TestLightAtten
        #define unity_4LightPosX0 _TestLightX
        #define unity_4LightPosZ0 _TestLightZ
        #include ""Packages/com.vrcfury.vrcfury/SPS/deform/sps_lights.cginc""
        float _SPS_BakedLength, _SPS_BakedRadius, _SPS_BaseActivationRadiusOverride;
        Texture2D _SPS_Bake;
        float4 _SPS_Bake_TexelSize;
        float _SPS_BlendshapeVertCount, _SPS_BlendshapeCount, _SPS_RadiusProfileSamples;
        #include ""Packages/com.vrcfury.vrcfury/SPS/deform/sps_path.cginc""
        #include ""Packages/com.vrcfury.vrcfury/SPS/deform/sps_curve.cginc""
        float3 _Position, _Forward, _Up, _Exit, _Root, _Stop1, _Stop2;
        float _Length, _CaptureLength, _Guided, _Entrance, _Flags, _Operation, _BaseRadius;
        float _SampleDistance, _ContactIndex, _ProfileDistance;
        float4 path_frag(v2f_img input) : SV_Target {
            SpsDeformPathInfo path;
            bool valid = sps_try_get_path(path);
            float4x4 frame = sps_contact_frame();
            float3 up = sps_normalize(frame._m01_m11_m21);
            float3 origin = frame._m03_m13_m23;
            if (_Operation == 9) return float4(sps_path_worlds[1], valid);
            if (_Operation == 10) return float4(sps_path_forwards[1], 0);
            if (_Operation == 11) return float4(0, path.nodeCount, path.deformationWeight, valid);
            if (_Operation == 12) return float4(sps_path_worlds[2], 0);
            if (_Operation == 13) return float4(sps_path_forwards[2], 0);
            return float4(
                dot(sps_path_worlds[2] - origin, up),
                dot(sps_path_worlds[3] - origin, up),
                dot(sps_path_worlds[1] - origin, up),
                valid
            );
        }
        float4 deformation_frag(v2f_img input) : SV_Target {
            SpsDeformPathInfo path;
            if (!sps_has_possible_entrance()) return float4(0, 0, 0, -2);
            if (!sps_try_get_path(path)) return float4(0, 0, 0, -1);
            float weight, radius;
            float3 position, forward, up;
            sps_deform_walk_chain(path, _SampleDistance, weight, radius, position, forward, up);
            return float4(position, radius);
        }
        float4 frag(v2f_img input) : SV_Target {
            if (_Operation == 7) return sps_bake_radius_multiplier(_ProfileDistance);
            if (_Operation == 6) {
                SpsSocketCandidate contact = sps_read_contact((uint)_ContactIndex);
                return float4(contact.radiusOffsetLimit, isinf(contact.radiusOffsetLimit), sps_socket_valid(contact), 1);
            }
            if (_Operation == 3) {
                [unroll]
                for (uint i = 0; i <= SPS_CHAIN_SOCKETS_MAX; i++) {
                    sps_path_worlds[i] = 0;
                    sps_path_forwards[i] = 0;
                }
                sps_path_worlds[0] = _Root;
                sps_path_forwards[0] = _Forward;
                sps_path_worlds[1] = _Position;
                sps_path_forwards[1] = _Exit;
                sps_path_worlds[2] = _Stop1;
                sps_path_forwards[2] = _Guided < 0 ? -_Exit : _Exit;
                if (_Guided > 1) {
                    sps_path_worlds[3] = _Stop2;
                    sps_path_forwards[3] = _Exit;
                }
                float holeAdvance = 0;
                float advance;
                sps_solve_entry_join(_Position - _Root, _Forward, _Exit, _Length, true, advance);
                float solvedAdvance = advance;
                SpsDeformNode entry = (SpsDeformNode)0;
                entry.world = _Position;
                entry.forward = _Exit;
                entry.up = _Up;
                entry.deformationWeight = 1;
                for (uint next = 2; next <= SPS_CHAIN_SOCKETS_MAX && advance > 0; next++) {
                    if (sps_is_zero(sps_path_forwards[next])) break;
                    SpsDeformNode node = (SpsDeformNode)0;
                    node.world = sps_path_worlds[next];
                    node.forward = sps_path_forwards[next];
                    node.up = _Up;
                    node.deformationWeight = 1;
                    if (!sps_advance_entry(entry, node, advance, holeAdvance)) break;
                }
                return float4(entry.world + entry.forward * advance, solvedAdvance);
            }
            if (_Operation == 2) {
                float weight = sps_deformation_weight(_Position, _Forward, _Up, _Length, .02, _BaseRadius, 0);
                return float4(weight, weight, weight, weight);
            }
            if (_Operation > 0 && _Operation != 4) {
                float advance;
                bool valid = sps_solve_entry_join(_Position, _Forward, _Exit, _Length, _Entrance > 0, advance);
                float oppositeAdvance;
                bool oppositeValid = sps_solve_entry_join(_Position, _Forward, -_Exit, _Length, _Entrance > 0, oppositeAdvance);
                return float4(
                    advance,
                    oppositeAdvance,
                    valid ? 1 : 0,
                    oppositeValid ? 1 : 0
                );
            }
            SpsSocketCandidate socket = sps_read_contact((uint)_ContactIndex);
            socket.world = _Position;
            socket.normal = -_Exit;
            socket.up = sps_nearest_normal(socket.normal, _Up);
            socket.control = (uint)_Flags;
            SpsDeformNode previous = (SpsDeformNode)0;
            previous.forward = _Forward;
            previous.up = _Up;
            SpsDeformNode node;
            float distanceSq;
            bool accepted = sps_prepare_socket(
                socket,
                previous,
                _Length,
                max(_Length - _CaptureLength, 0) * SPS_REACH_FADE_MAX,
                .02,
                _BaseRadius,
                _Guided > 0,
                0,
                previous.world,
                0,
                node,
                distanceSq
            );
            return float4(_Operation == 4 ? node.world : node.forward, accepted ? node.deformationWeight : 0);
        }
        ENDHLSL
    } }
}
";
            shader = ShaderUtil.CreateShaderAsset(source);
            pathShader = ShaderUtil.CreateShaderAsset(source
                .Replace("Hidden/VRCFury/Tests/SpsBend", "Hidden/VRCFury/Tests/SpsBendPath")
                .Replace("#pragma fragment frag", "#pragma fragment path_frag"));
            var deformationSource = source
                .Replace("Hidden/VRCFury/Tests/SpsBend", "Hidden/VRCFury/Tests/SpsBendDeformation")
                .Replace("#pragma fragment frag", "#pragma fragment deformation_frag");
            deformationShader = ShaderUtil.CreateShaderAsset(deformationSource);
            alternateTaperShader = ShaderUtil.CreateShaderAsset(deformationSource
                .Replace("Tests/SpsBendDeformation", "Tests/SpsBendAlternateTaper")
                .Replace("float _BendRoom;", @"
                    #undef SPS_TAPER_USE_INSTANT_FOR_STANDALONE_HOLES
                    #undef SPS_TAPER_USE_INSTANT_FOR_PATH_HOLES
                    #undef SPS_TAPER_USE_INSTANT_FOR_PATH_UNCOLLAPSE
                    #undef SPS_TAPER_USE_INSTANT_FOR_PATH_TERMINAL_HOLES
                    #undef SPS_TAPER_INSTANT_FULL_RADIUS_LENGTH
                    #undef SPS_TAPER_SMOOTH_COLLAPSE_LENGTH
                    #define SPS_TAPER_USE_INSTANT_FOR_STANDALONE_HOLES 1
                    #define SPS_TAPER_USE_INSTANT_FOR_PATH_HOLES 1
                    #define SPS_TAPER_USE_INSTANT_FOR_PATH_UNCOLLAPSE 0
                    #define SPS_TAPER_USE_INSTANT_FOR_PATH_TERMINAL_HOLES 0
                    #define SPS_TAPER_INSTANT_FULL_RADIUS_LENGTH .025
                    #define SPS_TAPER_SMOOTH_COLLAPSE_LENGTH .1
                    float _BendRoom;"));
        }

        [TearDown]
        public void TearDownRenderResources() {
            if (material != null) Object.DestroyImmediate(material);
            if (target != null) Object.DestroyImmediate(target);
            if (pixels != null) Object.DestroyImmediate(pixels);
            if (defaultProfile != null) Object.DestroyImmediate(defaultProfile);
        }

        [OneTimeTearDown]
        public void TearDown() {
            if (shader != null) Object.DestroyImmediate(shader);
            if (pathShader != null) Object.DestroyImmediate(pathShader);
            if (deformationShader != null) Object.DestroyImmediate(deformationShader);
            if (alternateTaperShader != null) Object.DestroyImmediate(alternateTaperShader);
        }

        protected void SetIndependentSockets(Shader targetShader, float secondZ, bool hole) {
            SetPath(targetShader, (Vector3.forward * -.2f, Vector3.forward, 0));
            var rotation = Quaternion.LookRotation(Vector3.back, Vector3.up);
            SetContacts(
                "SPS_Self",
                Matrix4x4.identity,
                Matrix4x4.TRS(Vector3.forward * -.2f, rotation, Vector3.one),
                Matrix4x4.TRS(
                    Vector3.forward * secondZ,
                    rotation,
                    new Vector3(2 | (hole ? 1 << 14 : 0), 1, 1)
                )
            );
        }

        protected void SetPath(params (Vector3 position, Vector3 forward, int flags)[] nodes) => SetPath(pathShader, nodes);

        protected void SetPath(Shader targetShader, params (Vector3 position, Vector3 forward, int flags)[] nodes) =>
            SetPath(targetShader, nodes, -1, 0);

        protected void SetPath(
            Shader targetShader,
            (Vector3 position, Vector3 forward, int flags)[] nodes,
            int zFlagNode,
            int zFlags
        ) {
            Read(Vector3.zero, Vector3.forward);
            material.shader = targetShader;
            Read(Vector3.zero, Vector3.forward);
            var contacts = nodes.Select((node, index) => Matrix4x4.TRS(
                node.position,
                Quaternion.LookRotation(-node.forward, Vector3.up),
                new Vector3(1 | (index << 7) | node.flags, 1, index == zFlagNode ? 1 | zFlags : 1)
            )).ToArray();
            SetContacts("SPS_Self", Matrix4x4.identity, contacts[0]);
            SetContacts("SPS_Others", Matrix4x4.identity);
            SetContacts("SPS_Path", Matrix4x4.identity, contacts.Skip(1).ToArray());
            material.SetFloat("_SPS_BakedLength", 1);
            material.SetFloat("_SPS_BakedRadius", DefaultPlugRadius);
            material.SetFloat("_SPS_BaseActivationRadiusOverride", 1);
        }

        protected void SetContacts(string buffer, Matrix4x4 frame, params Matrix4x4[] contacts) {
            var transforms = new Matrix4x4[9];
            Array.Copy(contacts, transforms, contacts.Length);
            transforms[8] = frame;
            var data = new Vector4[9];
            data[8].y = BitConverter.ToSingle(BitConverter.GetBytes(contacts.Length), 0);
            material.SetMatrixArray("_VRChatContact_Transforms_" + buffer, transforms);
            material.SetVectorArray("_VRChatContact_Data_" + buffer, data);
        }

        protected Texture2D SetProfile(params Keyframe[] keys) => SetProfile(0, 0, keys);

        protected Texture2D SetProfile(int vertices, int blendshapes, params Keyframe[] keys) {
            using var baked = new SpsBakedTexture(0);
            var curve = ProfileCurve(keys);
            baked.WriteFloat(0);
            for (var i = 0; i < vertices * 10 + (vertices * 9 + 1) * blendshapes; i++) baked.WriteFloat(7);
            for (var i = 0; i < SpsBaker.RadiusProfileSamples; i++) {
                baked.WriteFloat(curve.Evaluate(i / (SpsBaker.RadiusProfileSamples - 1f)));
            }
            var texture = baked.Save();
            material.SetTexture("_SPS_Bake", texture);
            material.SetVector("_SPS_Bake_TexelSize", new Vector4(1f / texture.width, 1f / texture.height, texture.width, texture.height));
            material.SetFloat("_SPS_BlendshapeVertCount", vertices);
            material.SetFloat("_SPS_BlendshapeCount", blendshapes);
            material.SetFloat(SpsConfigurer.SpsRadiusProfileSamples, SpsBaker.RadiusProfileSamples);
            return texture;
        }

        protected static AnimationCurve ProfileCurve(Keyframe[] keys) {
            var curve = new AnimationCurve(keys) { preWrapMode = WrapMode.ClampForever, postWrapMode = WrapMode.ClampForever };
            for (var i = 0; i < keys.Length; i++) {
                AnimationUtility.SetKeyLeftTangentMode(curve, i, AnimationUtility.TangentMode.Linear);
                AnimationUtility.SetKeyRightTangentMode(curve, i, AnimationUtility.TangentMode.Linear);
            }
            return curve;
        }

        protected Color Read(
            Vector3 position,
            Vector3 exit,
            bool twoWay = false,
            bool guided = false,
            bool entrance = true,
            bool joins = false,
            bool hole = false,
            float scale = 1,
            Quaternion rotation = default,
            float captureLength = .4f,
            int unlock = 0,
            float baseRadius = 1,
            float verticalAngle = 89,
            bool captureOnly = false,
            float horizontalAngle = 45,
            float maxAdvance = .3f,
            float bendRoom = .2f
        ) {
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null) Assert.Ignore("Requires a graphics device.");
            if (material == null) {
                material = new Material(shader);
                target = new RenderTexture(1, 1, 0, RenderTextureFormat.ARGBFloat);
                pixels = new Texture2D(1, 1, TextureFormat.RGBAFloat, false);
                defaultProfile = SetProfile(new Keyframe(0, 1), new Keyframe(1, 1));
            }
            if (rotation == default) rotation = Quaternion.identity;
            material.SetVector("_Position", rotation * position * scale);
            material.SetVector("_Forward", rotation * Vector3.forward);
            material.SetVector("_Up", rotation * Vector3.up);
            material.SetVector("_Exit", rotation * exit);
            material.SetFloat("_Length", .4f * scale);
            material.SetFloat("_CaptureLength", captureLength * scale);
            material.SetFloat("_BaseRadius", baseRadius * scale);
            material.SetFloat("_VerticalAngle", verticalAngle);
            material.SetFloat("_HorizontalAngle", horizontalAngle);
            material.SetFloat("_AdvanceMax", maxAdvance);
            material.SetFloat("_BendRoom", bendRoom);
            material.SetFloat("_Guided", guided ? 1 : 0);
            material.SetFloat("_Entrance", entrance ? 1 : 0);
            material.SetFloat("_Flags", (twoWay ? 1 << 13 : 0) | (hole ? 1 << 14 : 0) | unlock);
            material.SetFloat("_Operation", captureOnly ? 2 : joins ? 1 : 0);
            SetRadiusOffsetLimit(1);
            material.SetTexture("_SPS_Bake", defaultProfile);
            material.SetFloat(SpsConfigurer.SpsRadiusProfileSamples, SpsBaker.RadiusProfileSamples);
            material.SetVector(
                "_SPS_Bake_TexelSize",
                new Vector4(
                    1f / defaultProfile.width,
                    1f / defaultProfile.height,
                    defaultProfile.width,
                    defaultProfile.height
                )
            );
            material.SetFloat("_SPS_BlendshapeVertCount", 0);
            material.SetFloat("_SPS_BlendshapeCount", 0);
            material.SetFloat("_ProfileDistance", 0);
            return Render();
        }

        protected void SetRadiusOffsetLimit(float encodedCap, bool others = false, bool native = true) {
            var transforms = new Matrix4x4[9];
            transforms[0] = Matrix4x4.Scale(new Vector3(1, encodedCap, 1));
            transforms[8] = Matrix4x4.identity;
            var data = new Vector4[9];
            data[0].y = BitConverter.ToSingle(BitConverter.GetBytes(native ? 1 : 2), 0);
            data[8].y = BitConverter.ToSingle(BitConverter.GetBytes(1), 0);
            var buffer = others ? "SPS_Others" : "SPS_Self";
            material.SetMatrixArray("_VRChatContact_Transforms_" + buffer, transforms);
            material.SetVectorArray("_VRChatContact_Data_" + buffer, data);
            material.SetFloat("_ContactIndex", others ? 8 : 0);
            material.SetFloat("_SPS_OthersChannelTagCount", 1);
            material.SetFloat("_SPS_OthersLegacyTags", 1);
        }

        protected Color Render() {
            var previous = RenderTexture.active;
            try {
                Graphics.Blit(Texture2D.blackTexture, target, material);
                RenderTexture.active = target;
                pixels.ReadPixels(new Rect(0, 0, 1, 1), 0, 0);
                pixels.Apply();
                return pixels.GetPixel(0, 0);
            } finally {
                RenderTexture.active = previous;
            }
        }

        // Evaluate the geometric inequalities directly, independently of the shader's
        // quadratic inverse. All inputs here use a 0.4m plug and bend room 0.2.
        protected static bool MeetsJoinBounds(Vector3 position, Vector3 exit, float advance, double tolerance) {
            const double h = .5;
            const double radius = .2;
            var delta = position / .4f;
            var c = Math.Max(-1, Math.Min(1, exit.z));
            var q0 = Vector3.Dot(delta, exit);
            var lateral = delta - exit * q0;
            var rSq = (double)lateral.sqrMagnitude;
            var allowance = (1 - h) * rSq / (2 * radius) * Math.Max(-c, 0);
            var k = h * c;
            var q = q0 + advance / .4;
            var distanceSq = q * q + rSq;
            var distance = Math.Sqrt(distanceSq);
            return q >= Math.Max(q0, 0) - tolerance
                && advance <= .3 * .4 + tolerance
                && distanceSq >= 2 * radius * radius * (1 - c) - tolerance
                && distanceSq >= Math.Sqrt(6 * h) * radius * Math.Sqrt(rSq) - tolerance
                && c * q + lateral.z + allowance >= k * distance - tolerance
                && q + allowance >= k * distance - tolerance
                && distanceSq <= 1.6 * 1.6 + tolerance;
        }
    }
}
