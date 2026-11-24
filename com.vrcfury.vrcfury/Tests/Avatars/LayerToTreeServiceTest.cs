#if VRCSDK_HAS_CONTACT_PARAMETER_FLAGS
namespace VF.Tests {
    using static VF.Inspector.VRCFuryHapticPlugEditor;
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
    using VF.Model.Feature;
    using VF.Service;
    using VF.Utils;
    using VF.Utils.Controller;
    using VRC.SDK3.Avatars.Components;
    using VRC.SDK3.Dynamics.Contact.Components;
    using VRC.Dynamics;
    using VRC.SDKBase;
    using Object = UnityEngine.Object;

    internal class LayerToTreeServiceTest : SpsAvatarBuildTestBase {
        [Test]
        public void IndependentStaticSpsActionFoldsEmptyRampEnd(
            [Values] bool increasing,
            [Values] bool toggleOwner,
            [Values] bool drivesParameter
        ) {
            optimizeTrees = true;
            var owner = toggleOwner ? GameObjects.Create("Socket frame", root) : root.asVf();
            injector.Set("componentObject", new Func<VFGameObject>(() => owner));
            VFAParam enable = null;
            if (toggleOwner) {
                enable = fx.CreateUniqueFloat("Enable socket", 1);
                enable.MakeExpressionFloat(synced: false);
                var off = VFClip.Create("Disable socket");
                var on = VFClip.Create("Enable socket");
                off.SetEnabled(owner, false);
                on.SetEnabled(owner, true);
                fx.NewLayer("Socket enabled").NewState("Enabled").WithAnimation(
                    VFBlendTree1D.CreateWithData("Socket enabled", enable, (0, off), (1, on)));
            }

            var sign = increasing ? 1 : -1;
            var input = fx.CreateUniqueFloat("Measurement", -sign * 0.1f);
            var output = fx.GetOrCreateFloat("Independent output");
            var light = GameObjects.Create("Test light", root).AddComponent<Light>();
            light.intensity = 0;
            var action = new VRCFuryHapticSocket.DepthActionNew {
                range = increasing ? new Vector2(0, 0.25f) : new Vector2(-0.25f, 0),
                smoothingSeconds = 0
            };
            var clip = VFClip.Create("Static action");
            if (drivesParameter) clip.SetAap(output, 1);
            else clip.SetCurve(light, "m_Intensity", 1);
            action.actionSet.actions.Add(new VF.Model.StateAction.AnimationClipAction { vfClip = clip });
            injector.GetService<SpsDepthAnimationService>().CreateAnims(
                new[] { action },
                owner,
                "Independent",
                _ => _ => input,
                owner,
                measurement: increasing ? SpsParamPlugEntryRadius : SpsParamDepth
            );
            Start();
            Assert.That(fx.GetLayers().Any(layer => layer.name.StartsWith("Independent - ")), Is.False,
                "Independent actions should remain eligible for the layer optimizer");
            Assert.That(new AnimatorIterator.Trees().From(fx).Count(tree => tree.BlendParameter == input.name),
                Is.EqualTo(drivesParameter ? 2 : 1));
            float ReadOutput() => drivesParameter ? playable.GetFloat(output) : light.intensity;
            playable.SetFloat(input, sign * 0.125f);
            Evaluate(12);
            Assert.That(ReadOutput(), Is.EqualTo(0.5f).Within(0.001f));
            if (enable != null) {
                playable.SetFloat(enable, 0);
                Evaluate(12);
                Assert.That(ReadOutput(), Is.Zero.Within(0.001f));
                playable.SetFloat(enable, 1);
            }
            playable.SetFloat(input, 0);
            Evaluate(12);
            Assert.That(ReadOutput(), Is.Zero.Within(0.001f));
            playable.SetFloat(input, -sign * 0.1f);
            Evaluate(12);
            Assert.That(ReadOutput(), Is.Zero.Within(0.001f));
        }

        [Test]
        public void LayerOptimizationRequiresAValidSampledBinding(
            [Values(false, true)] bool validOff,
            [Values(false, true)] bool validOn,
            [Values(false, true)] bool compound) {
            var input = fx.CreateUniqueFloat("Input");
            var flag = fx.CreateUniqueFloat("Flag");
            var output = fx.GetOrCreateFloat("Valid output");
            VFClip Clip(bool valid) {
                var clip = VFClip.Create("Binding validation");
                if (valid) clip.SetAap(output, 1);
                else clip.SetCurve(root, typeof(SkinnedMeshRenderer), "blendShape.Missing", 1);
                return clip;
            }
            var layer = fx.NewLayer("Binding validation");
            var off = layer.NewState("Off"); off.WithAnimation(Clip(validOff));
            var on = layer.NewState("On"); on.WithAnimation(Clip(validOn));
            // Valid bindings elsewhere in the layer do not make the sampled states useful.
            layer.NewState("Unreachable").WithAnimation(output.MakeSetter(1));
            var condition = input.IsGreaterThan(0);
            if (compound) condition = condition.And(flag.IsGreaterThan(0));
            off.TransitionsTo(on).When(condition);
            on.TransitionsTo(off).When(condition.Not());
            injector.GetService<LayerToTreeService>().Apply();
            Assert.That(layer.Exists(), Is.EqualTo(!validOff && !validOn));
        }

        [Test]
        public void CompoundConditionsCanOptimizeBuiltInIntsAboveOne([Values(false, true)] bool optimize) {
            optimizeTrees = optimize;
            var gesture = fx.GetOrCreateInt("GestureLeft");
            var enabled = fx.CreateUniqueBool("Enabled", true);
            var output = fx.GetOrCreateFloat("Compound output");
            var layer = fx.NewLayer("Compound int conditions");
            var off = layer.NewState("Off");
            var on = layer.NewState("On");
            var clip = VFClip.Create("Compound on");
            clip.SetAap(output, 7);
            on.WithAnimation(clip);
            var condition = gesture.IsNotEqualTo(1).And(enabled.IsTrue());
            off.TransitionsTo(on).When(condition);
            on.TransitionsTo(off).When(condition.Not());
            Start();
            Assert.That(layer.Exists(), Is.EqualTo(!optimize));
            foreach (var value in new[] { 0, 1, 2, 7 }) {
                if (gesture.type == AnimatorControllerParameterType.Int) playable.SetInteger(gesture, value);
                else playable.SetFloat(gesture, value);
                Evaluate(12);
                Assert.That(playable.GetFloat(output), Is.EqualTo(value != 1 ? 7 : 0).Within(0.001f));
            }
            if (enabled.type == AnimatorControllerParameterType.Bool) playable.SetBool(enabled, false);
            else playable.SetFloat(enabled, 0);
            Evaluate(12);
            Assert.That(playable.GetFloat(output), Is.Zero.Within(0.001f));
        }

        [Test]
        public void NumericBooleanConditionsAcceptNegativeValues(
            [Values(false, true)] bool optimize,
            [Values(false, true)] bool invert,
            [Values(false, true)] bool integer) {
            var input = integer ? fx.CreateUniqueInt("Numeric input") : fx.CreateUniqueFloat("Numeric input");
            var gate = fx.CreateUniqueFloat("Gate", 1);
            var output = fx.GetOrCreateFloat("Numeric boolean output");
            var layer = fx.NewLayer("Numeric boolean conditions");
            var off = layer.NewState("Off"); off.WithAnimation(output.MakeSetter(0));
            var on = layer.NewState("On"); on.WithAnimation(output.MakeSetter(1));
            var condition = (invert ? input.IsFalse() : input.IsTrue()).And(gate.IsGreaterThan(0));
            off.TransitionsTo(on).When(condition);
            on.TransitionsTo(off).When(condition.Not());
            // Match production: optimize before the final parameter-type repair.
            if (optimize) injector.GetService<LayerToTreeService>().Apply();
            Start();
            Assert.That(layer.Exists(), Is.EqualTo(!optimize));
            foreach (var value in integer ? new[] { -2f, -1, 0, 1, 2 } : new[] { -1f, -0.5f, 0, 0.5f, 1 }) {
                if (input.type == AnimatorControllerParameterType.Int) playable.SetInteger(input, (int)value);
                else playable.SetFloat(input, value);
                Evaluate(12);
                Assert.That(playable.GetFloat(output), Is.EqualTo((value != 0) != invert ? 1 : 0).Within(0.001f));
            }
        }

        [TestCase(6, true)]
        [TestCase(7, false)]
        public void ConditionNegationIsLimitedBeforeCartesianExpansion(int conditionCount, bool shouldOptimize) {
            var inputs = Enumerable.Range(0, conditionCount).Select(i => fx.CreateUniqueFloat("Gate " + i, 1)).ToArray();
            var positive = VFCondition.Any(inputs.Select(input => input.IsGreaterThan(0)));
            var negative = VFCondition.All(inputs.Select(input => input.IsGreaterThan(0).Not()));
            if (!shouldOptimize) {
                // A later tree-size rejection must not mask an unbounded negation.
                UnityEngine.TestTools.LogAssert.Expect(LogType.Log,
                    new System.Text.RegularExpressions.Regex("Contains more than six conditions before negation"));
            }
            CheckConditionGateLimit(positive, inputs, shouldOptimize, false, negative);
        }

        [Test]
        public void CompoundConditionOptimizationHasSixGateLimit(
            [Values(5, 6, 7)] int comparisons,
            [Values(false, true)] bool disjunction,
            [Values(false, true)] bool largePayload) {
            var inputs = Enumerable.Range(0, comparisons).Select(i => fx.CreateUniqueFloat("Gate " + i, 1)).ToArray();
            var conditions = inputs.Select(input => input.IsGreaterThan(0)).ToArray();
            var condition = disjunction ? VFCondition.Any(conditions) : VFCondition.All(conditions);
            CheckConditionGateLimit(condition, inputs, comparisons <= 6, largePayload);
        }

        [TestCase(2, true)]
        [TestCase(3, true)]
        [TestCase(4, false)]
        public void CompoundConditionLimitCountsSharedTreesOnce(int pairs, bool shouldOptimize) {
            var inputs = Enumerable.Range(0, pairs * 2).Select(i => fx.CreateUniqueFloat("Gate " + i, 1)).ToArray();
            var condition = VFCondition.All(Enumerable.Range(0, pairs).Select(i =>
                inputs[i * 2].IsGreaterThan(0).Or(inputs[i * 2 + 1].IsGreaterThan(0))));
            // Construct the opposite directly instead of expanding the already-expanded positive DNF.
            var opposite = VFCondition.Any(Enumerable.Range(0, pairs).Select(i =>
                inputs[i * 2].IsGreaterThan(0).Not().And(inputs[i * 2 + 1].IsGreaterThan(0).Not())));
            CheckConditionGateLimit(condition, inputs, shouldOptimize, false, opposite);
        }

        [TestCase(true, true, true, true)]
        [TestCase(true, false, true, false)]
        [TestCase(false, false, true, true)]
        [TestCase(false, false, false, false)]
        public void ExactOppositesAreCheckedBeforeSimpleConditionExceptions(
            bool builtIn, bool exact, bool sameParameter, bool shouldOptimize) {
            optimizeTrees = true;
            var input = builtIn ? fx.GetOrCreateInt("GestureLeft") : fx.CreateUniqueInt("Input");
            var other = sameParameter ? input : fx.CreateUniqueInt("Other input");
            var output = fx.GetOrCreateFloat("Simple output");
            var layer = fx.NewLayer("Simple conditions");
            var off = layer.NewState("Off");
            off.WithAnimation(output.MakeSetter(0));
            var on = layer.NewState("On");
            on.WithAnimation(output.MakeSetter(7));
            on.TransitionsTo(off).When(input.IsEqualTo(1));
            off.TransitionsTo(on).When(other.IsNotEqualTo(exact ? 1 : 2));
            Start();
            Assert.That(layer.Exists(), Is.EqualTo(!shouldOptimize));
            if (!shouldOptimize) return;
            foreach (var value in new[] { 0, 1, 2, 7 }) {
                playable.SetFloat(input, value);
                Evaluate(12);
                Assert.That(playable.GetFloat(output), Is.EqualTo(value == 1 ? 0 : 7).Within(0.001f));
            }
        }

        [TestCase(4)]
        [TestCase(5)]
        public void TreeGrowthLimitSubtractsEachInputPayloadOnce(int depth) {
            optimizeTrees = true;
            var gate = fx.CreateUniqueFloat("Gate", 1);
            var flag = fx.CreateUniqueFloat("Flag", 1);
            var mix = fx.CreateUniqueFloat("Mix", 0.5f);
            var output = fx.GetOrCreateFloat("Shared payload output");
            var clip = output.MakeSetter(1);
            VFMotion payload = clip;
            for (var i = 0; i < depth; i++) {
                payload = VFBlendTree1D.CreateWithData("Shared payload " + i, mix, (0, clip), (1, payload));
            }
            var layer = fx.NewLayer("Shared payload");
            var off = layer.NewState("Off"); off.WithAnimation(payload);
            var on = layer.NewState("On"); on.WithAnimation(payload);
            var condition = gate.IsGreaterThan(0).And(flag.IsGreaterThan(0));
            off.TransitionsTo(on).When(condition);
            on.TransitionsTo(off).When(condition.Not());
            Start();
            Assert.That(layer.Exists(), Is.False, "The original payload of each input state must be subtracted once");
            Evaluate(12);
            Assert.That(playable.GetFloat(output), Is.EqualTo(1).Within(0.001f));
        }

        private void CheckConditionGateLimit(VFCondition condition, VFAParam[] inputs, bool shouldOptimize, bool largePayload,
            VFCondition opposite = null) {
            optimizeTrees = true;
            foreach (var input in inputs) input.MakeExpressionFloat(synced: false);
            var output = fx.GetOrCreateFloat("Budget output");
            var baseline = VFClip.Create("Budget baseline");
            baseline.SetAap(output, 0);
            fx.NewLayer("Budget baseline").NewState("Baseline").WithAnimation(baseline);
            var clip = VFClip.Create("Budget on");
            clip.SetAap(output, 1);
            VFMotion payload = clip;
            if (largePayload) {
                var mix = fx.CreateUniqueFloat("Payload mix", 0.5f);
                for (var i = 0; i < 8; i++) {
                    payload = VFBlendTree1D.CreateWithData("Existing payload " + i, mix, (0, clip), (1, payload));
                }
            }
            var layer = fx.NewLayer("Budget action");
            var off = layer.NewState("Off");
            var on = layer.NewState("On");
            on.WithAnimation(payload);
            off.TransitionsTo(on).When(condition);
            on.TransitionsTo(off).When(opposite ?? condition.Not());
            Start();
            Assert.That(layer.Exists(), Is.EqualTo(!shouldOptimize), "The gate budget must exclude the existing animation payload");
            Evaluate(12);
            Assert.That(playable.GetFloat(output), Is.EqualTo(1).Within(0.001f));
            foreach (var input in inputs) playable.SetFloat(input, 0);
            Evaluate(12);
            Assert.That(playable.GetFloat(output), Is.Zero.Within(0.001f));
        }
    }
}
#endif
