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
            var reused = math.Invert("Reused inverse", decoded, reuseWriters: true);
            Assert.That(fx.parameters.Single(p => p.name.EndsWith("Reused inverse/Tmp")).GetDefault(),
                Is.EqualTo(decoded.GetDefault() * 10000 - 1));
            Assert.That(folded.GetDefault(), Is.EqualTo(buffered.GetDefault()));
            Start();
            Evaluate(5);
            Assert.That(playable.GetFloat(folded), Is.EqualTo(0.5f).Within(0.0001f));
            playable.SetFloat(raw, 0.02f);
            Evaluate(2);
            Assert.That(playable.GetFloat(folded), Is.EqualTo(0.25f).Within(0.0001f));
            Assert.That(playable.GetFloat(reused), Is.EqualTo(0.25f).Within(0.0001f));
            Assert.That(playable.GetFloat(buffered), Is.EqualTo(0.5f).Within(0.0001f));
            Evaluate();
            Assert.That(playable.GetFloat(buffered), Is.EqualTo(0.25f).Within(0.0001f));
            foreach (var value in new[] { 0f, 0.0000001f, 0.0000005f, 0.000001f, 0.25f, 2f }) {
                playable.SetFloat(raw, value);
                Evaluate(5);
                var expected = 1 / Mathf.Max(value * 200, 0.0001f);
                Assert.That(playable.GetFloat(folded), Is.EqualTo(expected).Within(expected * 0.0001f));
                Assert.That(playable.GetFloat(buffered), Is.EqualTo(expected).Within(expected * 0.0001f));
                Assert.That(playable.GetFloat(reused), Is.EqualTo(expected).Within(expected * 0.0001f));
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
