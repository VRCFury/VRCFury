using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;
using VF.Builder;
using VF.Injector;
using VF.Service;
using VF.Utils;
using VF.Utils.Controller;
using VRC.SDK3.Avatars.Components;
using VRC.SDK3.Dynamics.Contact.Components;
using VRC.SDK3.Dynamics.PhysBone.Components;
using Object = UnityEngine.Object;

namespace VF.Tests {
    [Category("VRCFury")]
    public class BlendtreeMathTest {
        private GameObject root;
        private VRCFuryInjector injector;
        private VFAvatarController fx;
        private VFBlendTreeDirect tree;
        private BlendtreeMath math;
        private PlayableGraph graph;
        private AnimatorControllerPlayable playable;
        private readonly List<Object> assets = new();

        [SetUp]
        public void SetUp() {
            root = new GameObject("Blendtree math tests");
            var avatar = root.AddComponent<VRCAvatarDescriptor>();
            avatar.customizeAnimationLayers = true;
            avatar.baseAnimationLayers = new[] { new VRCAvatarDescriptor.CustomAnimLayer { type = VRCAvatarDescriptor.AnimLayerType.FX } };
            avatar.specialAnimationLayers = Array.Empty<VRCAvatarDescriptor.CustomAnimLayer>();
            injector = VRCFuryInjectorBuilder.GetInjector(avatar);
            var controllers = injector.GetService<ControllersService>();
            controllers.ClearCache(applyBaseMask: false);
            fx = controllers.GetFx();
            fx.GetOrCreateFloat(VFBlendTreeDirect.AlwaysOneParam, 1);
            tree = VFBlendTreeDirect.Create("Math");
            fx.NewLayer("Math").NewState("Math").WithAnimation(tree);
            math = new BlendtreeMath(fx, tree);
        }

        [TearDown]
        public void TearDown() {
            if (graph.IsValid()) graph.Destroy();
            Object.DestroyImmediate(root);
            foreach (var asset in assets.Distinct()) if (asset != null) Object.DestroyImmediate(asset);
            assets.Clear();
        }

        private void Start() {
            injector.GetService<NoBadControllerParamsService>().Apply();
            injector.GetService<FixPartiallyWeightedAapsService>().Apply();
            var context = new VFSaveContext(root, false);
            var controller = new AnimatorController { parameters = fx.parameters.Select(p => p.Save()).ToArray() };
            controller.layers = fx.GetLayers().Select(layer => layer.Save(context)).ToArray();
            assets.Add(controller);
            assets.AddRange(context.NewAssets);
            var animator = root.AddComponent<Animator>();
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            graph = PlayableGraph.Create("Blendtree math");
            graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
            playable = AnimatorControllerPlayable.Create(graph, controller);
            AnimationPlayableOutput.Create(graph, "Math", animator).SetSourcePlayable(playable);
            graph.Play();
        }

        private void Evaluate(int frames = 1) {
            for (var i = 0; i < frames; i++) graph.Evaluate(1f / 60);
        }

        [TestCase(false, false)]
        [TestCase(false, true)]
        [TestCase(true, false)]
        [TestCase(true, true)]
        public void ConstantOneComparisonsFoldAtRootsAndInSubtrees(bool external, bool atRoot) {
            var input = fx.CreateUniqueFloat("Constant comparison input", 1);
            if (external) input.MakeExpressionFloat(synced: false);
            var output = fx.CreateUniqueFloat("Comparison output");
            var motion = BlendtreeMath.GreaterThan(input, 0).create(output.MakeSetter(1), output.MakeSetter(0));
            if (atRoot) fx.GetLayers().Single().allStates.Single().WithAnimation(motion);
            else tree.Add(motion);
            injector.GetService<TreeFlatteningService>().Optimize();
            Assert.That(new AnimatorIterator.Trees().From(fx).Any(t => t.BlendParameter == input.name), Is.EqualTo(external));
            Start();
            Evaluate();
            Assert.That(playable.GetFloat(output), Is.EqualTo(1));
            if (external) {
                playable.SetFloat(input, 0);
                Evaluate();
                Assert.That(playable.GetFloat(output), Is.Zero);
            }
        }

        [TestCase("Contact")]
        [TestCase("Bone_Angle")]
        [TestCase("Bone_Stretch")]
        [TestCase("Bone_Squish")]
#if VRCSDK_HAS_CONTACT_PARAMETER_FLAGS
        [TestCase("Contact_PointSeparation")]
        [TestCase("Contact_SenderVelocityMagnitude")]
        [TestCase("Contact_CollisionTag_Test")]
#endif
        public void DynamicsDefaultOneComparisonsStillUpdate(string parameter) {
            // Include inactive objects: they may be enabled by an animation at runtime.
            var dynamics = new GameObject("Dynamics");
            dynamics.transform.SetParent(root.transform, false);
            dynamics.SetActive(false);
            var contact = dynamics.AddComponent<VRCContactReceiver>();
            contact.parameter = "Contact";
            contact.collisionTags = new List<string> { "Test" };
#if VRCSDK_HAS_CONTACT_PARAMETER_FLAGS
            contact.advancedParameters = VRC.Dynamics.ContactReceiver.AdvancedParameter.All;
#endif
            dynamics.AddComponent<VRCPhysBone>().parameter = "Bone";
            var input = fx.GetOrCreateFloat(parameter, 1);
            var output = fx.CreateUniqueFloat("Comparison output");
            tree.Add(BlendtreeMath.GreaterThan(input, 0).create(output.MakeSetter(1), output.MakeSetter(0)));
            injector.GetService<TreeFlatteningService>().Optimize();
            Start();
            Evaluate();
            Assert.That(playable.GetFloat(output), Is.EqualTo(1));
            playable.SetFloat(input, 0);
            Evaluate();
            Assert.That(playable.GetFloat(output), Is.Zero);
        }

        [Test]
        public void ConstantOneInsideMappingRangeStillInterpolates() {
            var input = fx.CreateUniqueFloat("Constant mapping input", 1);
            var output = math.Map("Mapped constant", input, -1, 3, 0, 4);
            injector.GetService<TreeFlatteningService>().Optimize();
            Assert.That(new AnimatorIterator.Trees().From(fx).Any(t => t.BlendParameter == input.name), Is.True);
            Start();
            Evaluate();
            Assert.That(playable.GetFloat(output), Is.EqualTo(2));
        }

        [TestCase(0f, 3f, false, true)]
        [TestCase(-2f, 3f, true, true)]
        [TestCase(-2f, 3f, false, false)]
        [TestCase(-2f, 3f, true, false)]
        [TestCase(1f, 1f, true, true)]
        [TestCase(1f, 1f, false, true)]
        public void RangeUsesOneTreeAndPreservesComparisonBoundaries(float min, float max, bool includeMin, bool includeMax) {
            var input = fx.CreateUniqueFloat("Input", min);
            var actual = fx.CreateUniqueFloat("Range");
            var expected = fx.CreateUniqueFloat("Comparisons");
            var range = BlendtreeMath.InRange(input, min, max, includeMin, includeMax);
            var comparisons = BlendtreeMath.GreaterThan(input, min, includeMin)
                .And(BlendtreeMath.LessThan(input, max, includeMax));
            var motion = range.create(actual.MakeSetter(1), actual.MakeSetter(0));
            Assert.That(new AnimatorIterator.Trees().From(motion).Count, Is.LessThanOrEqualTo(1));
            Assert.That(range.defaultIsTrue, Is.EqualTo(comparisons.defaultIsTrue));
            tree.Add(motion);
            tree.Add(comparisons.create(expected.MakeSetter(1), expected.MakeSetter(0)));
            Start();
            foreach (var value in new[] { min - 1, min, VF.Inspector.VRCFuryEditorUtils.NextFloatUp(min),
                         (min + max) / 2, VF.Inspector.VRCFuryEditorUtils.NextFloatDown(max), max, max + 1 }) {
                playable.SetFloat(input, value);
                Evaluate();
                Assert.That(playable.GetFloat(actual), Is.EqualTo(playable.GetFloat(expected)).Within(0.0001f),
                    "Input " + value);
            }
        }

        [TestCase(0f, 3f, false, true, true, true)]
        [TestCase(0f, 3f, false, true, true, false)]
        [TestCase(-2f, 3f, true, true, true, true)]
        [TestCase(-2f, 3f, true, false, false, true)]
        [TestCase(1f, 1f, true, true, true, true)]
        [TestCase(0f, 3f, true, true, false, false)]
        public void BoundedMappingCombinesValueAndValidationWithoutDelay(float min, float max,
            bool includeMin, bool includeMax, bool rejectBelow, bool rejectAbove) {
            var input = fx.CreateUniqueFloat("Input");
            var actual = fx.CreateUniqueFloat("Mapped");
            var actualValid = fx.CreateUniqueFloat("Mapped valid");
            var expected = fx.CreateUniqueFloat("Separate copy");
            var expectedValid = fx.CreateUniqueFloat("Separate validation");
            VFClip ValueClip(VFAParam output, VFAParam valid, float value, bool accepted) {
                var clip = output.MakeSetter(value);
                clip.SetAap(valid, accepted ? 1 : 0);
                return clip;
            }
            var fallback = ValueClip(actual, actualValid, -7, false);
            var motion = BlendtreeMath.MapWithFallback(input, min, max,
                value => ValueClip(actual, actualValid, 2 * value + 1, true),
                rejectBelow ? fallback : null, rejectAbove ? fallback : null, includeMin, includeMax);
            var mapping = new AnimatorIterator.Trees().From(motion).Single();
            Assert.That(mapping.children.All(child => child.motion is VFClip), Is.True);
            tree.Add(motion);

            var copy = VFBlendTreeDirect.Create("Separate copy and validation");
            copy.Add(expected.MakeCopier(input, minSupported: -10, maxSupported: 10, multiplier: 2));
            copy.Add(expected.MakeSetter(1));
            copy.Add(expectedValid.MakeSetter(1));
            var first = includeMin ? min : VF.Inspector.VRCFuryEditorUtils.NextFloatUp(min);
            var last = includeMax ? max : VF.Inspector.VRCFuryEditorUtils.NextFloatDown(max);
            var below = ValueClip(expected, expectedValid, rejectBelow ? -7 : 2 * first + 1, !rejectBelow);
            var above = ValueClip(expected, expectedValid, rejectAbove ? -7 : 2 * last + 1, !rejectAbove);
            tree.Add(BlendtreeMath.GreaterThan(input, min, includeMin).create(
                BlendtreeMath.LessThan(input, max, includeMax).create(copy, above), below));
            Start();
            foreach (var value in new[] { min - 1, VF.Inspector.VRCFuryEditorUtils.NextFloatDown(min), min,
                         VF.Inspector.VRCFuryEditorUtils.NextFloatUp(min), 0.00000001f, 0.0001f,
                         (min + max) / 2, VF.Inspector.VRCFuryEditorUtils.NextFloatDown(max), max,
                         VF.Inspector.VRCFuryEditorUtils.NextFloatUp(max), max + 1 }) {
                playable.SetFloat(input, value);
                Evaluate();
                Assert.That(playable.GetFloat(actual), Is.EqualTo(playable.GetFloat(expected)).Within(0.00001f),
                    "Mapped input " + value);
                Assert.That(playable.GetFloat(actualValid), Is.EqualTo(playable.GetFloat(expectedValid)).Within(0.00001f),
                    "Validation at input " + value);
            }
        }

        [TestCase(float.NaN, 3f, true)]
        [TestCase(0f, float.PositiveInfinity, true)]
        [TestCase(float.NegativeInfinity, 3f, false)]
        [TestCase(3f, 0f, true)]
        [TestCase(1f, 1f, false)]
        public void BoundedMappingRejectsInvalidIntervals(float min, float max, bool includeMin) {
            var input = fx.CreateUniqueFloat("Input");
            Assert.Throws<ArgumentException>(() => BlendtreeMath.MapWithFallback(input, min, max,
                value => input.MakeSetter(value), includeMin: includeMin));
        }

        [Test]
        public void AddCombinesRepeatedInputsAndConstantsWithoutAddingDelay() {
            var a = fx.CreateUniqueFloat("A", 2);
            var b = fx.CreateUniqueFloat("B", 3);
            var result = math.Add("Sum", (a, 2), (a, -1), (a, 0), (b, 0.5f), (2, 3), (-1, 4), (0, 100));
            Assert.That(result.GetDefault(), Is.EqualTo(5.5f));
            var sumTree = (VFTree)((VFTree)(VFMotion)tree).children.Single().motion;
            Assert.That(sumTree.children.Count(), Is.EqualTo(3));
            var buffer = math.Buffer(result);
            Start();
            Evaluate(3);
            Assert.That(playable.GetFloat(result), Is.EqualTo(5.5f).Within(0.0001f));
            playable.SetFloat(a, 4);
            playable.SetFloat(b, 6);
            Evaluate();
            Assert.That(playable.GetFloat(result), Is.EqualTo(9).Within(0.0001f));
            Assert.That(playable.GetFloat(buffer), Is.EqualTo(5.5f).Within(0.0001f));
            Evaluate();
            Assert.That(playable.GetFloat(buffer), Is.EqualTo(9).Within(0.0001f));
        }

        [Test]
        public void AddKeepsItsOutputWhenNormalizationLeavesOneInput() {
            var a = fx.CreateUniqueFloat("A", 2);
            var result = math.Add("Sum", (a, 2), (a, -1));
            Assert.That(result, Is.Not.SameAs(a));
            var buffer = math.Buffer(result);
            Start();
            Evaluate(3);
            playable.SetFloat(a, 5);
            Evaluate();
            Assert.That(playable.GetFloat(result), Is.EqualTo(5).Within(0.0001f));
            Assert.That(playable.GetFloat(buffer), Is.EqualTo(2).Within(0.0001f));
            Evaluate();
            Assert.That(playable.GetFloat(buffer), Is.EqualTo(5).Within(0.0001f));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void AddWritesZeroEvenWithANonzeroOutputDefault(bool empty) {
            var a = fx.CreateUniqueFloat("A", 3);
            var result = fx.CreateUniqueFloat("Cancelled", 7);
            var terms = empty ? Array.Empty<(BlendtreeMath.Value input, float multiplier)>()
                : new (BlendtreeMath.Value input, float multiplier)[] { (a, 1), (a, -1), (3, 1), (1, -3) };
            tree.Add(BlendtreeMath.Add("Cancelled", result, terms));
            Start();
            Evaluate(3);
            Assert.That(playable.GetFloat(result), Is.Zero);
        }

        [Test]
        public void InvertFoldsInputScalingWithoutChangingItsClampAndSavesAFrame() {
            var raw = fx.CreateUniqueFloat("Raw", 0.01f);
            var decoded = math.Multiply("Decoded", raw, 200);
            var buffered = math.Invert("Buffered inverse", decoded);
            var folded = math.Invert("Folded inverse", raw, 200);
            Assert.That(folded.GetDefault(), Is.EqualTo(buffered.GetDefault()));
            Start();
            Evaluate(5);
            Assert.That(playable.GetFloat(folded), Is.EqualTo(0.5f).Within(0.0001f));
            playable.SetFloat(raw, 0.02f);
            Evaluate(2);
            Assert.That(playable.GetFloat(folded), Is.EqualTo(0.25f).Within(0.0001f));
            Assert.That(playable.GetFloat(buffered), Is.EqualTo(0.5f).Within(0.0001f));
            Evaluate();
            Assert.That(playable.GetFloat(buffered), Is.EqualTo(0.25f).Within(0.0001f));
            foreach (var value in new[] { 0f, 0.0000001f, 0.0000005f, 0.000001f, 0.25f, 2f }) {
                playable.SetFloat(raw, value);
                Evaluate(5);
                var expected = 1 / Mathf.Max(value * 200, 0.0001f);
                Assert.That(playable.GetFloat(folded), Is.EqualTo(expected).Within(expected * 0.0001f));
                Assert.That(playable.GetFloat(buffered), Is.EqualTo(expected).Within(expected * 0.0001f));
            }
        }

        [TestCase(1f)]
        [TestCase(0.2f)]
        [TestCase(-2f)]
        [TestCase(0f)]
        public void CopyInPlaceWritesConstantsOverAnExistingTargetDefault(float value) {
            var source = fx.CreateUniqueFloat("Constant", value);
            var target = fx.CreateUniqueFloat("Injected", 77);
            math.CopyInPlace(source, target, 2);
            Assert.That(target.GetDefault(), Is.EqualTo(value * 2));
            Start();
            Evaluate();
            Assert.That(playable.GetFloat(target), Is.EqualTo(value * 2).Within(0.00001f));
        }

        [Test]
        public void UnitConversionsShareClipsWithoutNestingOrDelayingUpdates() {
            var raw = fx.CreateUniqueFloat("Raw", 3);
            var scale = fx.CreateUniqueFloat("Scale", 2);
            var otherScale = fx.CreateUniqueFloat("Other scale", 3);
            var source = math.Add("Source", (raw, 1), (1, -2));
            var first = fx.CreateUniqueFloat("First");
            var second = fx.CreateUniqueFloat("Second");
            var other = fx.CreateUniqueFloat("Other");
            var last = fx.CreateUniqueFloat("Last");
            math.MultiplyInPlace(first, scale, source);
            var treeCount = new AnimatorIterator.Trees().From(tree).Count;
            math.MultiplyInPlace(second, scale, source, 2);
            math.MultiplyInPlace(other, otherScale, source);
            math.MultiplyInPlace(last, scale, source, 3);
            Assert.That(new AnimatorIterator.Trees().From(tree).Count, Is.EqualTo(treeCount));
            var binding = VFBinding.MakeAnimatorBinding(first);
            Assert.That(new AnimatorIterator.Clips().From(tree).Where(c => c.GetCurve(binding, true) != null)
                .All(c => c.GetCurve(VFBinding.MakeAnimatorBinding(second), true) != null
                    && c.GetCurve(VFBinding.MakeAnimatorBinding(last), true) != null), Is.True);
            Start();
            Evaluate(3);
            Assert.That(playable.GetFloat(first), Is.EqualTo(2).Within(0.0001f));
            playable.SetFloat(raw, 1);
            Evaluate();
            Assert.That(playable.GetFloat(source), Is.EqualTo(-1).Within(0.0001f));
            Assert.That(playable.GetFloat(first), Is.EqualTo(-2).Within(0.0001f));
            Assert.That(playable.GetFloat(second), Is.EqualTo(-4).Within(0.0001f));
            Assert.That(playable.GetFloat(other), Is.EqualTo(-3).Within(0.0001f));
            Assert.That(playable.GetFloat(last), Is.EqualTo(-6).Within(0.0001f));
        }

        [Test]
        public void ObjectTreeDefaultsIncludeCopiedOutputsWithoutTouchingInputs() {
            var objectTrees = injector.GetService<ObjectTreeService>();
            var objectTree = objectTrees.Get(GameObjects.Create("Default outputs", root));
            var objectMath = new BlendtreeMath(fx, objectTree);
            var input = fx.CreateUniqueFloat("SDK input");
            var depth = objectMath.Add("Depth", (input, 1), (100, 1));
            var injected = fx.CreateUniqueFloat("Injected depth", 77);
            objectMath.CopyInPlace(depth, injected);
            Assert.That(injected.GetDefault(), Is.EqualTo(100));
            objectTrees.Apply();
            var defaults = new AnimatorIterator.Clips().From(fx).Single(c => c.name == "Default outputs - Disabled");
            Assert.That(defaults.GetCurve(VFBinding.MakeAnimatorBinding(depth), true).FloatCurve.Evaluate(0), Is.EqualTo(100));
            Assert.That(defaults.GetCurve(VFBinding.MakeAnimatorBinding(injected), true).FloatCurve.Evaluate(0), Is.EqualTo(100));
            Assert.That(defaults.GetCurve(VFBinding.MakeAnimatorBinding(input), true), Is.Null);
            depth.SetDefault(0);
            injected.SetDefault(0);
            objectTrees.Apply();
            Assert.That(defaults.GetCurve(VFBinding.MakeAnimatorBinding(depth), true).FloatCurve.Evaluate(0), Is.Zero);
            Assert.That(defaults.GetCurve(VFBinding.MakeAnimatorBinding(injected), true).FloatCurve.Evaluate(0), Is.Zero);
        }
    }
}
