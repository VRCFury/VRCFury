namespace VF.Tests {
    using System.Linq;
    using NUnit.Framework;
    using UnityEditor.Animations;
    using UnityEngine;
    using VF.Utils;
    using VF.Utils.Controller;
    using Object = UnityEngine.Object;

    internal class VFAParamTest : ControllerParameterTestBase {
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
            var sourceController = VFController.Create();
            var source = RequestParameter(sourceController, "Shared", sourceType, 1);
            var destination = VFController.Create();
            RequestParameter(destination, "Shared", destinationType);
            var copy = source.CopyTo(destination);
            Assert.That(FindDefinition(destination, "Shared").type, Is.EqualTo(AnimatorControllerParameterType.Float));
            Assert.That(copy.GetDefault(), Is.Zero);
            Assert.That(destination.parameters.Length, Is.EqualTo(1));

            var merged = VFController.Create();
            var existing = RequestParameter(merged, "Shared", destinationType);
            merged.TakeOwnershipOf(sourceController);
            Assert.That(merged.parameters.Single(), Is.SameAs(existing));
            Assert.That(existing.type, Is.EqualTo(AnimatorControllerParameterType.Float));
            Assert.That(existing.GetDefault(), Is.Zero);
            Assert.That(sourceController.parameters, Is.Empty);
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
            Assert.That(controller.GetOrCreateBool("Shared", false).GetDefault(), Is.EqualTo(expected));
            Assert.That(controller.GetOrCreateBool("Shared").GetDefault(), Is.EqualTo(expected));
            Assert.That(raw.type, Is.EqualTo(type));
            Assert.That(controller.parameters.Length, Is.EqualTo(1));
        }
    }
}
