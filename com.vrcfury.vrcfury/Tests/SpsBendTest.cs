using System;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Rendering;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace VF.Tests {
    [Category("VRCFury")]
    public class SpsBendTest {
        private Shader shader;
        private Material material;
        private RenderTexture target;
        private Texture2D pixels;

        [OneTimeSetUp]
        public void SetUp() {
#if !VRCSDK_HAS_MATERIAL_CONTACTS
            Assert.Ignore("SPS requires material contacts.");
#endif
            shader = ShaderUtil.CreateShaderAsset(@"
Shader ""Hidden/VRCFury/Tests/SpsBend"" {
    SubShader { Pass { ZTest Always Cull Off ZWrite Off
        HLSLPROGRAM
        #pragma target 4.5
        #pragma vertex vert_img
        #pragma fragment frag
        #include ""UnityCG.cginc""
        #include ""Packages/com.vrcfury.vrcfury/SPS/deform/sps_config.cginc""
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
        #define SPS_INC_CONTACTS
        #define SPS_INC_LIGHTS
        #define SPS_INC_GLOBALS
        #define SPS_CONTACT_COUNT (3 * VRC_CONTACT_MAX_TARGETS)
        struct SpsSocketCandidate { float3 world; float3 normal; float3 up; uint control; int owner; };
        bool sps_socket_valid(SpsSocketCandidate s) { return (s.control & SPS_SOCKET_VALID) != 0; }
        uint sps_socket_key(SpsSocketCandidate s) { return s.control & 1023u; }
        float4x4 sps_contact_frame() { return 0; }
        SpsSocketCandidate sps_read_contact(uint i) { return (SpsSocketCandidate)0; }
        SpsSocketCandidate sps_read_light(uint i, float3 p, float3 u) { return (SpsSocketCandidate)0; }
        float _SPS_RadiusOffsetMultiplier;
        float _SPS_BakedLength, _SPS_BakedRadius, _SPS_BaseActivationRadiusOverride;
        #include ""Packages/com.vrcfury.vrcfury/SPS/deform/sps_path.cginc""
        #include ""Packages/com.vrcfury.vrcfury/SPS/deform/sps_curve.cginc""
        float3 _Position, _Forward, _Up, _Exit, _Root, _Stop1, _Stop2;
        float _Length, _CaptureLength, _Guided, _Entrance, _Flags, _Operation, _BaseRadius;
        float4 frag(v2f_img input) : SV_Target {
            if (_Operation == 3) {
                [unroll]
                for (uint i = 0; i <= SPS_CHAIN_SOCKETS_MAX; i++) {
                    sps_path_worlds[i] = 0;
                    sps_path_forwards[i] = 0;
                    sps_path_weights[i] = 1;
                    sps_path_flags[i] = 0;
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
                SpsDeformPathInfo path = (SpsDeformPathInfo)0;
                path.worldLength = _Length;
                sps_solve_entry_join(_Position - _Root, _Forward, _Exit, _Length, true, path.entryAdvance);
                float advance = path.entryAdvance;
                float3 joinPoint = _Position;
                float3 forward = _Exit;
                uint flags = 0;
                float weight = 1;
                float terminalAdvance;
                sps_deform_advance_entry(advance, joinPoint, forward, flags, weight, terminalAdvance);
                return float4(joinPoint, advance);
            }
            if (_Operation == 2) {
                float weight = sps_deformation_weight(_Position, _Forward, _Up, _Length, .02, _BaseRadius, 0);
                return float4(weight, weight, weight, weight);
            }
            if (_Operation > 0) {
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
            SpsSocketCandidate socket = (SpsSocketCandidate)0;
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
                _CaptureLength,
                .02,
                _BaseRadius,
                _Guided > 0,
                _Entrance > 0,
                node,
                distanceSq
            );
            return float4(node.forward, accepted ? node.deformationWeight : 0);
        }
        ENDHLSL
    } }
}
");
        }

        [OneTimeTearDown]
        public void TearDown() {
            if (material != null) Object.DestroyImmediate(material);
            if (target != null) Object.DestroyImmediate(target);
            if (pixels != null) Object.DestroyImmediate(pixels);
            if (shader != null) Object.DestroyImmediate(shader);
        }

        [Test]
        public void ProductionPathAndCurveCompile() {
            var compiled = ShaderUtil.GetShaderData(shader).GetSerializedSubshader(0).GetPass(0).CompileVariant(
                ShaderType.Fragment,
                Array.Empty<string>(),
                ShaderCompilerPlatform.D3D,
                BuildTarget.StandaloneWindows64
            );
            Assert.That(compiled.Success, Is.True, string.Join("\n", compiled.Messages.Select(m => m.message)));
            Assert.That(compiled.ShaderData, Is.Not.Null.And.Not.Empty);
        }

        private Color Read(
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
            return Render();
        }

        private Color Render() {
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

        [Test]
        public void WithoutBaseOverrideBothAxesUseTheVerticalAngle() {
            var horizontal = Read(
                new Vector3(.16f, 0, .05f),
                Vector3.forward,
                baseRadius: 0,
                horizontalAngle: 10,
                verticalAngle: 60,
                captureOnly: true
            );
            var vertical = Read(
                new Vector3(0, .16f, .05f),
                Vector3.forward,
                baseRadius: 0,
                horizontalAngle: 10,
                verticalAngle: 60,
                captureOnly: true
            );
            Assert.That(horizontal.a, Is.GreaterThan(0));
            Assert.That(horizontal.a, Is.EqualTo(vertical.a).Within(.0001f));
        }

        [Test]
        public void CylinderSideFadeIsContinuousAcrossTheBasePlane() {
            foreach (var angle in new[] { 89f, 90f, 100f, 120f, 180f }) {
                var behind = Read(new Vector3(0, .09f, -.000001f), Vector3.forward, baseRadius: .1f, verticalAngle: angle, captureOnly: true);
                var ahead = Read(new Vector3(0, .09f, .000001f), Vector3.forward, baseRadius: .1f, verticalAngle: angle, captureOnly: true);
                Assert.That(behind.a, Is.EqualTo(ahead.a).Within(.001f), "angle=" + angle);
            }
        }

        [Test]
        public void NinetyDegreeConeFadesContinuouslyAtTheBasePlane() {
            var behind = Read(new Vector3(0, .3f, -.000001f), Vector3.forward, baseRadius: .1f, verticalAngle: 90, captureOnly: true);
            var ahead = Read(new Vector3(0, .3f, .000001f), Vector3.forward, baseRadius: .1f, verticalAngle: 90, captureOnly: true);
            Assert.That(behind.a, Is.Zero);
            Assert.That(ahead.a, Is.GreaterThan(0).And.LessThan(.001f));
        }

        [TestCase(89, false)]
        [TestCase(90, false)]
        [TestCase(100, true)]
        [TestCase(120, true)]
        public void VerticalConeOpensBehindOnlyAboveNinety(float angle, bool expected) {
            var result = Read(new Vector3(0, .3f, -.05f), Vector3.forward, baseRadius: .1f, verticalAngle: angle, captureOnly: true);
            Assert.That(float.IsNaN(result.a) || float.IsInfinity(result.a), Is.False);
            Assert.That(result.a > 0, Is.EqualTo(expected));
            var side = Read(new Vector3(.101f, .3f, -.05f), Vector3.forward, baseRadius: .1f, verticalAngle: angle, captureOnly: true);
            Assert.That(side.a, Is.Zero, "Vertical opening must retain the horizontal boundary.");
        }

        [TestCase(89, false)]
        [TestCase(90, false)]
        [TestCase(100, true)]
        [TestCase(120, true)]
        public void HorizontalConeOpensBehindOnlyAboveNinety(float angle, bool expected) {
            var result = Read(new Vector3(.3f, 0, -.05f), Vector3.forward, baseRadius: .1f, horizontalAngle: angle, captureOnly: true);
            Assert.That(float.IsNaN(result.a) || float.IsInfinity(result.a), Is.False);
            Assert.That(result.a > 0, Is.EqualTo(expected));
        }

        [Test]
        public void ExtendedAnglesAreSymmetricAndFinite() {
            foreach (var horizontal in new[] { 0f, 45f, 89.999f, 90f, 90.001f, 120f, 180f, 200f })
            foreach (var vertical in new[] { 0f, 45f, 90f, 120f, 180f, 200f })
            foreach (var p in new[] { Vector3.zero, new Vector3(.09f, .03f, 0), new Vector3(.3f, .2f, -.05f), new Vector3(0, 0, -.3f), new Vector3(.65f, 0, 0) }) {
                var result = Read(p, Vector3.forward, baseRadius: .1f, horizontalAngle: horizontal, verticalAngle: vertical, captureOnly: true);
                var swapped = Read(new Vector3(p.y, p.x, p.z), Vector3.forward, baseRadius: .1f, horizontalAngle: vertical, verticalAngle: horizontal, captureOnly: true);
                Assert.That(result.a, Is.InRange(0f, 1f), $"position={p}, angles={horizontal}/{vertical}");
                Assert.That(swapped.a, Is.EqualTo(result.a).Within(.0001f), $"position={p}, angles={horizontal}/{vertical}");
            }
        }

        [Test]
        public void BothAxesCanOpenBehindWhileRetainingReachLimit() {
            Assert.That(Read(new Vector3(.3f, .3f, -.05f), Vector3.forward, baseRadius: .1f, horizontalAngle: 120, verticalAngle: 120, captureOnly: true).a, Is.GreaterThan(0));
            Assert.That(Read(new Vector3(.5f, .5f, -.05f), Vector3.forward, baseRadius: .1f, horizontalAngle: 180, verticalAngle: 180, captureOnly: true).a, Is.Zero);
        }

        [TestCase(90)]
        [TestCase(100)]
        [TestCase(120)]
        [TestCase(180)]
        public void ExtendedConeStillRespectsMaximumReach(float angle) {
            var result = Read(new Vector3(0, .65f, 0), Vector3.forward, baseRadius: .1f, verticalAngle: angle, captureOnly: true);
            Assert.That(result.a, Is.Zero);
        }

        [Test]
        public void FullyOpenVerticalConeAcceptsDirectlyBehind() {
            var result = Read(new Vector3(0, 0, -.3f), Vector3.forward, baseRadius: .1f, verticalAngle: 180, captureOnly: true);
            var beyond = Read(new Vector3(0, 0, -.3f), Vector3.forward, baseRadius: .1f, verticalAngle: 200, captureOnly: true);
            Assert.That(result.a, Is.GreaterThan(0));
            Assert.That(beyond.a, Is.EqualTo(result.a).Within(.0001f));
        }

        [Test]
        public void RearCylinderIsOneAndAHalfRadiiDeepWithoutWidening() {
            Assert.That(Read(new Vector3(0, 0, -.12f), Vector3.forward, baseRadius: .1f, captureOnly: true).a, Is.EqualTo(1).Within(.0001f));
            Assert.That(Read(new Vector3(0, 0, -.145f), Vector3.forward, baseRadius: .1f, captureOnly: true).a, Is.EqualTo(1f / 6).Within(.0001f));
            Assert.That(Read(new Vector3(0, 0, -.151f), Vector3.forward, baseRadius: .1f, captureOnly: true).a, Is.Zero);
            Assert.That(Read(new Vector3(.101f, 0, -.12f), Vector3.forward, baseRadius: .1f, captureOnly: true).a, Is.Zero);
        }

        [Test]
        public void TiltedHandjobStaysForwardAcrossTheBase() {
            foreach (var up in new[] { -1f, 1f })
            foreach (var z in new[] { -.02f, -.005f, 0f, .005f, .02f })
            foreach (var angle in new[] { -30f, -5f, 0f, 5f, 30f }) {
                var exit = Quaternion.Euler(angle, 0, 0) * Vector3.forward;
                var position = new Vector3(0, up * .005f, z);
                var twoWay = Read(position, exit, twoWay: true);
                Assert.That(twoWay.a, Is.EqualTo(1).Within(.0001f));
                Assert.That(twoWay.b, Is.GreaterThan(.8f));
                Assert.That(Read(position, exit).a, Is.EqualTo(1).Within(.0001f));
                Assert.That(Read(position, -exit).a, Is.Zero);
            }
        }

        [TestCase(false)]
        [TestCase(true)]
        public void BothAuthoredDirectionsCanWorkAtTheMouth(bool hole) {
            foreach (var up in new[] { -1f, 1f })
            foreach (var angle in new[] { -5f, 0f, 5f }) {
                var position = new Vector3(0, up * .18f, 0);
                var exit = Quaternion.Euler(angle, 0, 0) * Vector3.forward;
                Assert.That(Read(position, exit, hole: hole).a, Is.EqualTo(1).Within(.0001f));
                Assert.That(Read(position, -exit, hole: hole).a, Is.EqualTo(1).Within(.0001f));
            }
        }

        [Test]
        public void DownwardHoleCanJoinBeyondEntranceWhenAdvanceBudgetAllowsIt() {
            foreach (var angle in new[] { 80f, 85f, 90f, 95f, 100f }) {
                var exit = Quaternion.Euler(angle, 0, 0) * Vector3.forward;
                foreach (var forward in new[] { .1f, .2f })
                foreach (var scale in new[] { 1f, 2f }) {
                    var position = new Vector3(0, .2f, forward);
                    var oneWay = Read(position, exit, hole: true, scale: scale, maxAdvance: 1.6f);
                    Assert.That(oneWay.a, Is.EqualTo(1).Within(.0001f), "angle=" + angle);
                    var twoWay = Read(position, exit, twoWay: true, hole: true, scale: scale);
                    Assert.That(twoWay.a, Is.EqualTo(1).Within(.0001f));
                    Assert.That(twoWay.g, Is.GreaterThan(.9f));
                }
            }
        }

        [TestCase(0, 1)]
        [TestCase(0, -1)]
        [TestCase(1, 0)]
        [TestCase(-1, 0)]
        public void AngledHoleKeepsTargetingWhilePassingThroughPlugRoot(float x, float y) {
            var exit = new Vector3(x, y, 1).normalized;
            foreach (var scale in new[] { .5f, 1f, 2f }) {
                foreach (var distance in new[] { .04f, .02f, .005f, 0f, -.005f, -.01f, -.02f, -.04f }) {
                    var result = Read(exit * distance, exit, hole: true, scale: scale);
                    Assert.That(result.a, Is.EqualTo(1).Within(.0001f), $"distance={distance}, scale={scale}");
                }
            }
        }

        [Test]
        public void JoinCanMakeRoomForATurnButCannotReverseAtRoot() {
            var exit = new Vector3(0, 1, .1f).normalized;
            Assert.That(Read(Vector3.zero, exit, hole: true).a, Is.EqualTo(1).Within(.0001f));
            Assert.That(Read(Vector3.zero, Vector3.back, hole: true).a, Is.Zero);
        }

        [Test]
        public void BackwardFaceDirectlyAheadIsRejected() {
            var position = new Vector3(0, 0, .2f);
            Assert.That(Read(position, Vector3.back).a, Is.Zero);
            var twoWay = Read(position, Vector3.back, twoWay: true);
            Assert.That(twoWay.a, Is.EqualTo(1).Within(.0001f));
            Assert.That(twoWay.b, Is.EqualTo(1).Within(.0001f));
        }

        [Test]
        public void NearlyCrosswiseSocketPullsTowardItsSide() {
            foreach (var side in new[] { -1f, 1f })
            foreach (var angle in new[] { 89f, 89.5f, 90f, 90.5f, 91f }) {
                var exit = Quaternion.Euler(0, angle, 0) * Vector3.forward;
                var result = Read(new Vector3(side * .01f, 0, .3f), exit, twoWay: true);
                Assert.That(result.a, Is.EqualTo(1).Within(.0001f));
                Assert.That(result.r * side, Is.GreaterThan(0));
            }
        }

        [Test]
        public void TwoWayChoosesShorterPermissibleRoute() {
            var position = new Vector3(0, .2f, 0);
            var exit = Quaternion.Euler(5, 0, 0) * Vector3.forward;
            var result = Read(position, exit, twoWay: true);
            Assert.That(result.a, Is.EqualTo(1).Within(.0001f));
            Assert.That(Vector3.Dot(new Vector3(result.r, result.g, result.b), -exit), Is.GreaterThan(.999f));
            var reversed = Read(position, -exit, twoWay: true);
            Assert.That(Vector4.Distance(result, reversed), Is.LessThan(.0001f));
        }

        [Test]
        public void SymmetricTwoWayDoesNotDependOnAuthoredNormalSign() {
            foreach (var exit in new[] { Vector3.up, Vector3.right }) {
                var first = Read(Vector3.zero, exit, twoWay: true);
                var reversed = Read(Vector3.zero, -exit, twoWay: true);
                Assert.That(first.a, Is.EqualTo(1).Within(.0001f));
                Assert.That(Vector4.Distance(first, reversed), Is.LessThan(.0001f));
            }
        }

        [TestCase(1 << 10)]
        [TestCase(1 << 11)]
        public void UnlockedSocketKeepsItsAutomaticOrientation(int unlock) {
            var position = new Vector3(0, .2f, .1f);
            var result = Read(position, Vector3.back, twoWay: true, unlock: unlock);
            Assert.That(result.a, Is.EqualTo(1).Within(.0001f));
            Assert.That(Vector3.Dot(new Vector3(result.r, result.g, result.b), position.normalized), Is.GreaterThan(.999f));
        }

        [TestCase(1 << 10, true)]
        [TestCase(1 << 11, true)]
        [TestCase(1 << 10, false)]
        [TestCase(1 << 11, false)]
        public void UnlockedTwoWayStillRequiresAPermissibleJoin(int unlock, bool entrance) {
            var position = new Vector3(0, .005f, 0);
            Assert.That(Read(position, Vector3.up, joins: true, maxAdvance: 0).b, Is.Zero);
            var result = Read(
                position,
                Vector3.up,
                twoWay: true,
                entrance: entrance,
                unlock: unlock,
                maxAdvance: 0
            );
            Assert.That(result.a, Is.Zero);
        }

        [TestCase(1e-8f)]
        [TestCase(1e-19f)]
        [TestCase(1e-25f)]
        [TestCase(-1e-25f)]
        public void NearlyPerpendicularJoinMatchesPerpendicularJoin(float forwardComponent) {
            var position = new Vector3(0, .1f, .05f);
            var perpendicular = Read(position, Vector3.up, joins: true);
            var nearlyPerpendicular = Read(position, new Vector3(0, 1, forwardComponent), joins: true);
            Assert.That(perpendicular.b, Is.EqualTo(1));
            Assert.That(nearlyPerpendicular.b, Is.EqualTo(1));
            Assert.That(nearlyPerpendicular.r, Is.EqualTo(perpendicular.r).Within(.00001f));
        }

        [TestCase(1e-25f)]
        [TestCase(-1e-25f)]
        public void TinyDotProductPreservesTheFoldBound(float forwardComponent) {
            var position = new Vector3(.05f, .1f, 0);
            // At exactly perpendicular this constraint disappears. Compare against
            // the same side of perpendicular, where the fold bound still applies.
            var referenceExit = new Vector3(0, 1, Math.Sign(forwardComponent) * 1e-8f);
            var reference = Read(position, referenceExit, joins: true);
            var result = Read(position, new Vector3(0, 1, forwardComponent), joins: true);
            Assert.That(reference.b, Is.EqualTo(1));
            Assert.That(result.b, Is.EqualTo(1));
            Assert.That(result.r, Is.EqualTo(reference.r).Within(.00001f));
        }

        [Test]
        public void GuidedStopKeepsAuthoredDirectionDespiteSharpnessAndExhaustedReach() {
            var result = Read(
                new Vector3(0, 0, .001f),
                Vector3.back,
                guided: true,
                entrance: false,
                captureLength: 0
            );
            Assert.That(result.a, Is.EqualTo(1).Within(.0001f));
            Assert.That(result.b, Is.EqualTo(-1).Within(.0001f));
        }

        [Test]
        public void OnlyInitialEntranceUsesLookahead() {
            var position = new Vector3(0, .1f, -.02f);
            var entrance = Read(position, Vector3.forward, joins: true);
            var laterStop = Read(position, Vector3.forward, entrance: false, joins: true);
            Assert.That(entrance.r, Is.GreaterThan(.02f));
            Assert.That(laterStop.r, Is.Zero);
            Assert.That(Mathf.Abs(entrance.r - laterStop.r), Is.GreaterThan(.0001f));
        }

        [Test]
        public void AlignedPlugJoinsImmediatelyRatherThanAdvancingAFixedAmount() {
            foreach (var z in new[] { .1f, 0f, -.1f }) {
                var join = Read(new Vector3(0, 0, z), Vector3.forward, joins: true);
                Assert.That(join.r, Is.EqualTo(Mathf.Max(-z, 0)).Within(.0001f));
                Assert.That(join.b, Is.EqualTo(1));
            }
        }

        [Test]
        public void JoinProvidesRoomForSidewaysOffsetAndTurningTogether() {
            foreach (var angle in new[] { 0f, 15f, 30f, 45f, 60f }) {
                var exit = Quaternion.Euler(-angle, 0, 0) * Vector3.forward;
                var sideways = new Vector3(0, exit.z, -exit.y);
                foreach (var offset in new[] { -.05f, -.02f, -.005f, 0f, .005f, .02f, .05f }) {
                    var position = -exit * .04f + sideways * offset;
                    if (position.z > 0) continue;
                    var result = Read(position, exit, hole: true, maxAdvance: 1.6f);
                    var join = Read(position, exit, joins: true, maxAdvance: 1.6f);
                    Assert.That(join.b, Is.EqualTo(1), $"angle={angle}, offset={offset}");
                    Assert.That(result.a, Is.EqualTo(1).Within(.0001f), $"angle={angle}, offset={offset}");
                }
            }
        }

        [Test]
        public void EntrySocketAdvanceWalksAcrossGuidedSegmentsAndFinalExit() {
            foreach (var z in new[] { .0125f, .025f, .05f }) {
                var root = new Vector3(.025f, 0, z);
                var entry = Read(-root, Vector3.forward, joins: true);
                Assert.That(entry.b, Is.EqualTo(1));
                material.SetVector("_Position", Vector3.zero);
                material.SetVector("_Stop1", new Vector3(.025f, 0, 0));
                material.SetVector("_Stop2", new Vector3(.025f, 0, .025f));
                material.SetFloat("_Operation", 3);
                material.SetFloat("_Guided", 2);
                material.SetVector("_Root", root);
                var result = Render();
                Assert.That(result.r, Is.EqualTo(.025f).Within(.0001f));
                Assert.That(result.g, Is.Zero.Within(.0001f));
                Assert.That(result.b, Is.EqualTo(entry.r - .025f).Within(.0001f));
                Assert.That(result.a, Is.EqualTo(entry.r).Within(.0001f));
            }
        }

        [Test]
        public void GuidedStopDirectionDoesNotRecalculateTheEntryAdvance() {
            var root = new Vector3(.025f, 0, .05f);
            var entry = Read(-root, Vector3.forward, joins: true);
            Assert.That(entry.b, Is.EqualTo(1));
            material.SetVector("_Position", Vector3.zero);
            material.SetVector("_Root", root);
            material.SetVector("_Stop1", root);
            material.SetFloat("_Operation", 3);
            material.SetFloat("_Guided", -1); // A guide authored to point backward.
            var result = Render();
            Assert.That(result.r, Is.EqualTo(root.x).Within(.0001f));
            Assert.That(result.g, Is.Zero.Within(.0001f));
            Assert.That(result.b, Is.EqualTo(root.z - (entry.r - root.magnitude)).Within(.0001f));
            Assert.That(result.a, Is.EqualTo(entry.r).Within(.0001f));
        }

        [Test]
        public void LaterPathStopsCannotPullTheEntryJoinFartherAlongThePath() {
            Read(Vector3.zero, Vector3.forward);
            material.SetVector("_Position", Vector3.zero);
            material.SetVector("_Root", new Vector3(0, 0, .05f));
            material.SetVector("_Stop1", new Vector3(.1f, 0, 0));
            material.SetVector("_Stop2", new Vector3(0, 0, .05f));
            material.SetFloat("_Operation", 3);
            material.SetFloat("_Guided", 2);
            var result = Render();
            Assert.That(result.a, Is.EqualTo(.05f).Within(.00001f));
            Assert.That(result.r, Is.EqualTo(.05f).Within(.0001f));
            Assert.That(result.b, Is.Zero.Within(.0001f));
        }

        [Test]
        public void SmallerBendRoomPermitsATighterJoin() {
            var exit = new Vector3(0, 1, 1).normalized;
            var tight = Read(Vector3.zero, exit, joins: true, bendRoom: .1f);
            var normal = Read(Vector3.zero, exit, joins: true);
            var broad = Read(Vector3.zero, exit, joins: true, bendRoom: .3f);
            foreach (var result in new[] { tight, normal, broad }) Assert.That(result.b, Is.EqualTo(1));
            Assert.That(tight.r, Is.LessThan(normal.r));
            Assert.That(broad.r, Is.GreaterThan(normal.r));
        }

        [TestCase(0, false)]
        [TestCase(.1f, false)]
        [TestCase(.2f, true)]
        [TestCase(.3f, true)]
        public void MaximumAdvanceRejectsInsteadOfClampingAnInvalidJoin(float cap, bool allowed) {
            var exit = new Vector3(0, 1, 1).normalized;
            var result = Read(Vector3.zero, exit, joins: true, maxAdvance: cap);
            Assert.That(result.b, Is.EqualTo(allowed ? 1 : 0));
            if (allowed) {
                Assert.That(result.r, Is.EqualTo(.2f * Mathf.Sqrt(2 - Mathf.Sqrt(2)) * .4f).Within(.00001f));
                Assert.That(result.r, Is.LessThanOrEqualTo(cap * .4f));
            }
        }

        [Test]
        public void AdvanceCapIsMeasuredFromEntranceInsteadOfClosestPoint() {
            var position = new Vector3(0, 0, -.2f);
            Assert.That(Read(position, Vector3.forward, joins: true).b, Is.Zero);
            var uncapped = Read(position, Vector3.forward, joins: true, maxAdvance: 1.6f);
            Assert.That(uncapped.b, Is.EqualTo(1));
            Assert.That(uncapped.r, Is.EqualTo(.2f).Within(.00001f));
        }

        [Test]
        public void RearCaptureDepthCanEndBeforeAnAlignedJoinReachesItsAdvanceCap() {
            var position = new Vector3(0, 0, -.16f);
            var join = Read(position, Vector3.forward, joins: true, maxAdvance: .5f);
            Assert.That(join.b, Is.EqualTo(1));
            Assert.That(join.r, Is.EqualTo(.16f).Within(.00001f));
            Assert.That(Read(position, Vector3.forward, hole: true, baseRadius: .08f, maxAdvance: .5f).a, Is.Zero,
                "The 1.5-radius rear cylinder ends at 0.12m, before the 0.20m advance cap");
            Assert.That(Read(position, Vector3.forward, hole: true, baseRadius: .14f, maxAdvance: .5f).a,
                Is.EqualTo(1).Within(.0001f));
            Assert.That(Read(new Vector3(0, 0, -.201f), Vector3.forward, hole: true, baseRadius: .14f, maxAdvance: .5f).a,
                Is.Zero, "A larger capture zone still cannot exceed the advance cap");
        }

        [Test]
        public void JoinUsesFullPlugLengthRatherThanRemainingReach() {
            var position = new Vector3(0, .005f, .005f);
            Assert.That(Read(position, Vector3.down).a, Is.EqualTo(1).Within(.0001f));
            Assert.That(Read(position, Vector3.down, captureLength: .1f).a, Is.EqualTo(1).Within(.0001f));
        }

        [Test]
        public void ReportedAngledRootTransformKeepsTargetingAcrossScales() {
            var exit = -(Quaternion.Euler(358, 136, 1) * Vector3.forward);
            foreach (var positionScale in new[] { 1f, 10f, 100f }) {
                var position = new Vector3(0, .0005f, -.00019f) * positionScale;
                Assert.That(Read(position, exit, hole: true).a, Is.EqualTo(1).Within(.0001f), $"positionScale={positionScale}");
            }
        }

        [Test]
        public void JoinDoesNotSnapBetweenEntranceAndOffsetAtTheBasePlane() {
            var exit = new Vector3(1, 0, 1).normalized;
            var before = Read(new Vector3(.02f, 0, .00001f), exit, joins: true).r;
            var after = Read(new Vector3(.02f, 0, -.00001f), exit, joins: true).r;
            Assert.That(before, Is.GreaterThan(0));
            Assert.That(after, Is.EqualTo(before).Within(.0001f));
        }

        [Test]
        public void EntrancePoseDoesNotOverrideAPermissibleAdvancedJoin() {
            var exit = new Vector3(1, 0, 1).normalized;
            var position = new Vector3(-.02f, 0, .00001f);
            var join = Read(position, exit, joins: true);
            Assert.That(join.b, Is.EqualTo(1));
            Assert.That(join.r, Is.GreaterThan(0));
            Assert.That(Read(position, exit, hole: true).a, Is.EqualTo(1).Within(.0001f));
        }

        [Test]
        public void ReportedDistantTransformDoesNotSnapItsJoinWhenMovedSlightly() {
            var exit = -(Quaternion.Euler(358, -49, 1.7f) * Vector3.forward);
            foreach (var positionScale in new[] { .01f, .05f, .1f, .2f }) {
                var position = new Vector3(-.484f, .019f, 2.249f) * positionScale;
                var advance = Read(position, exit, joins: true, maxAdvance: 1.6f).r;
                foreach (var direction in new[] { Vector3.right, Vector3.up, Vector3.forward }) {
                    var moved = Read(position + direction * .00001f, exit, joins: true, maxAdvance: 1.6f).r;
                    Assert.That(moved, Is.EqualTo(advance).Within(.0001f));
                }
            }
        }

        [Test]
        public void CoincidentStraightTargetJoinsImmediately() {
            foreach (var rotation in new[] { Quaternion.identity, Quaternion.Euler(35, 67, 19) }) {
                var result = Read(
                    Vector3.zero,
                    Vector3.forward,
                    entrance: false,
                    joins: true,
                    rotation: rotation
                );
                Assert.That(result.r, Is.Zero);
                Assert.That(result.b, Is.EqualTo(1));
                Assert.That(result.a, Is.Zero);
            }
        }

        [Test]
        public void JoinsScaleWithAvatarAndAreInvariantUnderRotation() {
            var position = new Vector3(.03f, .17f, .08f);
            var exit = new Vector3(.1f, .9f, -.4f).normalized;
            var original = Read(position, exit, joins: true);
            foreach (var scale in new[] { .01f, 100f }) {
                var transformed = Read(
                    position,
                    exit,
                    joins: true,
                    scale: scale,
                    rotation: Quaternion.Euler(35, 67, 19)
                );
                Assert.That(transformed.r / scale, Is.EqualTo(original.r).Within(.0001f));
                Assert.That(transformed.g / scale, Is.EqualTo(original.g).Within(.0001f));
                Assert.That(transformed.b, Is.EqualTo(original.b));
                Assert.That(transformed.a, Is.EqualTo(original.a));
            }
        }

        [TestCase(.475f, .025f, 50, true, .45792847f)]
        [TestCase(.525f, .1f, 35, true, .43804144f)]
        [TestCase(.1f, 0, -105, false, 0)]
        [TestCase(.45f, .175f, 175, true, .21355416f)]
        [TestCase(.6f, .075f, 155, true, .32154404f)]
        [TestCase(.275f, .125f, -175, true, .38083706f)]
        public void MatchesVisualizedPrototypeCases(float y, float z, float angle, bool allowed, float advance) {
            var position = new Vector3(0, y, z) * .4f;
            var exit = Quaternion.Euler(angle, 0, 0) * Vector3.forward;
            var result = Read(position, exit, joins: true, maxAdvance: 1.6f);
            Assert.That(result.b, Is.EqualTo(allowed ? 1 : 0));
            if (allowed) Assert.That(result.r, Is.EqualTo(advance * .4f).Within(.00001f));
            Assert.That(Read(position, exit, hole: true, maxAdvance: 1.6f).a, Is.EqualTo(allowed ? 1 : 0).Within(.0001f));
        }

        [TestCase(2, true)]
        [TestCase(2.572f, true)]
        [TestCase(3, true)]
        public void ReportedBackFacePoseDependsOnItsRatioToPlugLength(float localLength, bool allowed) {
            var position = new Vector3(-.127f, -.026f, 2.572f) * (.4f / localLength);
            var exit = -(Quaternion.Euler(358, -44, 1.6f) * Vector3.forward);
            Assert.That(Read(position, exit, joins: true, maxAdvance: 1.6f).b, Is.EqualTo(allowed ? 1 : 0));
            Assert.That(Read(position, exit, hole: true, maxAdvance: 1.6f).a, Is.GreaterThan(0));
        }

        [Test]
        public void AngledHoleHasNoExtraRejectionBandAcrossTheRoot() {
            foreach (var side in new[] { -1f, 1f }) {
                var exit = new Vector3(side, 0, 1).normalized;
                foreach (var x in new[] { -.02f, -.01f, -.001f, .001f, .01f, .02f }) {
                    foreach (var z in new[] { -.02f, -.01f, -.001f, .001f, .01f, .02f }) {
                        var position = new Vector3(x, 0, z);
                        Assert.That(Read(position, exit, hole: true).a, Is.EqualTo(1).Within(.0001f),
                            $"side={side}, x={x}, z={z}");
                    }
                }
            }
        }

        [Test]
        public void ReportedPoseHasNoExtraRejectionBandWhenMovedForwardOrBack() {
            var exit = -(Quaternion.Euler(358, -44, 1.6f) * Vector3.forward);
            foreach (var x in new[] { -.25f, -.127f, -.05f, 0, .05f, .127f, .25f }) {
                foreach (var z in new[] { 2.4f, 2.5f, 2.572f, 2.65f, 2.75f }) {
                    var position = new Vector3(x, -.026f, z) * (.4f / 2.572f);
                    Assert.That(Read(position, exit, hole: true, maxAdvance: 1.6f).a, Is.GreaterThan(0), $"x={x}, z={z}");
                }
            }
        }

        [Test]
        public void FoldBoundsCanRequireMoreAdvanceOrForbidFurtherAdvance() {
            var forward = Read(new Vector3(0, .02f, 0), Vector3.forward, joins: true);
            Assert.That(forward.b, Is.EqualTo(1));
            Assert.That(forward.r, Is.GreaterThan(0));
            Assert.That(Read(new Vector3(0, .02f, 0), Vector3.forward, entrance: false, joins: true).b, Is.Zero);

            var backward = Read(new Vector3(0, .2f, 0), Vector3.back, joins: true);
            Assert.That(backward.b, Is.EqualTo(1));
            Assert.That(backward.r, Is.Zero);
            var tooFar = new Vector3(0, .2f, -.4f);
            Assert.That(Read(tooFar, Vector3.back, entrance: false, joins: true).b, Is.Zero);
            Assert.That(Read(tooFar, Vector3.back, joins: true).b, Is.Zero,
                "Advancing cannot repair an already exceeded upper fold bound");
        }

        // Evaluate the geometric inequalities directly, independently of the shader's
        // quadratic inverse. All inputs here use a 0.4m plug and bend room 0.2.
        private static bool MeetsJoinBounds(Vector3 position, Vector3 exit, float advance, double tolerance) {
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

        [Test]
        public void InverseFindsTheFirstAllowedJoinOrReportsNoSolution() {
            var random = new System.Random(798326);
            float Coordinate() => (float)(random.NextDouble() * .6 - .3);
            for (var i = 0; i < 128; i++) {
                var position = new Vector3(Coordinate(), Coordinate(), Coordinate());
                var exit = new Vector3(Coordinate(), Coordinate(), Coordinate()).normalized;
                var result = Read(position, exit, joins: true);
                if (result.b == 1) {
                    Assert.That(MeetsJoinBounds(position, exit, result.r, .00002), Is.True, $"case={i}");
                    if (result.r > .0004f) {
                        Assert.That(MeetsJoinBounds(position, exit, result.r - .0004f, 1e-8), Is.False,
                            $"A smaller advance was already permissible: case={i}");
                    }
                } else {
                    for (var sample = 0; sample <= 128; sample++) {
                        Assert.That(MeetsJoinBounds(position, exit, sample * .01f, 1e-8), Is.False,
                            $"A permissible join was missed: case={i}, sample={sample}");
                    }
                }
            }
        }
    }
}
