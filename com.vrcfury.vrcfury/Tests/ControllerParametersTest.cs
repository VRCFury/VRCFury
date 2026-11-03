using System.Linq;
using NUnit.Framework;
using UnityEditor.Animations;
using UnityEngine;
using VF.Utils;
using VF.Utils.Controller;
using Object = UnityEngine.Object;

namespace VF.Tests {
    [Category("VRCFury")]
    public class ControllerParametersTest {
        private static VFAParam FindDefinition(VFController controller, string name) =>
            controller.parameters.FirstOrDefault(p => p.name == name);

        private static VFAParam RequestParameter(VFController controller, string name,
            AnimatorControllerParameterType type, FloatOrBool def = null) {
            switch (type) {
                case AnimatorControllerParameterType.Float: return controller.GetOrCreateFloat(name, def);
                case AnimatorControllerParameterType.Int: return controller.GetOrCreateInt(name, def);
                case AnimatorControllerParameterType.Bool: return controller.GetOrCreateBool(name, def);
                default: return new VFAParam(name, type).CopyTo(controller);
            }
        }

        [TestCase(false, 0)]
        [TestCase(true, 1)]
        public void BooleanValuesWorkForDefaultsConditionsAndAnimationSetters(bool value, float expected) {
            var controller = VFController.Create();
            var parameter = controller.GetOrCreateBool("Enabled", def: value);
            Assert.That(parameter.GetDefault(), Is.EqualTo(expected));
            Assert.That(controller.GetOrCreateBool("Enabled", !value).GetDefault(), Is.EqualTo(expected));
            Assert.That(FindDefinition(controller, "Enabled").type, Is.EqualTo(AnimatorControllerParameterType.Bool));
            Assert.That(parameter.MakeSetter(value).GetFloatCurves().Single().Item2.keys.Single().value, Is.EqualTo(expected));
            var layer = controller.NewLayer("Boolean comparison");
            layer.NewState("Match").TransitionsFromAny().When(parameter.IsEqualTo(value));
            Assert.That(layer.allTransitions.Single().conditions.Single().threshold, Is.EqualTo(expected));
            parameter.SetDefault(!value);
            Assert.That(parameter.GetDefault(), Is.EqualTo(1 - expected));
        }

        [TestCase(AnimatorControllerParameterType.Trigger, 0, AnimatorControllerParameterType.Trigger)]
        [TestCase(AnimatorControllerParameterType.Bool, 0, AnimatorControllerParameterType.Bool)]
        [TestCase(AnimatorControllerParameterType.Bool, 1, AnimatorControllerParameterType.Bool)]
        [TestCase(AnimatorControllerParameterType.Bool, -3, AnimatorControllerParameterType.Int)]
        [TestCase(AnimatorControllerParameterType.Bool, 0.75f, AnimatorControllerParameterType.Float)]
        [TestCase(AnimatorControllerParameterType.Int, 0, AnimatorControllerParameterType.Int)]
        [TestCase(AnimatorControllerParameterType.Int, 0.75f, AnimatorControllerParameterType.Float)]
        [TestCase(AnimatorControllerParameterType.Float, 0, AnimatorControllerParameterType.Float)]
        public void RegistrationHonorsTheRequestedTypeAndPreservesTheDefault(
            AnimatorControllerParameterType requested, float value, AnimatorControllerParameterType expected) {
            var controller = VFController.Create();
            var parameter = RequestParameter(controller, "Value", requested, value);
            Assert.That(FindDefinition(controller, parameter.name).type, Is.EqualTo(expected));
            controller.UpgradeWrongParamTypes();
            Assert.That(FindDefinition(controller, parameter.name).type, Is.EqualTo(expected));
            Assert.That(parameter.GetDefault(), Is.EqualTo(value));
        }

        [TestCase(AnimatorControllerParameterType.Trigger, AnimatorControllerParameterType.Trigger, AnimatorControllerParameterType.Trigger)]
        [TestCase(AnimatorControllerParameterType.Trigger, AnimatorControllerParameterType.Bool, AnimatorControllerParameterType.Bool)]
        [TestCase(AnimatorControllerParameterType.Trigger, AnimatorControllerParameterType.Int, AnimatorControllerParameterType.Int)]
        [TestCase(AnimatorControllerParameterType.Trigger, AnimatorControllerParameterType.Float, AnimatorControllerParameterType.Float)]
        [TestCase(AnimatorControllerParameterType.Bool, AnimatorControllerParameterType.Int, AnimatorControllerParameterType.Int)]
        [TestCase(AnimatorControllerParameterType.Bool, AnimatorControllerParameterType.Float, AnimatorControllerParameterType.Float)]
        [TestCase(AnimatorControllerParameterType.Int, AnimatorControllerParameterType.Bool, AnimatorControllerParameterType.Int)]
        [TestCase(AnimatorControllerParameterType.Int, AnimatorControllerParameterType.Float, AnimatorControllerParameterType.Float)]
        [TestCase(AnimatorControllerParameterType.Float, AnimatorControllerParameterType.Bool, AnimatorControllerParameterType.Float)]
        [TestCase(AnimatorControllerParameterType.Float, AnimatorControllerParameterType.Int, AnimatorControllerParameterType.Float)]
        public void ReusingAParameterOnlyUpgradesItsTypeAndPreservesItsEffectiveDefault(
            AnimatorControllerParameterType existing, AnimatorControllerParameterType requested,
            AnimatorControllerParameterType expected) {
            var controller = VFController.Create();
            ImportParameter(controller, "Shared", p => {
                p.type = existing;
                p.defaultBool = true;
                p.defaultInt = -3;
                p.defaultFloat = 0.75f;
            });
            controller.TryGetParam("Shared", out var original);
            var defaultValue = original.GetDefault();
            var reused = RequestParameter(controller, "Shared", requested, def: 100);
            Assert.That(reused.type, Is.EqualTo(expected));
            Assert.That(original.type, Is.EqualTo(expected));
            Assert.That(reused.GetDefault(), Is.EqualTo(defaultValue));
            Assert.That(controller.parameters.Length, Is.EqualTo(1));
        }

        [TestCase(int.MaxValue)]
        [TestCase(int.MinValue)]
        [TestCase(16777217)]
        public void ReusingAndRepairingAnIntegerPreservesDefaultsOutsideExactFloatPrecision(int defaultValue) {
            var controller = VFController.Create();
            var raw = ImportParameter(controller, "Integer", p => {
                p.type = AnimatorControllerParameterType.Int;
                p.defaultInt = defaultValue;
            });
            var parameter = controller.GetOrCreateInt("Integer");
            var layer = controller.NewLayer("Integer condition");
            layer.NewState("Match").TransitionsFromAny().When(parameter.IsEqualTo(1));
            controller.UpgradeWrongParamTypes();
            controller.RemoveWrongParamTypes();
            Assert.That(raw.type, Is.EqualTo(AnimatorControllerParameterType.Int));
            Assert.That(raw.Save().defaultInt, Is.EqualTo(defaultValue));
            var clone = controller.Clone();
            Assert.That(clone.GetOrCreateInt("Integer").Save().defaultInt, Is.EqualTo(defaultValue));
            Assert.That(parameter.CopyTo(VFController.Create()).Save().defaultInt, Is.EqualTo(defaultValue));
            Assert.That(parameter.WithName("Renamed").Save().defaultInt, Is.EqualTo(defaultValue));
            Assert.That(layer.allTransitions.Single().conditions.Single().parameter, Is.EqualTo("Integer"));
            Assert.That(layer.allTransitions.Single().conditions.Single().mode, Is.EqualTo(AnimatorConditionMode.Equals));
        }

        [TestCase(0)]
        [TestCase(1)]
        public void CopiedSliderStaysFloatWhenOnlyUsedAsACondition(float defaultValue) {
            var source = VFController.Create();
            var slider = source.GetOrCreateFloat("Slider", defaultValue);
            var destination = VFController.Create();
            var copy = slider.CopyTo(destination);
            destination.NewLayer("Exclusivity").NewState("Active").TransitionsFromAny().When(copy.IsGreaterThan(0));
            destination.UpgradeWrongParamTypes();
            Assert.That(FindDefinition(destination, "Slider").type, Is.EqualTo(AnimatorControllerParameterType.Float));
            Assert.That(copy.GetDefault(), Is.EqualTo(defaultValue));
            copy.SetDefault(0.25f);
            Assert.That(slider.GetDefault(), Is.EqualTo(defaultValue), "Copies must have independent storage");
        }

        [TestCase(AnimatorControllerParameterType.Bool, AnimatorControllerParameterType.Float)]
        [TestCase(AnimatorControllerParameterType.Float, AnimatorControllerParameterType.Bool)]
        public void CopyMergesTypesWithoutReplacingTheExistingDefault(
            AnimatorControllerParameterType sourceType, AnimatorControllerParameterType destinationType) {
            var source = RequestParameter(VFController.Create(), "Shared", sourceType, 1);
            var destination = VFController.Create();
            RequestParameter(destination, "Shared", destinationType);
            var copy = source.CopyTo(destination);
            Assert.That(FindDefinition(destination, "Shared").type, Is.EqualTo(AnimatorControllerParameterType.Float));
            Assert.That(copy.GetDefault(), Is.Zero);
            Assert.That(destination.parameters.Length, Is.EqualTo(1));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void MissingConditionsGetAValidFalseFallbackEvenWhenItsBaseNameExists(bool collision) {
            var controller = VFController.Create();
            if (collision) controller.GetOrCreateFloat("InvalidParam", 1);
            var layer = controller.NewLayer("Invalid transition");
            layer.NewState("Idle").TransitionsTo(layer.NewState("Target")).When(new VFCondition(new AnimatorCondition {
                parameter = "Undeclared", mode = AnimatorConditionMode.If
            }));
            controller.UpgradeWrongParamTypes();
            controller.RemoveWrongParamTypes();
            var condition = layer.allTransitions.Single().conditions.Single();
            var fallback = FindDefinition(controller, condition.parameter);
            Assert.That(fallback.type, Is.EqualTo(AnimatorControllerParameterType.Float));
            Assert.That(fallback.GetDefault(), Is.Zero);
            Assert.That(condition.mode, Is.EqualTo(AnimatorConditionMode.Greater));
            Assert.That(condition.threshold, Is.Zero);
            if (collision) Assert.That(FindDefinition(controller, "InvalidParam").GetDefault(), Is.EqualTo(1));
            var count = controller.parameters.Length;
            controller.RemoveWrongParamTypes();
            Assert.That(controller.parameters.Length, Is.EqualTo(count));
        }

        [Test]
        public void ThresholdRepairDoesNotMakeInvalidTreeWeightsUseItsTrueGuard() {
            var controller = VFController.Create();
            ImportParameter(controller, "Counter", p => p.type = AnimatorControllerParameterType.Int);
            var tree = VFBlendTreeDirect.Create("Invalid integer weight");
            tree.Add("Counter", VFClip.Create());
            var layer = controller.NewLayer("Invalid inputs");
            layer.NewState("Idle").WithAnimation(tree).TransitionsTo(layer.NewState("Target")).When(new VFCondition(new AnimatorCondition {
                parameter = "Counter", mode = AnimatorConditionMode.Greater, threshold = 1.5f
            }));
            controller.RemoveWrongParamTypes();
            var guard = layer.allTransitions.Single().conditions.Single(c => c.parameter != "Counter");
            Assert.That(guard.mode, Is.EqualTo(AnimatorConditionMode.Greater));
            Assert.That(FindDefinition(controller, guard.parameter).GetDefault(), Is.EqualTo(1));
            var weight = controller.GetTrees().Single().children.Single().directBlendParameter;
            Assert.That(FindDefinition(controller, weight).GetDefault(), Is.Zero);
            Assert.That(weight, Is.Not.EqualTo(guard.parameter));
        }

        [Test]
        public void TryGetDoesNotRegisterMissingParametersAndReturnsALiveHandleWhenPresent() {
            var controller = VFController.Create();
            Assert.That(controller.TryGetParam("Missing", out var missing), Is.False);
            Assert.That(missing, Is.Null);
            Assert.That(controller.parameters, Is.Empty);
            var created = controller.GetOrCreateBool("Present", 1);
            Assert.That(controller.TryGetParam("Present", out var found), Is.True);
            Assert.That(found, Is.SameAs(created));
            Assert.That(controller.GetOrCreateBool("Present"), Is.SameAs(created));
            Assert.That(controller.parameters.Single(), Is.SameAs(created));
            found.SetDefault(0.75f);
            Assert.That(created.GetDefault(), Is.EqualTo(0.75f));
            Assert.That(controller.parameters.Length, Is.EqualTo(1));
        }

        [Test]
        public void RenamingKeepsExistingHandlesUnchangedAndDefaultsIndependent() {
            var controller = VFController.Create();
            var first = controller.GetOrCreateBool("Shared", 1);
            var second = controller.GetOrCreateBool("Shared", 100);
            second.SetDefault(-0.75f);
            Assert.That(first.GetDefault(), Is.EqualTo(-0.75f));
            controller.RewriteParameters(name => "Renamed/" + name);
            Assert.That(first.name, Is.EqualTo("Shared"));
            Assert.That(second.name, Is.EqualTo(first.name));
            var renamed = controller.GetOrCreateFloat("Renamed/Shared");
            Assert.That(renamed, Is.Not.SameAs(first));
            Assert.That(renamed.GetDefault(), Is.EqualTo(-0.75f));
            first.SetDefault(0.5f);
            Assert.That(renamed.GetDefault(), Is.EqualTo(-0.75f));
            var clone = controller.Clone();
            clone.GetOrCreateFloat(renamed.name).SetDefault(0.25f);
            Assert.That(renamed.GetDefault(), Is.EqualTo(-0.75f));
            Assert.That(clone.GetOrCreateFloat(renamed.name).GetDefault(), Is.EqualTo(0.25f));
        }

        [Test]
        public void ParameterLookupTracksRenamesRemovalsAndFirstEntryOrder() {
            var controller = VFController.Create();
            var first = controller.GetOrCreateFloat("First", 0.25f);
            controller.GetOrCreateInt("Second", 3);
            controller.GetOrCreateBool("Third");
            controller.RewriteParameters(name => {
                if (name == "Second") {
                    Assert.That(controller.TryGetParam("First", out var original), Is.True);
                    Assert.That(original, Is.SameAs(first));
                    Assert.That(controller.TryGetParam("Renamed/First", out _), Is.False);
                }
                return "Renamed/" + name;
            });
            Assert.That(controller.TryGetParam("First", out _), Is.False);
            Assert.That(controller.TryGetParam("Renamed/First", out var renamed), Is.True);
            Assert.That(renamed, Is.Not.SameAs(first));
            controller.RemoveParameter("Renamed/Second");
            Assert.That(controller.TryGetParam("Renamed/Second", out _), Is.False);
            controller.GetOrCreateInt("Renamed/Second");
            Assert.That(controller.parameters.Select(p => p.name),
                Is.EqualTo(new[] { "Renamed/First", "Renamed/Third", "Renamed/Second" }));
            controller.RewriteParameters(_ => "Shared");
            var shared = controller.GetOrCreateFloat("Shared");
            Assert.That(shared, Is.Not.SameAs(renamed));
            Assert.That(controller.parameters.Single(), Is.SameAs(shared));
            controller.RemoveWrongParamTypes();
            Assert.That(controller.parameters.Single(), Is.SameAs(shared));
            Assert.That(shared.GetDefault(), Is.EqualTo(0.25f));
        }

        private static VFAParam ImportParameter(VFController controller, string name,
            System.Action<AnimatorControllerParameter> configure) {
            var parameter = new AnimatorControllerParameter { name = name };
            configure(parameter);
            var model = VFAParam.Load(parameter);
            return controller.GetOrCreateParam(name, model.type, () => model);
        }

        [TestCase(AnimatorControllerParameterType.Bool, 1)]
        [TestCase(AnimatorControllerParameterType.Int, -3)]
        [TestCase(AnimatorControllerParameterType.Float, -0.75f)]
        public void HandlesReadTheEffectiveDefaultWithoutChangingTheStoredType(
            AnimatorControllerParameterType type, float expected) {
            var controller = VFController.Create();
            var raw = ImportParameter(controller, "Shared", p => {
                p.type = type;
                // Deliberately inconsistent inactive fields: only the active type is authoritative.
                p.defaultBool = true;
                p.defaultInt = -3;
                p.defaultFloat = -0.75f;
            });
            Assert.That(controller.GetOrCreateBool("Shared", 100).GetDefault(), Is.EqualTo(expected));
            Assert.That(controller.GetOrCreateBool("Shared").GetDefault(), Is.EqualTo(expected));
            Assert.That(raw.type, Is.EqualTo(type));
            Assert.That(controller.parameters.Length, Is.EqualTo(1));
        }

        [TestCase(AnimatorControllerParameterType.Bool)]
        [TestCase(AnimatorControllerParameterType.Int)]
        [TestCase(AnimatorControllerParameterType.Float)]
        public void RepairPreservesEffectiveDefaultsWhenUsageRequiresFloat(AnimatorControllerParameterType type) {
            var controller = VFController.Create();
            var raw = ImportParameter(controller, "Shared", p => p.type = type);
            raw.SetDefault(type == AnimatorControllerParameterType.Bool ? 1 : -3);
            var expected = controller.GetOrCreateBool("Shared").GetDefault();
            var tree = VFBlendTreeDirect.Create("Requires float");
            tree.Add("Shared", VFClip.Create());
            controller.NewLayer("Test").NewState("Blend").WithAnimation(tree);
            controller.UpgradeWrongParamTypes();
            Assert.That(raw.type, Is.EqualTo(AnimatorControllerParameterType.Float));
            Assert.That(controller.GetOrCreateBool("Shared").GetDefault(), Is.EqualTo(expected));
        }

        [TestCase(AnimatorControllerParameterType.Bool)]
        [TestCase(AnimatorControllerParameterType.Int)]
        public void UpdatingNumericDefaultDoesNotLoseFractionalValuesDuringRepair(AnimatorControllerParameterType type) {
            var controller = VFController.Create();
            ImportParameter(controller, "Shared", p => p.type = type);
            var numeric = controller.GetOrCreateBool("Shared");
            numeric.SetDefault(-0.75f);
            controller.UpgradeWrongParamTypes();
            Assert.That(numeric.GetDefault(), Is.EqualTo(-0.75f));
            Assert.That(controller.GetOrCreateBool("Shared").GetDefault(), Is.EqualTo(-0.75f));
        }

        [Test]
        public void DuplicateParametersUseUnityFirstEntrySemantics(
            [Values(AnimatorControllerParameterType.Float, AnimatorControllerParameterType.Int,
                AnimatorControllerParameterType.Bool, AnimatorControllerParameterType.Trigger)] AnimatorControllerParameterType firstType,
            [Values(AnimatorControllerParameterType.Float, AnimatorControllerParameterType.Int,
                AnimatorControllerParameterType.Bool, AnimatorControllerParameterType.Trigger)] AnimatorControllerParameterType secondType
        ) {
            var source = new AnimatorController();
            source.AddLayer("Base");
            var stateMachine = source.layers[0].stateMachine;
            var state = stateMachine.AddState("Idle");
            var target = stateMachine.AddState("Target");
            var transition = state.AddTransition(target);
            transition.hasExitTime = false;
            transition.duration = 0;
            var mode = firstType == AnimatorControllerParameterType.Float || firstType == AnimatorControllerParameterType.Int
                ? AnimatorConditionMode.Greater : AnimatorConditionMode.If;
            transition.AddCondition(mode, firstType == AnimatorControllerParameterType.Float ? 0.4f : 3, "Duplicate");
            var obj = new GameObject("DuplicateParameters");
            try {
                source.parameters = new[] {
                    new AnimatorControllerParameter {
                        name = "Duplicate", type = firstType,
                        defaultFloat = 0.25f, defaultInt = 2, defaultBool = false
                    },
                    new AnimatorControllerParameter { name = "Other", type = AnimatorControllerParameterType.Float },
                    new AnimatorControllerParameter {
                        name = "Duplicate", type = secondType,
                        defaultFloat = 0.75f, defaultInt = 7, defaultBool = true
                    }
                };
                var animator = obj.AddComponent<Animator>();
                animator.runtimeAnimatorController = source;
                animator.Rebind();
                animator.Update(0);
                Assert.That(animator.parameterCount, Is.EqualTo(3), "Unity retains duplicate metadata");
                Assert.That(animator.GetCurrentAnimatorStateInfo(0).IsName("Idle"), Is.True);
                switch (firstType) {
                    case AnimatorControllerParameterType.Float:
                        Assert.That(animator.GetFloat("Duplicate"), Is.EqualTo(0.25f));
                        animator.SetFloat("Duplicate", 0.5f);
                        Assert.That(animator.GetFloat("Duplicate"), Is.EqualTo(0.5f));
                        break;
                    case AnimatorControllerParameterType.Int:
                        Assert.That(animator.GetInteger("Duplicate"), Is.EqualTo(2));
                        animator.SetInteger("Duplicate", 4);
                        Assert.That(animator.GetInteger("Duplicate"), Is.EqualTo(4));
                        break;
                    case AnimatorControllerParameterType.Bool:
                        Assert.That(animator.GetBool("Duplicate"), Is.False);
                        animator.SetBool("Duplicate", true);
                        Assert.That(animator.GetBool("Duplicate"), Is.True);
                        break;
                    case AnimatorControllerParameterType.Trigger:
                        Assert.That(animator.GetBool("Duplicate"), Is.False);
                        animator.SetTrigger("Duplicate");
                        Assert.That(animator.GetBool("Duplicate"), Is.True);
                        break;
                }
                animator.Update(0.1f);
                animator.Update(0.1f);
                Assert.That(animator.GetCurrentAnimatorStateInfo(0).IsName("Target"), Is.True,
                    "Transition evaluation must use the first entry too");

                var controller = VFController.Load(source, new VFLoadContext { OwnerObject = obj, AnimatorObject = obj });
                Assert.That(controller.parameters.Select(p => p.name), Is.EqualTo(new[] { "Duplicate", "Other" }));
                var parameter = FindDefinition(controller, "Duplicate");
                Assert.That(parameter.type, Is.EqualTo(firstType));
                var saved = parameter.Save();
                if (firstType == AnimatorControllerParameterType.Float) Assert.That(saved.defaultFloat, Is.EqualTo(0.25f));
                if (firstType == AnimatorControllerParameterType.Int) Assert.That(saved.defaultInt, Is.EqualTo(2));
                if (firstType == AnimatorControllerParameterType.Bool) Assert.That(saved.defaultBool, Is.False);
                var condition = controller.layers.SelectMany(layer => layer.allTransitions).Single().conditions.Single();
                Assert.That(condition.parameter, Is.EqualTo("Duplicate"));
                Assert.That(condition.mode, Is.EqualTo(mode));
                controller.UpgradeWrongParamTypes();
                Assert.That(FindDefinition(controller, "Duplicate").type, Is.EqualTo(firstType),
                    "A later duplicate must not upgrade the winning parameter's type");
                Assert.That(source.parameters, Has.Length.EqualTo(3), "Do not modify the source controller");
            } finally {
                Object.DestroyImmediate(obj);
                Object.DestroyImmediate(transition);
                Object.DestroyImmediate(target);
                Object.DestroyImmediate(state);
                Object.DestroyImmediate(stateMachine);
                Object.DestroyImmediate(source);
            }
        }
    }
}
