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
        float3 _Position, _Forward, _Up, _Exit;
        float _Length, _CaptureLength, _Guided, _Entrance, _Flags, _Operation, _BaseRadius;
        float4 frag(v2f_img input) : SV_Target {
            if (_Operation == 2) {
                float weight = sps_deformation_weight(_Position, _Forward, _Up, _Length, .02, _BaseRadius, 0);
                return float4(weight, weight, weight, weight);
            }
            if (_Operation > 0) {
                float advance = _Entrance > 0
                    ? sps_entry_advance_distance(-_Position, _Forward, _Exit, _Length) : 0;
                return float4(
                    sps_bend_sharpness_sq(_Position + _Exit * advance, _Forward, _Exit, _Length),
                    sps_bend_sharpness_sq(_Position - _Exit * advance, _Forward, -_Exit, _Length),
                    advance,
                    1
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
            bool scores = false,
            bool hole = false,
            float scale = 1,
            Quaternion rotation = default,
            float captureLength = .4f,
            int unlock = 0,
            float baseRadius = 1,
            float verticalAngle = 89,
            bool captureOnly = false,
            float horizontalAngle = 45
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
            material.SetFloat("_Guided", guided ? 1 : 0);
            material.SetFloat("_Entrance", entrance ? 1 : 0);
            material.SetFloat("_Flags", (twoWay ? 1 << 13 : 0) | (hole ? 1 << 14 : 0) | unlock);
            material.SetFloat("_Operation", captureOnly ? 2 : scores ? 1 : 0);
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
            Assert.That(Read(new Vector3(0, 0, -.12f), Vector3.forward, baseRadius: .1f).a, Is.EqualTo(1).Within(.0001f));
            Assert.That(Read(new Vector3(0, 0, -.145f), Vector3.forward, baseRadius: .1f).a, Is.EqualTo(1f / 6).Within(.0001f));
            Assert.That(Read(new Vector3(0, 0, -.151f), Vector3.forward, baseRadius: .1f).a, Is.Zero);
            Assert.That(Read(new Vector3(.101f, 0, -.12f), Vector3.forward, baseRadius: .1f).a, Is.Zero);
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
                var position = new Vector3(0, up * .2f, 0);
                var exit = Quaternion.Euler(angle, 0, 0) * Vector3.forward;
                Assert.That(Read(position, exit, hole: hole).a, Is.EqualTo(1).Within(.0001f));
                Assert.That(Read(position, -exit, hole: hole).a, Is.EqualTo(1).Within(.0001f));
            }
        }

        [Test]
        public void DownwardHoleAtMouthRejectsConcentratedExitReversal() {
            foreach (var angle in new[] { 80f, 85f, 90f, 95f, 100f }) {
                var exit = Quaternion.Euler(angle, 0, 0) * Vector3.forward;
                foreach (var forward in new[] { .1f, .2f })
                foreach (var scale in new[] { 1f, 2f }) {
                    var position = new Vector3(0, .2f, forward);
                    var oneWay = Read(position, exit, hole: true, scale: scale);
                    Assert.That(oneWay.a, Is.Zero, "angle=" + angle);
                    var twoWay = Read(position, exit, twoWay: true, hole: true, scale: scale);
                    Assert.That(twoWay.a, Is.EqualTo(1).Within(.0001f));
                    Assert.That(twoWay.g, Is.GreaterThan(.9f));
                }
            }
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
        public void TwoWayChoosesGentlerRouteIncludingLookahead() {
            var position = new Vector3(0, .2f, 0);
            var exit = Quaternion.Euler(5, 0, 0) * Vector3.forward;
            var result = Read(position, exit, twoWay: true);
            Assert.That(result.a, Is.EqualTo(1).Within(.0001f));
            Assert.That(Vector3.Dot(new Vector3(result.r, result.g, result.b), exit), Is.GreaterThan(.999f));
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
            var entrance = Read(position, Vector3.forward, scores: true);
            var laterStop = Read(position, Vector3.forward, entrance: false, scores: true);
            Assert.That(entrance.b, Is.EqualTo(.11f).Within(.0001f));
            Assert.That(laterStop.b, Is.Zero);
            Assert.That(Mathf.Abs(entrance.r - laterStop.r), Is.GreaterThan(.0001f));
        }

        [Test]
        public void SharpnessUsesFullPlugLengthRatherThanRemainingReach() {
            var position = new Vector3(0, .005f, 0);
            Assert.That(Read(position, Vector3.down).a, Is.Zero);
            Assert.That(Read(position, Vector3.down, captureLength: .1f).a, Is.Zero);
        }

        [Test]
        public void CoincidentStraightTargetHasNoBendPenalty() {
            foreach (var rotation in new[] { Quaternion.identity, Quaternion.Euler(35, 67, 19) }) {
                var result = Read(
                    Vector3.zero,
                    Vector3.forward,
                    entrance: false,
                    scores: true,
                    rotation: rotation
                );
                Assert.That(result.r, Is.Zero);
                Assert.That(result.g, Is.GreaterThan(100));
            }
        }

        [Test]
        public void ScoresAreInvariantUnderScaleAndRotation() {
            var position = new Vector3(.03f, .17f, .08f);
            var exit = new Vector3(.1f, .9f, -.4f).normalized;
            var original = Read(position, exit, scores: true);
            foreach (var scale in new[] { .01f, 100f }) {
                var transformed = Read(
                    position,
                    exit,
                    scores: true,
                    scale: scale,
                    rotation: Quaternion.Euler(35, 67, 19)
                );
                Assert.That(transformed.r, Is.EqualTo(original.r).Within(Mathf.Max(.001f, original.r * .0001f)));
                Assert.That(transformed.g, Is.EqualTo(original.g).Within(Mathf.Max(.001f, original.g * .0001f)));
            }
        }
    }
}
