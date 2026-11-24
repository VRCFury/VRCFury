namespace VF.Tests {
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;
    using NUnit.Framework;
    using UnityEngine;
    using VF.Builder;
    using VF.Service;
    using VF.Service.Compressor;
    using VF.Utils;
    using VRC.SDK3.Avatars.Components;
    using VRC.SDK3.Avatars.ScriptableObjects;
    using Object = UnityEngine.Object;
    using VF.Utils.Controller;

    internal class VFControllerTest : ExpressionParameterTestBase {
        [TestCase(false)]
        [TestCase(true)]
        public void RepairPreservesBooleanRegistrationsAndImportedFloatStorage(bool imported) {
            var controller = VFController.Create();
            if (imported) new VFAParam("Value", AnimatorControllerParameterType.Float).CopyTo(controller);
            var parameter = controller.GetOrCreateBool("Value");
            controller.NewLayer("Driver").NewState("Write").Drives(parameter, false);
            var clone = controller.Clone();
            clone.RewriteParameters(name => "Renamed/" + name);
            var merged = VFController.Create();
            merged.TakeOwnershipOf(clone);
            merged.UpgradeWrongParamTypes();
            Assert.That(FindDefinition(merged, "Renamed/Value").type,
                Is.EqualTo(imported ? AnimatorControllerParameterType.Float : AnimatorControllerParameterType.Bool));
        }

        [TestCase(false, 3, AnimatorControllerParameterType.Int)]
        [TestCase(false, 0.25f, AnimatorControllerParameterType.Float)]
        [TestCase(true, 1, AnimatorControllerParameterType.Int)]
        [TestCase(true, 0.25f, AnimatorControllerParameterType.Float)]
        public void ExplicitNumericStorageSurvivesBooleanDriverWrites(
            bool delta, float value, AnimatorControllerParameterType expected) {
            var controller = VFController.Create();
            var parameter = expected == AnimatorControllerParameterType.Float
                ? controller.GetOrCreateFloat("Value") : controller.GetOrCreateInt("Value");
            var state = controller.NewLayer("Drivers").NewState("Write");
            state.Drives(parameter, false);
            if (delta) state.DrivesDelta(parameter, value);
            else state.Drives(parameter, value);
            controller.UpgradeWrongParamTypes();
            Assert.That(FindDefinition(controller, parameter.name).type, Is.EqualTo(expected));
        }

        [Test]
        public void ControllerHelpersDeclareTypesBeforeTheFinalRepairPass() {
            var controller = new VFAvatarController(
                VFController.Create(),
                VRCAvatarDescriptor.AnimLayerType.FX,
                () => 0,
                null,
                new LayerSourceService()
            );
            Assert.That(controller.One().type, Is.EqualTo(AnimatorControllerParameterType.Float));
            Assert.That(controller.Zero().type, Is.EqualTo(AnimatorControllerParameterType.Float));
            Assert.That(controller.GestureLeft().type, Is.EqualTo(AnimatorControllerParameterType.Int));
            Assert.That(controller.GestureRight().type, Is.EqualTo(AnimatorControllerParameterType.Int));
            Assert.That(controller.Viseme().type, Is.EqualTo(AnimatorControllerParameterType.Int));
            Assert.That(controller.GestureLeftWeight().type, Is.EqualTo(AnimatorControllerParameterType.Float));
            Assert.That(controller.GestureRightWeight().type, Is.EqualTo(AnimatorControllerParameterType.Float));
            Assert.That(controller.IsLocal().type, Is.EqualTo(AnimatorControllerParameterType.Bool));
        }

        [Test]
        public void ExplicitIntegerStorageFollowsCloningRenamingAndControllerMerging() {
            var source = VFController.Create();
            var parameter = source.GetOrCreateInt("Random");
            source.NewLayer("Drivers").NewState("Random").DrivesRandom(parameter, 0, 10);
            var clone = source.Clone();
            clone.RewriteParameters(name => "Renamed/" + name);
            var merged = VFController.Create();
            merged.TakeOwnershipOf(clone);
            merged.UpgradeWrongParamTypes();
            Assert.That(FindDefinition(merged, "Renamed/Random").type, Is.EqualTo(AnimatorControllerParameterType.Int));
            Assert.That(FindDefinition(source, "Random").type, Is.EqualTo(AnimatorControllerParameterType.Int));
            var driver = merged.layers.Single().allStates.Single().behaviours
                .FindBehaviour<VRCAvatarParameterDriver>().Read<VRCAvatarParameterDriver>();
            Assert.That(driver.parameters.Single().name, Is.EqualTo("Renamed/Random"));
        }

        [TestCase(VRCExpressionParameters.ValueType.Bool, AnimatorControllerParameterType.Bool)]
        [TestCase(VRCExpressionParameters.ValueType.Int, AnimatorControllerParameterType.Int)]
        [TestCase(VRCExpressionParameters.ValueType.Float, AnimatorControllerParameterType.Float)]
        public void CopyConversionPreservesExplicitDestinationStorage(
            VRCExpressionParameters.ValueType destinationType, AnimatorControllerParameterType expected) {
            var controller = VFController.Create();
            var source = controller.GetOrCreateBool("Source", 0.75f);
            var destination = expected == AnimatorControllerParameterType.Float ? controller.GetOrCreateFloat("Destination")
                : expected == AnimatorControllerParameterType.Int ? controller.GetOrCreateInt("Destination")
                : controller.GetOrCreateBool("Destination");
            controller.NewLayer("Driver").NewState("Copy").DrivesCopy(source,
                new VRCExpressionParameters.Parameter { name = destination.name, valueType = destinationType });
            controller.UpgradeWrongParamTypes();
            Assert.That(FindDefinition(controller, destination.name).type, Is.EqualTo(expected));
            Assert.That(controller.GetOrCreateBool(source.name).GetDefault(), Is.EqualTo(0.75f));
        }

        [Test]
        public void IntegerCopyUsesTheDeclaredDestinationStorage() {
            var controller = VFController.Create();
            var source = controller.GetOrCreateBool("Source", 3.75f);
            var destination = controller.GetOrCreateInt("Integer");
            controller.NewLayer("Driver").NewState("Copy").DrivesCopy(source, destination);
            Assert.That(FindDefinition(controller, destination.name).type, Is.EqualTo(AnimatorControllerParameterType.Int));
            controller.UpgradeWrongParamTypes();
            Assert.That(FindDefinition(controller, destination.name).type, Is.EqualTo(AnimatorControllerParameterType.Int));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void ExplicitIntegerStorageSurvivesUntilFloatUsageRequiresPromotion(bool blendTree) {
            var controller = VFController.Create();
            var integer = controller.GetOrCreateInt("IntegerRandom");
            var continuous = controller.GetOrCreateFloat("ContinuousRandom");
            var state = controller.NewLayer("Drivers").NewState("Random");
            // Identical bounds do not tell us whether the random result should be integral.
            state.DrivesRandom(integer, 0, 1);
            state.DrivesRandom(continuous, 0, 1);
            if (blendTree) {
                var tree = VFBlendTreeDirect.Create("Requires float");
                tree.Add(integer, VFClip.Create());
                state.WithAnimation(tree);
            }
            controller.UpgradeWrongParamTypes();
            controller.RemoveWrongParamTypes(null);
            Assert.That(FindDefinition(controller, integer.name).type,
                Is.EqualTo(blendTree ? AnimatorControllerParameterType.Float : AnimatorControllerParameterType.Int));
            Assert.That(FindDefinition(controller, continuous.name).type, Is.EqualTo(AnimatorControllerParameterType.Float));
        }

        [Test]
        public void AnimatorGetOrCreatePreservesExistingDefaultsAndOnlyInitializesOnce() {
            var controller = VFController.Create();
            var enabled = controller.GetOrCreateBool("Enabled", def: 1);
            var mode = controller.GetOrCreateBool("Mode", 3);
            var weight = controller.GetOrCreateBool("Weight", 0.75f);
            Assert.That(enabled.name, Is.EqualTo("Enabled"));
            Assert.That(mode.GetDefault(), Is.EqualTo(3));
            Assert.That(weight.GetDefault(), Is.EqualTo(0.75f));
            Assert.That(controller.GetOrCreateBool("Enabled").GetDefault(), Is.EqualTo(1));
            Assert.That(controller.GetOrCreateBool("Mode").GetDefault(), Is.EqualTo(3));
            Assert.That(controller.GetOrCreateBool("Weight").GetDefault(), Is.EqualTo(0.75f));
            controller.GetOrCreateBool("Numeric", 5);
            var initialized = FindDefinition(controller, "Numeric");
            Assert.That(controller.GetOrCreateBool("Numeric", 100).GetDefault(), Is.EqualTo(5));
            Assert.That(FindDefinition(controller, "Numeric"), Is.SameAs(initialized));
            Assert.That(controller.parameters.Length, Is.EqualTo(4));
        }
    }
}
