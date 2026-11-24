namespace VF.Tests {
    using Party = VF.Builder.Haptics.SelfAndOthersReceiverService.Party;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;
    using NUnit.Framework;
    using UnityEditor.Animations;
    using UnityEngine;
    using UnityEngine.Animations;
    using UnityEngine.Playables;
    using VF.Builder;
    using VF.Builder.Haptics;
    using VF.Component;
    using VF.Injector;
    using VF.Inspector;
    using VF.Service;
    using VF.Utils;
    using VF.Utils.Controller;
    using VRC.SDK3.Avatars.Components;
    using VRC.SDK3.Dynamics.Contact.Components;
    using VRC.Dynamics;
    using VRC.SDKBase;
    using Object = UnityEngine.Object;

    internal class SpsPlugRadiusServiceTest : SpsPlugRadiusTestBase {
        [TestCase(1f, false)]
        [TestCase(3f, false)]
        [TestCase(1f, true)]
        public void DisabledPlugResetsRadiusSamplingAndKeepsSenderInvertible(float resize, bool emptyProfile) {
            if (emptyProfile) bake.radiusCurve = AnimationCurve.Constant(0, 1, 0);
            var defaultScale = bake.mainSender.localScale;
            var enabled = fx.GetOrCreateFloat("TestPlugEnabled", 1);
            var on = VFClip.Create("Plug on"); on.SetEnabled(bake.bakeRoot, true);
            var off = VFClip.Create("Plug off"); off.SetEnabled(bake.bakeRoot, false);
            fx.NewLayer("Plug toggle").NewState("Plug toggle").WithAnimation(
                VFBlendTree1D.CreateWithData("Plug toggle", enabled, (0, off), (1, on)));
            Start();
            root.transform.localScale *= resize;
            playable.SetFloat("TestInverseWorldScale", 1 / bake.bakeRoot.worldScale.x);
            SetMergedProximity(1);
            for (var i = 0; i < 4; i++) graph.Evaluate(1f / 60);
            playable.SetFloat(enabled, 0);
            // Decode the initially-on object's delta, then propagate the gate and radius reset.
            for (var i = 0; i < 3; i++) graph.Evaluate(1f / 60);
            var position = fx.parameters.Single(p => p.name.EndsWith("Test - Socket position"));
            Assert.That(playable.GetFloat(position), Is.EqualTo(1));
            Assert.That(bake.mainSender.worldScale.y, Is.EqualTo(bake.worldRadius).Within(0.0001f));
            Assert.That(bake.mainSender.worldScale.x, Is.GreaterThan(0));
            Assert.That(bake.mainSender.worldScale.z, Is.EqualTo(bake.worldLength).Within(0.0001f));
            Assert.That(bake.mainSender.localToWorldMatrix.determinant, Is.GreaterThan(0));
            Assert.That(bake.mainSender.localScale, Is.EqualTo(defaultScale));
            Assert.That(playable.IsParameterControlledByCurve(mergedParameterName), Is.False);
            playable.SetFloat(enabled, 1);
            playable.SetFloat(mergedParameterName + "_PointSeparation", bake.worldLength / 2);
            for (var i = 0; i < 4; i++) graph.Evaluate(1f / 60);
            Assert.That(playable.GetFloat(position), Is.EqualTo(0.5f).Within(0.0001f));
            var expected = emptyProfile ? MinimumRadius
                : Mathf.Lerp(0.06f, MinimumRadius, 0.5f);
            Assert.That(bake.mainSender.worldScale.y, Is.EqualTo(expected * resize).Within(0.0001f));
        }

        [Test]
        public void RadiusSamplingOnlyAnimatesSenderScaleAndPreservesBakedRadius() {
#if !VRCSDK_HAS_MATERIAL_CONTACTS
            Assert.Ignore("SPS deformation requires material contact support.");
#endif
            var renderer = bake.renderers.Single();
            renderer.spsBlendshapes = new List<string>();
            var material = new Material(Shader.Find("Unlit/Color"));
            assets.Add(material);
            SpsPatcher.Patch(material, false, false);
            material.SetFloat(SpsConfigurer.SpsBakedRadius, 0.06f);
            renderer.renderer.sharedMaterial = material;
            for (var pass = 0; pass < material.passCount; pass++) UnityEditor.ShaderUtil.CompilePass(material, pass, true);
            Assert.That(UnityEditor.ShaderUtil.ShaderHasError(material.shader), Is.False);
            Start();
            Assert.That(assets.OfType<AnimatorController>().Single().animationClips
                .SelectMany(UnityEditor.AnimationUtility.GetCurveBindings)
                .Any(binding => binding.propertyName.StartsWith("material.")), Is.False);
            var parameterCount = fx.parameters.Length;
            foreach (var resize in new[] { 0.5f, 1f, 3f }) foreach (var distance in new[] { 0f, 0.5f, 1f, 1.5f }) {
                root.transform.localScale = Vector3.one * (2 * resize);
                playable.SetFloat("TestInverseWorldScale", 1 / bake.bakeRoot.worldScale.x);
                SetMergedProximity(1);
                playable.SetFloat(mergedParameterName + "_PointSeparation", distance * bake.worldLength);
                for (var i = 0; i < 20; i++) graph.Evaluate(1f / 60);
                Assert.That(material.GetFloat(SpsConfigurer.SpsBakedRadius), Is.EqualTo(0.06f));
                Assert.That(bake.mainSender.worldScale.y, Is.GreaterThan(0), "A zero sampled radius must not make the sender matrix singular");
            }
            Assert.That(fx.parameters.Length, Is.EqualTo(parameterCount));
        }

        [Test]
        public void BaseActivationRadiusOverrideFollowsUpperLegScaleInsteadOfPlugScale(
            [Values(0.5f, 2f)] float upperLegLocalScale,
            [Values(HumanBodyBones.Hips, HumanBodyBones.Head)] HumanBodyBones closest,
            [Values] bool missingLeg
        ) {
#if !VRCSDK_HAS_MATERIAL_CONTACTS
            Assert.Ignore("SPS deformation requires material contact support.");
#endif
            var upperLeg = GameObjects.Create("Upper leg", root);
            upperLeg.localScale = Vector3.one * upperLegLocalScale;
            var uploadScale = upperLeg.worldScale.x;
            var otherLeg = GameObjects.Create("Other upper leg", root);
            otherLeg.localPosition = new Vector3(0.2f, 0, 0);
            var localRadius = Vector3.Distance(upperLeg.worldPosition, otherLeg.worldPosition) * 0.5f / uploadScale;
            typeof(SpsPlugRadiusService).VFField("autoChannelGenerator").SetValue(service, new LegSpacingBones {
                closest = closest,
                left = upperLeg,
                right = missingLeg ? null : otherLeg
            });
            var overridden = closest == HumanBodyBones.Hips && !missingLeg;
            var renderer = bake.renderers.Single();
            renderer.spsBlendshapes = new List<string>();
            var material = new Material(Shader.Find("Unlit/Color"));
            assets.Add(material);
            SpsPatcher.Patch(material, false, false);
            Assert.That(material.GetFloat(SpsConfigurer.SpsBaseActivationRadiusOverride), Is.Zero);
            renderer.configureMaterial = (slot, source) => {
                if (slot != 0 || source == null) return source;
                var copy = new Material(source);
                assets.Add(copy);
                return copy;
            };
            renderer.renderer.sharedMaterial = material;
            for (var pass = 0; pass < material.passCount; pass++) UnityEditor.ShaderUtil.CompilePass(material, pass, true);
            Assert.That(UnityEditor.ShaderUtil.ShaderHasError(material.shader), Is.False);
            var detector = injector.GetService<WorldScaleDetectorService>();
            var raw = detector.GetRawWorldScale(upperLeg, "SPS Leg Spacing");
            var cache = (Dictionary<VFGameObject, (VFAParam raw, float multiplier, Lazy<VFAParam> inverse)>)
                typeof(WorldScaleDetectorService).VFField("cache").GetValue(detector);
            Start(() => {
                var initialRadius = overridden ? localRadius * uploadScale : 0;
                var configured = renderer.configureMaterial(0, material);
                Assert.That(configured, Is.Not.SameAs(material));
                Assert.That(configured.GetFloat(SpsConfigurer.SpsBaseActivationRadiusOverride),
                    Is.EqualTo(initialRadius).Within(0.0001f));
                renderer.renderer.sharedMaterial = configured;
                var swapSource = new Material(material);
                assets.Add(swapSource);
                var swap = renderer.configureMaterial(0, swapSource);
                Assert.That(swap.GetFloat(SpsConfigurer.SpsBaseActivationRadiusOverride),
                    Is.EqualTo(initialRadius).Within(0.0001f));
                Assert.That(renderer.configureMaterial(1, material), Is.SameAs(material));
                Assert.That(renderer.configureMaterial(0, null), Is.Null);
                Assert.That(material.GetFloat(SpsConfigurer.SpsBaseActivationRadiusOverride), Is.Zero);
                Assert.That(swapSource.GetFloat(SpsConfigurer.SpsBaseActivationRadiusOverride), Is.Zero);
            });
            var block = new MaterialPropertyBlock();
            foreach (var plugScale in new[] { 0.5f, 3f }) {
                bake.bakeRoot.localScale = Vector3.one * plugScale;
                foreach (var legScale in new[] { 0.5f, 1f, 3f, 2 * WorldScaleDetectorService.MaxDetectedScale * uploadScale }) {
                    playable.SetFloat(raw.name, legScale / (WorldScaleDetectorService.MaxDetectedScale * uploadScale));
                    for (var i = 0; i < 20; i++) graph.Evaluate(1f / 60);
                    renderer.renderer.GetPropertyBlock(block);
                    Assert.That(block.GetFloat(SpsConfigurer.SpsBaseActivationRadiusOverride),
                        Is.EqualTo(overridden ? localRadius * legScale : 0).Within(0.0001f));
                }
            }
            Assert.That(cache.Keys.Count(obj => obj == upperLeg), Is.EqualTo(1));
            Assert.That(cache[upperLeg].inverse.IsValueCreated, Is.False);
        }

        [TestCase(0.01f)]
        [TestCase(0.25f)]
        [TestCase(0.75f)]
        [TestCase(1f)]
        [TestCase(1.5f)]
        public void SamplesCurveAtNormalizedSocketDistanceAndTracksAvatarScale(float distance) {
            Assert.That(bake.radiusCurve.keys.First().time, Is.Zero);
            Assert.That(bake.radiusCurve.keys.Last().time, Is.EqualTo(1));
            Assert.That(bake.radiusCurve.Evaluate(distance),
                Is.EqualTo(Mathf.Lerp(0.06f, 0, Mathf.Clamp01(distance))).Within(0.000001f));
            Start();
            var self = SourceReceiver(true);
            var other = SourceReceiver(false);
            // Actual receiver proximity for a point sender distance*plugLength from the plug base.
            playable.SetFloat(self.parameter, Mathf.Clamp01(1 - distance / SpsPlugReceiverService.RangeInPlugLengths));
            playable.SetFloat(other.parameter, 0);
            SetMergedProximity(
                Mathf.Clamp01(1 - distance / SpsPlugReceiverService.RangeInPlugLengths));
            foreach (var scale in new[] { 1f, 3f, 0.5f }) {
                root.transform.localScale = Vector3.one * (2 * scale);
                playable.SetFloat("TestInverseWorldScale", 1 / bake.bakeRoot.worldScale.x);
                playable.SetFloat(mergedParameterName + "_PointSeparation", distance * bake.worldLength);
                for (var i = 0; i < 20; i++) graph.Evaluate(1f / 60);
                var actual = bake.mainSender.worldScale;
                var expected = Mathf.Lerp(0.06f, MinimumRadius, Mathf.Clamp01(distance)) * scale;
                Assert.That(actual.y, Is.EqualTo(expected).Within(0.00002f));
                Assert.That(actual.y, Is.GreaterThan(0));
                Assert.That(actual.x, Is.EqualTo(0.06f * scale).Within(0.00002f));
                Assert.That(actual.z, Is.EqualTo(0.4f * scale).Within(0.00002f));
                var sender = bake.mainSender.GetComponent<VRCContactSender>();
                Assert.That(sender.radius * Mathf.Max(actual.x, actual.y, actual.z), Is.EqualTo(0.4f * scale).Within(0.00002f));
                Assert.That(self.radius * self.transform.lossyScale.x, Is.EqualTo(0.64f * scale).Within(0.00002f));
            }
        }

        [TestCase(1)]
        [TestCase(10)]
        public void SamplesPointSeparationIndependentlyOfProximityAndItsThreeMeterCap(float scale) {
            Start();
            root.transform.localScale = Vector3.one * (2 * scale);
            playable.SetFloat("TestInverseWorldScale", 1 / bake.bakeRoot.worldScale.x);
            var distance = 0.5f * bake.worldLength;
            foreach (var proximity in new[] { 1 - distance / Mathf.Min(3, 0.64f * scale), 1f, 0f }) {
                playable.SetFloat(mergedParameterName, proximity);
                playable.SetFloat(mergedParameterName + "_PointSeparation", distance);
                for (var i = 0; i < 8; i++) graph.Evaluate(1f / 60);
                Assert.That(bake.mainSender.worldScale.y,
                    Is.EqualTo(Mathf.Lerp(0.06f, MinimumRadius, 0.5f) * scale).Within(0.0001f));
            }
            playable.SetFloat(mergedParameterName, 0);
            foreach (var idleSeparation in new[] { 0f, -1f }) {
                playable.SetFloat(mergedParameterName + "_PointSeparation", idleSeparation);
                for (var i = 0; i < 2; i++) graph.Evaluate(1f / 60);
                Assert.That(bake.mainSender.worldScale.y, Is.EqualTo(MinimumRadius * scale).Within(0.0001f));
            }
            playable.SetFloat(mergedParameterName, 1);
            const float baseDistance = 0.002f;
            playable.SetFloat(mergedParameterName + "_PointSeparation", baseDistance);
            graph.Evaluate(1f / 60);
            Assert.That(bake.mainSender.worldScale.y, Is.EqualTo(MinimumRadius * scale).Within(0.0001f));
            graph.Evaluate(1f / 60);
            Assert.That(bake.mainSender.worldScale.y,
                Is.EqualTo(Mathf.Lerp(0.06f, MinimumRadius, baseDistance / bake.worldLength) * scale).Within(0.0001f));
        }

        [TestCase(0.5f)]
        [TestCase(2f)]
        public void PostBakeResizingPreservesProfilePositionAndLocalRadius(float scale) {
            root.transform.localScale *= scale;
            Start();
            playable.SetFloat("TestInverseWorldScale", 1 / bake.bakeRoot.worldScale.x);
            playable.SetFloat(mergedParameterName, 1 - 0.5f / SpsPlugReceiverService.RangeInPlugLengths);
            playable.SetFloat(mergedParameterName + "_PointSeparation", bake.worldLength / 2);
            for (var i = 0; i < 4; i++) graph.Evaluate(1f / 60);
            var position = fx.parameters.Single(p => p.name.EndsWith("Test - Socket position"));
            Assert.That(playable.GetFloat(position), Is.EqualTo(0.5f).Within(0.0001f));
            Assert.That(bake.mainSender.worldScale.y,
                Is.EqualTo(Mathf.Lerp(0.06f, MinimumRadius, 0.5f) * scale).Within(0.0001f));
            Assert.That(bake.mainSender.worldScale.z, Is.EqualTo(bake.worldLength).Within(0.0001f));
        }

        [Test]
        public void SamplesNearestPartyAndClampsToTipWithoutContacts() {
            Start();
            var self = SourceReceiver(true);
            var other = SourceReceiver(false);
            Assert.That(self.collisionTags, Is.EqualTo(new[] { SelfTag("SPS/Socket/first") }));
            Assert.That(other.collisionTags, Is.EqualTo(new[] { "SPS/Socket/second" }));
            var quarterRadius = Mathf.Lerp(0.06f, MinimumRadius, 0.25f);
            foreach (var values in new[] { (0.75f, 0.25f, quarterRadius), (0.25f, 0.75f, quarterRadius), (2f, 2f, 0f) }) {
                playable.SetFloat(self.parameter, Mathf.Clamp01(1 - values.Item1 / SpsPlugReceiverService.RangeInPlugLengths));
                playable.SetFloat(other.parameter, Mathf.Clamp01(1 - values.Item2 / SpsPlugReceiverService.RangeInPlugLengths));
                SetMergedProximity(
                    Mathf.Clamp01(1 - Mathf.Min(values.Item1, values.Item2) / SpsPlugReceiverService.RangeInPlugLengths));
                for (var i = 0; i < 20; i++) graph.Evaluate(1f / 60);
                Assert.That(bake.mainSender.worldScale.y,
                    Is.EqualTo(Mathf.Max(MinimumRadius, values.Item3)).Within(0.00002f));
                Assert.That(bake.mainSender.worldScale.y, Is.GreaterThan(0));
            }
        }

        [TestCase(true, 0f)]
        [TestCase(true, 0.75f)]
        [TestCase(true, 2f)]
        [TestCase(false, 1f)]
        [TestCase(false, 1.5f)]
        [TestCase(false, 2f)]
        public void ZeroRadiusSlicesKeepSenderTransformInvertible(bool emptyProfile, float distance) {
            if (emptyProfile) bake.radiusCurve = AnimationCurve.Constant(0, 1, 0);
            Start();
            SetMergedProximity( Mathf.Clamp01(1 - distance / SpsPlugReceiverService.RangeInPlugLengths));
            foreach (var scale in new[] { 0.1f, 1f, 3f }) {
                root.transform.localScale = Vector3.one * (2 * scale);
                playable.SetFloat("TestInverseWorldScale", 1 / bake.bakeRoot.worldScale.x);
                playable.SetFloat(mergedParameterName + "_PointSeparation", distance * bake.worldLength);
                for (var i = 0; i < 4; i++) graph.Evaluate(1f / 60);
                var senderScale = bake.mainSender.worldScale;
                Assert.That(senderScale.x, Is.GreaterThan(0));
                Assert.That(senderScale.y, Is.GreaterThan(0));
                Assert.That(senderScale.z, Is.GreaterThan(0));
                Assert.That(senderScale.y, Is.EqualTo(MinimumRadius * scale).Within(0.00000001f));
                var matrix = bake.mainSender.localToWorldMatrix;
                Assert.That(matrix.determinant, Is.GreaterThan(0));
                var inverse = matrix.inverse;
                for (var i = 0; i < 16; i++) Assert.That(float.IsNaN(inverse[i]) || float.IsInfinity(inverse[i]), Is.False);
                Assert.That(bake.mainSender.GetComponent<VRCContactSender>().radius
                    * Mathf.Max(senderScale.x, senderScale.y, senderScale.z), Is.EqualTo(0.4f * scale).Within(0.00001f));
            }
        }

        [Test]
        public void ReciprocalDoesNotBlendInItsNonzeroParameterDefault() {
            var tree = VFBlendTreeDirect.Create("Reciprocal regression");
            fx.NewLayer("Reciprocal regression").NewState("DBT").WithAnimation(tree);
            var input = fx.CreateUniqueFloat("Reciprocal input", def: 2);
            var reciprocal = new BlendtreeMath(fx, tree).Invert("Reciprocal output", input);
            Start();
            foreach (var value in new[] { 2f, 4f, 0.5f }) {
                playable.SetFloat(input.name, value);
                for (var i = 0; i < 10; i++) graph.Evaluate(1f / 60);
                Assert.That(playable.GetFloat(reciprocal.name), Is.EqualTo(1 / value).Within(0.0001f));
            }
        }

        [Test]
        public void ManualPlugWithoutMeshUsesConstantRadiusUntilTip() {
            plug.configureTpsMesh.Clear();
            var injector = new VRCFuryInjector(); injector.ImportScan(typeof(VFServiceAttribute));
            bake = injector.GetService<VRCFuryHapticPlugBaker>().Bake(plug);
            foreach (var depth in new[] { 0f, 0.25f, 0.75f })
                Assert.That(bake.radiusCurve.Evaluate(depth), Is.EqualTo(0.06f).Within(0.00001f));
            Assert.That(bake.radiusCurve.Evaluate(1), Is.Zero);
        }
    }
}
