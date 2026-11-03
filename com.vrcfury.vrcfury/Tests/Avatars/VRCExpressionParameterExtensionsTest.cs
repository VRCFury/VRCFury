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

namespace VF.Tests {
    [Category("VRCFury")]
    public class VRCExpressionParameterExtensionsTest {
        private static VFAParam FindDefinition(VFController controller, string name) =>
            controller.parameters.FirstOrDefault(p => p.name == name);

        private static VFAvatarController CreateController(ExpressionParamsService expressions) =>
            new VFAvatarController(VFController.Create(), VRCAvatarDescriptor.AnimLayerType.FX,
                () => 0, () => "", name => name, new LayerSourceService(), expressions);

        private static ExpressionParamsService GetExpressionParams(GameObject root) =>
            VRCFuryInjectorBuilder.GetInjector(root.AddComponent<VRCAvatarDescriptor>()).GetService<ExpressionParamsService>();

        [TestCase(VRCExpressionParameters.ValueType.Bool)]
        [TestCase(VRCExpressionParameters.ValueType.Int)]
        [TestCase(VRCExpressionParameters.ValueType.Float)]
        public void FluentExpressionDeclarationsDefaultToUnsavedAndConfigureTheSameInstance(VRCExpressionParameters.ValueType type) {
            var parameter = ExpressionParam.Create("Example");
            Assert.That(parameter.name, Is.EqualTo("Example"));
            Assert.That(parameter.saved, Is.False);
            Assert.That(parameter.IsNetworkSynced(), Is.True);
            Assert.That(parameter.defaultValue, Is.Zero);
            var typed = type == VRCExpressionParameters.ValueType.Bool ? parameter.Bool()
                : type == VRCExpressionParameters.ValueType.Int ? parameter.Int() : parameter.Float();
            Assert.That(typed, Is.SameAs(parameter));
            Assert.That(parameter.valueType, Is.EqualTo(type));
            Assert.That(parameter.Default(true).Saved().Unsynced(), Is.SameAs(parameter));
            Assert.That(parameter.defaultValue, Is.EqualTo(1));
            Assert.That(parameter.saved, Is.True);
            Assert.That(parameter.IsNetworkSynced(), Is.False);
            Assert.That(parameter.Saved(false).Synced(), Is.SameAs(parameter));
            Assert.That(parameter.saved, Is.False);
            Assert.That(parameter.IsNetworkSynced(), Is.True);
            Assert.That(parameter.defaultValue, Is.EqualTo(1));
            Assert.That(parameter.valueType, Is.EqualTo(type));
        }

        [TestCase(VRCExpressionParameters.ValueType.Bool, false)]
        [TestCase(VRCExpressionParameters.ValueType.Bool, true)]
        [TestCase(VRCExpressionParameters.ValueType.Int, false)]
        [TestCase(VRCExpressionParameters.ValueType.Int, true)]
        [TestCase(VRCExpressionParameters.ValueType.Float, false)]
        [TestCase(VRCExpressionParameters.ValueType.Float, true)]
        public void GlobalToggleOverridesControllerDefaultBeforeCreatingAnExpression(
            VRCExpressionParameters.ValueType type, bool expressionAlreadyExists) {
            var root = new GameObject("Global toggle defaults");
            try {
                var avatar = root.AddComponent<VRCAvatarDescriptor>();
                avatar.baseAnimationLayers = new[] {
                    new VRCAvatarDescriptor.CustomAnimLayer { type = VRCAvatarDescriptor.AnimLayerType.FX }
                };
                avatar.specialAnimationLayers = new[] {
                    new VRCAvatarDescriptor.CustomAnimLayer { type = VRCAvatarDescriptor.AnimLayerType.Action }
                };
                var injector = VRCFuryInjectorBuilder.GetInjector(avatar);
                injector.Set("componentObject", new Func<VFGameObject>(() => root));
                var globals = injector.GetService<GlobalsService>();
                globals.allFeaturesInRun = new List<VF.Model.Feature.FeatureModel>();
                globals.allBuildersInRun = new List<VF.Feature.Base.FeatureBuilder>();
                var controllers = injector.GetService<ControllersService>();
                controllers.ClearCache(applyBaseMask: false);
                var fx = controllers.GetFx();
                var oldDefault = type == VRCExpressionParameters.ValueType.Float ? 0.75f : 1;
                var parameter = type == VRCExpressionParameters.ValueType.Float ? fx.GetOrCreateFloat("Shared", oldDefault)
                    : type == VRCExpressionParameters.ValueType.Int ? fx.GetOrCreateInt("Shared", oldDefault)
                    : fx.GetOrCreateBool("Shared", oldDefault);
                if (expressionAlreadyExists) {
                    var expression = new VRCExpressionParameters.Parameter {
                        name = parameter.name, valueType = type, defaultValue = oldDefault, saved = false
                    };
                    if (type == VRCExpressionParameters.ValueType.Int) expression.DisableInterpolation();
                    injector.GetService<ExpressionParamsService>().AddOrMerge(expression);
                }
                var toggle = injector.CreateAndFillObject<VF.Feature.ToggleBuilder>();
                toggle.featureBaseObject = root;
                toggle.model = new VF.Model.Feature.Toggle {
                    name = "Toggle",
                    useGlobalParam = true,
                    globalParam = "Shared",
                    slider = type == VRCExpressionParameters.ValueType.Float,
                    useInt = type == VRCExpressionParameters.ValueType.Int,
                    defaultSliderValue = 0.25f,
                    defaultOn = false,
                    addMenuItem = false
                };
                toggle.Apply();
                var expected = toggle.model.slider ? 0.25f : 0;
                Assert.That(parameter.GetDefault(), Is.EqualTo(expected));
                Assert.That(avatar.expressionParameters.FindParameter("Shared").defaultValue,
                    Is.EqualTo(expressionAlreadyExists ? oldDefault : expected));
            } finally {
                Object.DestroyImmediate(root);
            }
        }

        [TestCase(false)]
        [TestCase(true)]
        public void OgbOverridesExistingControllerAndExpressionDefaults(bool expressionAlreadyExists) {
            var root = new GameObject("OGB defaults");
            try {
                var avatar = root.AddComponent<VRCAvatarDescriptor>();
                avatar.baseAnimationLayers = new[] {
                    new VRCAvatarDescriptor.CustomAnimLayer { type = VRCAvatarDescriptor.AnimLayerType.FX }
                };
                avatar.specialAnimationLayers = new[] {
                    new VRCAvatarDescriptor.CustomAnimLayer { type = VRCAvatarDescriptor.AnimLayerType.Action }
                };
                var injector = VRCFuryInjectorBuilder.GetInjector(avatar);
                var controllers = injector.GetService<ControllersService>();
                controllers.ClearCache(applyBaseMask: false);
                var parameter = controllers.GetFx().GetOrCreateBool("OGB_ENABLED", true);
                if (expressionAlreadyExists) parameter.MakeExpressionBool(synced: false);
                injector.GetService<OgbEnabledService>().Register(root);
                Assert.That(parameter.GetDefault(), Is.Zero);
                var expression = avatar.expressionParameters.FindParameter("OGB_ENABLED");
                Assert.That(expression.defaultValue, Is.Zero);
                Assert.That(expression.IsNetworkSynced(), Is.False);
            } finally {
                Object.DestroyImmediate(root);
            }
        }

#if VRCSDK_HAS_DYNAMIC_PARAMETER_BIT_WIDTH
        [TestCase(VRCExpressionParameters.ValueType.Int, 3, false)]
        [TestCase(VRCExpressionParameters.ValueType.Float, 3, false)]
        [TestCase(VRCExpressionParameters.ValueType.Int, 8, true)]
        [TestCase(VRCExpressionParameters.ValueType.Float, 8, true)]
        public void MakeExpressionRejectsConflictingEncodingWithoutChangingExistingSettings(
            VRCExpressionParameters.ValueType type, int width, bool flipped) {
            var root = new GameObject("Conflicting expression encoding");
            try {
                var expressions = GetExpressionParams(root);
                var original = new VRCExpressionParameters.Parameter {
                    name = "Shared", valueType = type, bitWidth = (byte)width,
                    flippedRepresentation = flipped, saved = true, defaultValue = 1
                };
                original.SetNetworkSynced(false);
                expressions.AddOrMerge(original);
                var before = JsonUtility.ToJson(original);
                var controller = CreateController(expressions);
                var parameter = type == VRCExpressionParameters.ValueType.Int
                    ? controller.GetOrCreateInt("Shared") : controller.GetOrCreateFloat("Shared");

                var error = Assert.Throws<Exception>(() => parameter.MakeExpression(type));

                Assert.That(error.Message, Does.Contain("bit width or signedness"));
                Assert.That(JsonUtility.ToJson(expressions.Single()), Is.EqualTo(before));
            } finally {
                Object.DestroyImmediate(root);
            }
        }
#endif

        [Test]
        public void ExpressionServiceEnumeratesOwnedParametersAndSnapshotsDoNotMutateImportedAssets() {
            var root = new GameObject("Expression iteration");
            var imported = ScriptableObject.CreateInstance<VRCExpressionParameters>();
            try {
                var service = GetExpressionParams(root);
                var avatar = root.GetComponent<VRCAvatarDescriptor>();
                var local = new VRCExpressionParameters.Parameter { name = "Local", valueType = VRCExpressionParameters.ValueType.Int };
                local.SetNetworkSynced(false);
                imported.parameters = new[] {
                    new VRCExpressionParameters.Parameter { name = "Enabled", valueType = VRCExpressionParameters.ValueType.Bool },
                    new VRCExpressionParameters.Parameter { name = "Depth", valueType = VRCExpressionParameters.ValueType.Float },
                    new VRCExpressionParameters.Parameter { name = "", valueType = VRCExpressionParameters.ValueType.Int },
                    local
                };
                avatar.customExpressions = true;
                avatar.expressionParameters = imported;
                var snapshot = service.GetSnapshot();
                Assert.That(service.CalcTotalCost(), Is.EqualTo(imported.CalcTotalCost()));
                Assert.That(avatar.expressionParameters, Is.SameAs(imported));
                snapshot[0].defaultValue = 1;
                Assert.That(imported.parameters[0].defaultValue, Is.Zero);
                var owned = service.ToArray();
                owned[0].defaultValue = 1;
                Assert.That(avatar.expressionParameters, Is.Not.SameAs(imported));
                Assert.That(imported.parameters[0].defaultValue, Is.Zero);
                service.SetParameters(owned.Reverse());
                Assert.That(((System.Collections.IEnumerable)service).Cast<VRCExpressionParameters.Parameter>(),
                    Is.EqualTo(owned.Reverse()));
            } finally {
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(imported);
            }
        }

        [Test]
        public void CompressorSnapshotsPreserveDuplicateEntriesForSdkCostAccounting() {
            var root = new GameObject("Duplicate expression parameters");
            var imported = ScriptableObject.CreateInstance<VRCExpressionParameters>();
            try {
                var service = GetExpressionParams(root);
                var avatar = root.GetComponent<VRCAvatarDescriptor>();
                imported.parameters = Enumerable.Range(0, 33).Select(_ => new VRCExpressionParameters.Parameter {
                    name = "DuplicateInt", valueType = VRCExpressionParameters.ValueType.Int
                }).ToArray();
                avatar.customExpressions = true;
                avatar.expressionParameters = imported;

                var originalCost = service.CalcTotalCost();
                var snapshot = service.GetSnapshot();
                Assert.That(originalCost, Is.EqualTo(264));
                Assert.That(snapshot, Has.Length.EqualTo(33));
                var decision = new OptimizationDecision { compress = snapshot };
                decision.Optimize(originalCost);
                Assert.That(decision.GetFinalCost(originalCost), Is.LessThanOrEqualTo(ExpressionParamsService.GetMaxCost()));
                Assert.That(avatar.expressionParameters, Is.SameAs(imported));
                Assert.That(imported.parameters, Has.Length.EqualTo(33));
            } finally {
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(imported);
            }
        }

        [Test]
        public void MovingAParameterKeepsItsIdentityAndUsesTheDestinationExpressionService() {
            var sourceRoot = new GameObject("Source avatar");
            var destinationRoot = new GameObject("Destination avatar");
            try {
                var sourceExpressions = GetExpressionParams(sourceRoot);
                var destinationExpressions = GetExpressionParams(destinationRoot);
                var source = CreateController(sourceExpressions);
                var destination = CreateController(destinationExpressions);
                var parameter = source.GetOrCreateBool("Moved", true);
                destination.TakeOwnershipOf(source);
                Assert.That(source.TryGetParam("Moved", out _), Is.False);
                Assert.That(destination.GetOrCreateBool("Moved"), Is.SameAs(parameter));
                parameter.MakeExpressionBool(saved: true);
                Assert.That(sourceExpressions.Contains("Moved"), Is.False);
                Assert.That(destinationExpressions.Single().defaultValue, Is.EqualTo(1));
                Assert.That(destinationExpressions.Single().saved, Is.True);
                var clone = destination.Clone();
                Assert.That(clone, Is.TypeOf<VFAvatarController>());
                Assert.That(clone.GetOrCreateBool("Moved"), Is.Not.SameAs(parameter));
                clone.GetOrCreateBool("FromClone").MakeExpressionBool();
                Assert.That(destinationExpressions.Contains("FromClone"), Is.True);
                clone.Clone().GetOrCreateBool("FromSecondClone").MakeExpressionBool();
                Assert.That(destinationExpressions.Contains("FromSecondClone"), Is.True);
            } finally {
                Object.DestroyImmediate(sourceRoot);
                Object.DestroyImmediate(destinationRoot);
            }
        }

        [TestCase(VRCExpressionParameters.ValueType.Bool, false)]
        [TestCase(VRCExpressionParameters.ValueType.Bool, true)]
        [TestCase(VRCExpressionParameters.ValueType.Int, false)]
        [TestCase(VRCExpressionParameters.ValueType.Int, true)]
        [TestCase(VRCExpressionParameters.ValueType.Float, false)]
        [TestCase(VRCExpressionParameters.ValueType.Float, true)]
        public void MakeExpressionUsesTheRequestedNetworkSync(VRCExpressionParameters.ValueType type, bool synced) {
            var root = new GameObject("Expression network sync");
            try {
                var expressions = GetExpressionParams(root);
                var parameter = CreateController(expressions).GetOrCreateBool("Value");
                switch (type) {
                    case VRCExpressionParameters.ValueType.Bool: parameter.MakeExpressionBool(synced, saved: true); break;
                    case VRCExpressionParameters.ValueType.Int: parameter.MakeExpressionInt(synced, saved: true); break;
                    case VRCExpressionParameters.ValueType.Float: parameter.MakeExpressionFloat(synced, saved: true); break;
                }
                var expression = expressions.Single(p => p.name == parameter.name);
                Assert.That(expression.valueType, Is.EqualTo(type));
                Assert.That(expression.IsNetworkSynced(), Is.EqualTo(synced));
                Assert.That(expression.saved, Is.True);
            } finally {
                Object.DestroyImmediate(root);
            }
        }

        [TestCase(VRCExpressionParameters.ValueType.Bool, VRCExpressionParameters.ValueType.Float)]
        [TestCase(VRCExpressionParameters.ValueType.Bool, VRCExpressionParameters.ValueType.Int)]
        [TestCase(VRCExpressionParameters.ValueType.Int, VRCExpressionParameters.ValueType.Bool)]
        [TestCase(VRCExpressionParameters.ValueType.Int, VRCExpressionParameters.ValueType.Float)]
        [TestCase(VRCExpressionParameters.ValueType.Float, VRCExpressionParameters.ValueType.Bool)]
        [TestCase(VRCExpressionParameters.ValueType.Float, VRCExpressionParameters.ValueType.Int)]
        public void MakeExpressionRejectsConflictingTypesWithoutChangingExistingSettings(
            VRCExpressionParameters.ValueType existingType, VRCExpressionParameters.ValueType requestedType) {
            var root = new GameObject("Conflicting expression types");
            try {
                var avatar = root.AddComponent<VRCAvatarDescriptor>();
                var expressions = VRCFuryInjectorBuilder.GetInjector(avatar).GetService<ExpressionParamsService>();
                var parameter = CreateController(expressions).GetOrCreateBool("Shared");
                var existing = parameter.MakeExpression(existingType, saved: true);
                var exception = Assert.Throws<Exception>(() => parameter.MakeExpression(requestedType, saved: false));
                Assert.That(exception.Message, Does.Contain("Shared").And.Contain(existingType.ToString()).And.Contain(requestedType.ToString()));
                Assert.That(expressions.ToArray(), Has.Length.EqualTo(1));
                Assert.That(existing.valueType, Is.EqualTo(existingType));
                Assert.That(existing.saved, Is.True);
            } finally {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void SyncedHandlesKeepTheirAvatarContextThroughLookupCloningAndMerging() {
            VRCAvatarDescriptor MakeAvatar(string name) {
                var avatar = new GameObject(name).AddComponent<VRCAvatarDescriptor>();
                avatar.baseAnimationLayers = new[] {
                    new VRCAvatarDescriptor.CustomAnimLayer { type = VRCAvatarDescriptor.AnimLayerType.FX }
                };
                avatar.specialAnimationLayers = Array.Empty<VRCAvatarDescriptor.CustomAnimLayer>();
                return avatar;
            }
            var first = MakeAvatar("First parameter context");
            var second = MakeAvatar("Second parameter context");
            try {
                VFAvatarController GetFx(VRCAvatarDescriptor avatar) {
                    var controllers = VRCFuryInjectorBuilder.GetInjector(avatar).GetService<ControllersService>();
                    controllers.ClearCache(applyBaseMask: false);
                    return controllers.GetFx();
                }
                var fx = GetFx(first);
                var otherFx = GetFx(second);
                var input = fx.CreateUniqueBool("Input", 0.75f);
                Assert.That(first.expressionParameters, Is.Null, "Handles must not create expression assets until requested");
                input.MakeExpressionFloat(synced: false, saved: true);
                Assert.That(second.expressionParameters, Is.Null);
                Assert.That(fx.TryGetParam(input.name, out var found), Is.True);
                found.MakeExpression(VRCExpressionParameters.ValueType.Float, synced: false, saved: true);
                var expression = first.expressionParameters.FindParameter(input.name);
                Assert.That(expression.defaultValue, Is.EqualTo(0.75f));
                Assert.That(expression.saved, Is.True);
                Assert.That(expression.IsNetworkSynced(), Is.False);

                var clone = fx.Clone();
                clone.RewriteParameters(name => "Clone/" + name);
                clone.GetOrCreateBool("Clone/" + input.name).MakeExpressionFloat(synced: false, saved: true);
                Assert.That(first.expressionParameters.FindParameter("Clone/" + input.name).defaultValue, Is.EqualTo(0.75f));
                otherFx.TakeOwnershipOf(clone);
                otherFx.GetOrCreateBool("Clone/" + input.name).MakeExpressionFloat(synced: false, saved: true);
                Assert.That(second.expressionParameters.FindParameter("Clone/" + input.name).defaultValue, Is.EqualTo(0.75f));
                otherFx.GetOrCreateBool(input.name, 1).MakeExpressionBool();
                Assert.That(second.expressionParameters.FindParameter(input.name).valueType, Is.EqualTo(VRCExpressionParameters.ValueType.Bool));
                Assert.That(second.expressionParameters.FindParameter(input.name).saved, Is.False);
                Assert.That(first.expressionParameters.FindParameter(input.name).valueType, Is.EqualTo(VRCExpressionParameters.ValueType.Float));
                Assert.Throws<InvalidOperationException>(() => VFController.Create().GetOrCreateBool("No avatar").MakeExpressionBool());
            } finally {
                Object.DestroyImmediate(first.gameObject);
                Object.DestroyImmediate(second.gameObject);
            }
        }

        [TestCase(false)]
        [TestCase(true)]
        public void UniqueNamesCheckAllControllersAndForgetDiscardedControllers(bool overrides) {
            var root = new GameObject("Unloaded parameter names");
            var raw = new UnityEditor.Animations.AnimatorController {
                parameters = new[] { new AnimatorControllerParameter { name = "VF7_Existing", type = AnimatorControllerParameterType.Float } }
            };
            var wrapper = new AnimatorOverrideController(raw);
            var action = new UnityEditor.Animations.AnimatorController {
                parameters = new[] { new AnimatorControllerParameter { name = "VF7_FromAction", type = AnimatorControllerParameterType.Float } }
            };
            try {
                var avatar = root.AddComponent<VRCAvatarDescriptor>();
                avatar.customizeAnimationLayers = true;
                avatar.baseAnimationLayers = new[] {
                    new VRCAvatarDescriptor.CustomAnimLayer {
                        type = VRCAvatarDescriptor.AnimLayerType.FX, animatorController = overrides ? (RuntimeAnimatorController)wrapper : raw
                    }
                };
                avatar.specialAnimationLayers = new[] {
                    new VRCAvatarDescriptor.CustomAnimLayer {
                        type = VRCAvatarDescriptor.AnimLayerType.Action, animatorController = action
                    }
                };
                var injector = VRCFuryInjectorBuilder.GetInjector(avatar);
                injector.GetService<GlobalsService>().currentFeatureNum = 7;
                var names = injector.GetService<UniqueParamService>();
                var controllers = injector.GetService<ControllersService>();
                controllers.ClearCache(applyBaseMask: false);
                Assert.That(names.GetUniqueName("Existing"), Is.EqualTo("VF7_Existing_2"));
                Assert.That(names.GetUniqueName("FromAction"), Is.EqualTo("VF7_FromAction_2"));
                Assert.That(controllers.GetAllMutatedControllers().Count, Is.EqualTo(2));
                controllers.ClearCache(applyBaseMask: false);
                var fx = controllers.GetFx();
                fx.GetOrCreateBool("VF7_Transient");
                Assert.That(names.GetUniqueName("Transient"), Is.EqualTo("VF7_Transient_2"));
                controllers.ClearCache(applyBaseMask: false);
                Assert.That(names.GetUniqueName("Transient"), Is.EqualTo("VF7_Transient"), "Discarded controller definitions must no longer block names");
            } finally {
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(wrapper);
                Object.DestroyImmediate(raw);
                Object.DestroyImmediate(action);
            }
        }

        [Test]
        public void SpsRandomParametersAreDeclaredAsIntsInFxAndDrivenFromAction() {
            var root = new GameObject("SPS cross-controller types");
            try {
                var avatar = root.AddComponent<VRCAvatarDescriptor>();
                avatar.baseAnimationLayers = new[] {
                    new VRCAvatarDescriptor.CustomAnimLayer { type = VRCAvatarDescriptor.AnimLayerType.FX }
                };
                avatar.specialAnimationLayers = new[] {
                    new VRCAvatarDescriptor.CustomAnimLayer { type = VRCAvatarDescriptor.AnimLayerType.Action }
                };
                var injector = VRCFuryInjectorBuilder.GetInjector(avatar);
                var controllers = injector.GetService<ControllersService>();
                controllers.ClearCache(applyBaseMask: false);
                injector.GetService<SpsPlayerIdService>().Register(root.AddComponent<MeshRenderer>());
                injector.GetService<NoBadControllerParamsService>().Apply();
                var random = controllers.GetFx().parameters.Where(p => p.name.EndsWith("_RANDOM")).ToArray();
                Assert.That(random, Has.Length.EqualTo(2));
                Assert.That(random.All(p => p.type == AnimatorControllerParameterType.Int), Is.True);
                Assert.That(controllers.GetAction().parameters.All(p => !p.name.EndsWith("_RANDOM")), Is.True,
                    "Repair must not need duplicate declarations in the driver's controller");
            } finally {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void CompressorBooleanIndicesDoNotMultiplyTransitionsDuringRepair() {
            var root = new GameObject("Compressor Boolean indices");
            try {
                var avatar = root.AddComponent<VRCAvatarDescriptor>();
                avatar.baseAnimationLayers = Array.Empty<VRCAvatarDescriptor.CustomAnimLayer>();
                avatar.specialAnimationLayers = new[] {
                    new VRCAvatarDescriptor.CustomAnimLayer { type = VRCAvatarDescriptor.AnimLayerType.Action }
                };
                var injector = VRCFuryInjectorBuilder.GetInjector(avatar);
                var controllers = injector.GetService<ControllersService>();
                controllers.ClearCache(applyBaseMask: false);
                var controller = controllers.GetAction();
                injector.GetService<ParameterCompressorLayerService>().BuildLayer(new OptimizationDecision {
                    numberSlots = 1,
                    compress = Enumerable.Range(0, 31).Select(i => new VRCExpressionParameters.Parameter {
                        name = "Slider" + i, valueType = VRCExpressionParameters.ValueType.Float
                    }).ToArray()
                }, controller);
                var before = controller.layers.Sum(l => l.allTransitions.Count());
                injector.GetService<NoBadControllerParamsService>().Apply();
                var indices = controller.parameters.Where(p => p.name.Contains("SyncIndex")).ToArray();
                Assert.That(indices, Has.Length.EqualTo(5));
                Assert.That(indices.All(p => p.type == AnimatorControllerParameterType.Bool), Is.True);
                Assert.That(controller.layers.Sum(l => l.allTransitions.Count()), Is.EqualTo(before));
            } finally {
                Object.DestroyImmediate(root);
            }
        }

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
        public void NameChecksDoNotCreateExpressionAssetsAndNewEntriesDefaultToUnsaved() {
            var root = new GameObject("Expression parameter lookup");
            var imported = ScriptableObject.CreateInstance<VRCExpressionParameters>();
            try {
                var avatar = root.AddComponent<VRCAvatarDescriptor>();
                avatar.baseAnimationLayers = new[] {
                    new VRCAvatarDescriptor.CustomAnimLayer { type = VRCAvatarDescriptor.AnimLayerType.FX }
                };
                avatar.specialAnimationLayers = Array.Empty<VRCAvatarDescriptor.CustomAnimLayer>();
                var injector = VRCFuryInjectorBuilder.GetInjector(avatar);
                var controllers = injector.GetService<ControllersService>();
                controllers.ClearCache(applyBaseMask: false);
                controllers.GetFx();
                var names = injector.GetService<UniqueParamService>();
                var expressions = injector.GetService<ExpressionParamsService>();
                var count = Resources.FindObjectsOfTypeAll<VRCExpressionParameters>().Length;
                Assert.That(expressions.Contains("Missing"), Is.False);
                names.GetUniqueName("One");
                names.GetUniqueName("Two");
                Assert.That(avatar.expressionParameters, Is.Null);
                Assert.That(Resources.FindObjectsOfTypeAll<VRCExpressionParameters>(), Has.Length.EqualTo(count));

                imported.parameters = new[] { new VRCExpressionParameters.Parameter {
                    name = "Imported", valueType = VRCExpressionParameters.ValueType.Bool, saved = true
                } };
                avatar.customExpressions = true;
                avatar.expressionParameters = imported;
                Assert.That(expressions.Contains("Imported"), Is.True);
                Assert.That(avatar.expressionParameters, Is.SameAs(imported));
                Assert.That(Resources.FindObjectsOfTypeAll<VRCExpressionParameters>(), Has.Length.EqualTo(count));

                var created = expressions.AddOrMerge(new VRCExpressionParameters.Parameter {
                    name = "New", saved = false, valueType = VRCExpressionParameters.ValueType.Bool
                });
                Assert.That(created.saved, Is.False);
                Assert.That(expressions.Contains("New"), Is.True);
                Assert.That(expressions.AddOrMerge(new VRCExpressionParameters.Parameter { name = "Saved", saved = true }).saved, Is.True);
                Assert.That(expressions.AddOrMerge(imported.parameters[0].Clone()).saved, Is.True);
                Assert.That(imported.parameters, Has.Length.EqualTo(1));
            } finally {
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(imported);
            }
        }

        [Test]
        public void BufferCanInheritItsInputDefaultOrExplicitlyUseNegativeOneHundred() {
            var root = new GameObject("Buffer defaults");
            try {
                var avatar = root.AddComponent<VRCAvatarDescriptor>();
                avatar.baseAnimationLayers = new[] {
                    new VRCAvatarDescriptor.CustomAnimLayer { type = VRCAvatarDescriptor.AnimLayerType.FX }
                };
                avatar.specialAnimationLayers = Array.Empty<VRCAvatarDescriptor.CustomAnimLayer>();
                var controllers = VRCFuryInjectorBuilder.GetInjector(avatar).GetService<ControllersService>();
                controllers.ClearCache(applyBaseMask: false);
                var fx = controllers.GetFx();
                var input = fx.CreateUniqueBool("Input", 0.25f);
                var math = new BlendtreeMath(fx, VFBlendTreeDirect.Create("Buffers"));
                Assert.That(math.Buffer(input).GetDefault(), Is.EqualTo(0.25f));
                Assert.That(math.Buffer(input, def: -100).GetDefault(), Is.EqualTo(-100));
                Assert.That(math.Buffer(input, def: true).GetDefault(), Is.EqualTo(1));
                Assert.That(math.Buffer(input, def: false).GetDefault(), Is.Zero);
                var before = fx.parameters.Length;
                fx.Always();
                fx.Never();
                fx.One();
                Assert.That(fx.parameters, Has.Length.EqualTo(before + 1), "True and one share one constant");
            } finally {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void ControllerHelpersDeclareTypesBeforeTheFinalRepairPass() {
            var controller = new VFAvatarController(VFController.Create(), VRCAvatarDescriptor.AnimLayerType.FX,
                () => 0, () => "", name => "Unique/" + name, new LayerSourceService());
            Assert.That(controller.One().type, Is.EqualTo(AnimatorControllerParameterType.Float));
            Assert.That(controller.Zero().type, Is.EqualTo(AnimatorControllerParameterType.Float));
            Assert.That(controller.GestureLeft().type, Is.EqualTo(AnimatorControllerParameterType.Int));
            Assert.That(controller.GestureRight().type, Is.EqualTo(AnimatorControllerParameterType.Int));
            Assert.That(controller.Viseme().type, Is.EqualTo(AnimatorControllerParameterType.Int));
            Assert.That(controller.GestureLeftWeight().type, Is.EqualTo(AnimatorControllerParameterType.Float));
            Assert.That(controller.GestureRightWeight().type, Is.EqualTo(AnimatorControllerParameterType.Float));
            Assert.That(controller.IsLocal().type, Is.EqualTo(AnimatorControllerParameterType.Bool));
            Assert.That(controller.CreateUniqueFloat("Weight").type,
                Is.EqualTo(AnimatorControllerParameterType.Float));
            Assert.That(controller.CreateUniqueInt("Counter").type,
                Is.EqualTo(AnimatorControllerParameterType.Int));
            Assert.That(controller.CreateUniqueBool("Toggle").type,
                Is.EqualTo(AnimatorControllerParameterType.Bool));
        }

        [Test]
        public void UniqueCreationUsesGetOrCreateWithoutReplacingTheExistingDefault() {
            var controller = new VFAvatarController(VFController.Create(), VRCAvatarDescriptor.AnimLayerType.FX,
                () => 0, () => "", _ => "Taken", new LayerSourceService());
            var existing = controller.GetOrCreateBool("Taken", 0.25f);
            var reused = controller.CreateUniqueBool("Feature", 1);
            Assert.That(reused.name, Is.EqualTo(existing.name));
            Assert.That(reused.type, Is.EqualTo(AnimatorControllerParameterType.Float));
            Assert.That(reused.GetDefault(), Is.EqualTo(0.25f));
            Assert.That(existing.GetDefault(), Is.EqualTo(0.25f));
            Assert.That(controller.parameters.Length, Is.EqualTo(1));
        }

        [Test]
        public void ControllerUniqueCreationUsesTheBoundAllocatorAndLiteralRegistrationDoesNot() {
            var root = new GameObject("Bound unique parameters");
            try {
                var avatar = root.AddComponent<VRCAvatarDescriptor>();
                avatar.baseAnimationLayers = new[] {
                    new VRCAvatarDescriptor.CustomAnimLayer { type = VRCAvatarDescriptor.AnimLayerType.FX }
                };
                avatar.specialAnimationLayers = new[] {
                    new VRCAvatarDescriptor.CustomAnimLayer { type = VRCAvatarDescriptor.AnimLayerType.Action }
                };
                var injector = VRCFuryInjectorBuilder.GetInjector(avatar);
                var globals = injector.GetService<GlobalsService>();
                globals.currentFeatureNum = 7;
                var controllers = injector.GetService<ControllersService>();
                controllers.ClearCache(applyBaseMask: false);
                var fx = controllers.GetFx();
                var literal = fx.GetOrCreateBool("Depth", 2);
                var first = fx.CreateUniqueBool("Depth", def: true);
                var second = fx.CreateUniqueBool("Depth", def: 3);
                var third = controllers.GetAction().CreateUniqueBool("Depth", def: 4);
                Assert.That(first.name, Is.EqualTo("VF7_Depth"));
                Assert.That(second.name, Is.EqualTo("VF7_Depth_2"));
                Assert.That(third.name, Is.EqualTo("VF7_Depth_3"));
                Assert.That(first.GetDefault(), Is.EqualTo(1));
                Assert.That(second.GetDefault(), Is.EqualTo(3));
                Assert.That(third.GetDefault(), Is.EqualTo(4));
                Assert.That(literal.name, Is.EqualTo("Depth"));
                Assert.That(fx.GetOrCreateBool("Depth", 100).GetDefault(), Is.EqualTo(2));
                globals.currentFeatureNum = 8;
                Assert.That(fx.CreateUniqueBool("Depth").name, Is.EqualTo("VF8_Depth"),
                    "The callback must use the current feature rather than the controller's creation context");
                Assert.That(avatar.expressionParameters, Is.Null);
            } finally {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void AllocationAnimatorRegistrationAndExpressionRegistrationAreIndependent() {
            var root = new GameObject("Parameter flow");
            try {
                var avatar = root.AddComponent<VRCAvatarDescriptor>();
                avatar.baseAnimationLayers = new[] {
                    new VRCAvatarDescriptor.CustomAnimLayer { type = VRCAvatarDescriptor.AnimLayerType.FX }
                };
                avatar.specialAnimationLayers = new[] {
                    new VRCAvatarDescriptor.CustomAnimLayer { type = VRCAvatarDescriptor.AnimLayerType.Action }
                };
                var injector = VRCFuryInjectorBuilder.GetInjector(avatar);
                var controllers = injector.GetService<ControllersService>();
                controllers.ClearCache(applyBaseMask: false);
                var name = injector.GetService<UniqueParamService>().GetUniqueName("Feature");
                var fx = controllers.GetFx();
                Assert.That(FindDefinition(fx, name), Is.Null);
                Assert.That(avatar.expressionParameters, Is.Null);
                var parameter = fx.GetOrCreateBool(name, 0.25f);
                Assert.That(avatar.expressionParameters, Is.Null, "Animator registration must not create expression entries");
                var expressions = injector.GetService<ExpressionParamsService>();
                var expression = expressions.AddOrMerge(new VRCExpressionParameters.Parameter {
                    name = parameter.name, valueType = VRCExpressionParameters.ValueType.Float,
                    defaultValue = parameter.GetDefault(), saved = true
                });
                Assert.That(expression, Is.SameAs(avatar.expressionParameters.FindParameter(name)));
                Assert.That(expressions.ToArray(), Has.Length.EqualTo(1));
                Assert.That(expression.defaultValue, Is.EqualTo(0.25f));
                var actionParameter = parameter.CopyTo(controllers.GetAction());
                parameter.SetDefault(0.75f);
                Assert.That(fx.GetOrCreateBool(name).GetDefault(), Is.EqualTo(0.75f));
                Assert.That(actionParameter.GetDefault(), Is.EqualTo(0.25f));
                Assert.That(expression.defaultValue, Is.EqualTo(0.25f), "Expression defaults are configured independently");
            } finally {
                Object.DestroyImmediate(root);
            }
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

        [Test]
        public void UniqueNamesAvoidExpressionsAllControllersAndOutstandingReservations() {
            var root = new GameObject("Unique parameter names");
            try {
                var avatar = root.AddComponent<VRCAvatarDescriptor>();
                avatar.baseAnimationLayers = new[] {
                    new VRCAvatarDescriptor.CustomAnimLayer { type = VRCAvatarDescriptor.AnimLayerType.FX }
                };
                avatar.specialAnimationLayers = new[] {
                    new VRCAvatarDescriptor.CustomAnimLayer { type = VRCAvatarDescriptor.AnimLayerType.Action }
                };
                var injector = VRCFuryInjectorBuilder.GetInjector(avatar);
                injector.GetService<GlobalsService>().currentFeatureNum = 7;
                var controllers = injector.GetService<ControllersService>();
                controllers.ClearCache(applyBaseMask: false);
                var expressionParams = injector.GetService<ExpressionParamsService>();
                expressionParams.AddOrMerge(new VRCExpressionParameters.Parameter { name = "VF7_Test", valueType = VRCExpressionParameters.ValueType.Bool, saved = false });
                var fx = controllers.GetFx();
                Assert.That(fx.GetOrCreateBool("VF7_Test_2").name, Is.EqualTo("VF7_Test_2"));
                Assert.That(controllers.GetAction().GetOrCreateBool("VF7_Test_3").name, Is.EqualTo("VF7_Test_3"));
                var uniqueParams = injector.GetService<UniqueParamService>();
                var first = uniqueParams.GetUniqueName("Test");
                Assert.That(first, Is.EqualTo("VF7_Test_4"));
                Assert.That(uniqueParams.GetUniqueName("Test"), Is.EqualTo("VF7_Test_5"));
                Assert.That(FindDefinition(fx, first), Is.Null, "Allocation must not create an animator parameter");
                Assert.That(expressionParams.ToArray(), Has.Length.EqualTo(1));
                Assert.That(injector.GetService<ParameterSourceService>().GetSource(first).originalParamName, Is.EqualTo("Test"));
            } finally {
                Object.DestroyImmediate(root);
            }
        }

        [TestCase(false, false)]
        [TestCase(true, false)]
        [TestCase(false, true)]
        [TestCase(true, true)]
        public void ExpressionMergeKeepsTheFirstDefaultSavedAndSyncedValues(bool changeSaved, bool changeSync) {
            var root = new GameObject("Expression merge settings");
            try {
                var service = GetExpressionParams(root);
                var original = service.AddOrMerge(ExpressionParam.Create("Shared").Float().Default(0.75f).Saved());
                var input = original.Clone();
                input.defaultValue = 0;
                if (changeSaved) input.saved = false;
                if (changeSync) input.Unsynced();
                var before = JsonUtility.ToJson(original);
                Assert.That(service.AddOrMerge(input), Is.SameAs(original));
                Assert.That(JsonUtility.ToJson(original), Is.EqualTo(before));
            } finally {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void MergeKeepsTheFirstDefaultAndReturnsTheOwnedParameter() {
            var root = new GameObject("Parameter creation");
            var imported = ScriptableObject.CreateInstance<VRCExpressionParameters>();
            try {
                var avatar = root.AddComponent<VRCAvatarDescriptor>();
                imported.parameters = new[] { new VRCExpressionParameters.Parameter {
                    name = "Imported", valueType = VRCExpressionParameters.ValueType.Float,
                    defaultValue = 0.75f, saved = true
                } };
                avatar.customExpressions = true;
                avatar.expressionParameters = imported;
                var service = VRCFuryInjectorBuilder.GetInjector(avatar).GetService<ExpressionParamsService>();
                var input = imported.parameters[0].Clone();
                input.defaultValue = 0;
                var existing = service.AddOrMerge(input);
                Assert.That(existing.saved, Is.True);
                Assert.That(existing.defaultValue, Is.EqualTo(0.75f));
                Assert.That(existing, Is.Not.SameAs(imported.parameters[0]));
                var created = service.AddOrMerge(new VRCExpressionParameters.Parameter {
                    name = "Created", valueType = VRCExpressionParameters.ValueType.Int, saved = false
                });
                Assert.That(created.name, Is.EqualTo("Created"));
                Assert.That(service.AddOrMerge(created.Clone()), Is.SameAs(created));
                Assert.That(service.ToArray(), Has.Length.EqualTo(2));
                Assert.That(imported.parameters, Has.Length.EqualTo(1));
            } finally {
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(imported);
            }
        }

        [TestCase(0, false)]
        [TestCase(1, true)]
        [TestCase(0.5f, true)]
        [TestCase(-0.5f, true)]
        public void LegacyCompressorImportsBooleanDefaultsWithoutPromotingTheirStorage(float defaultValue, bool expected) {
            var root = new GameObject("Legacy compressor Bool import");
            try {
                var avatar = root.AddComponent<VRCAvatarDescriptor>();
                avatar.baseAnimationLayers = Array.Empty<VRCAvatarDescriptor.CustomAnimLayer>();
                avatar.specialAnimationLayers = new[] {
                    new VRCAvatarDescriptor.CustomAnimLayer { type = VRCAvatarDescriptor.AnimLayerType.Action }
                };
                var injector = VRCFuryInjectorBuilder.GetInjector(avatar);
                var controllers = injector.GetService<ControllersService>();
                controllers.ClearCache(applyBaseMask: false);
                var controller = controllers.GetAction();
                var original = new VRCExpressionParameters.Parameter {
                    name = "ImportedBool", valueType = VRCExpressionParameters.ValueType.Bool, defaultValue = defaultValue
                };
                injector.GetService<ExpressionParamsService>().SetParameters(new[] { original });
                injector.GetService<ParameterCompressorLegacyLayerService>().BuildLayer(
                    new OptimizationDecision { boolSlots = 1, compress = new[] { original } }, controller);
                Assert.That(controller.TryGetParam(original.name, out var imported), Is.True);
                Assert.That(imported.type, Is.EqualTo(AnimatorControllerParameterType.Bool));
                Assert.That(imported.GetDefault(), Is.EqualTo(expected ? 1 : 0));
            } finally {
                Object.DestroyImmediate(root);
            }
        }

#if VRCSDK_HAS_PARAMETER_INTERPOLATION
        [TestCase(false)]
        [TestCase(true)]
        public void CompressorTransportNeverInterpolates(bool legacy) {
            var root = new GameObject("Compressor interpolation");
            try {
                var avatar = root.AddComponent<VRCAvatarDescriptor>();
                avatar.baseAnimationLayers = Array.Empty<VRCAvatarDescriptor.CustomAnimLayer>();
                avatar.specialAnimationLayers = new[] {
                    new VRCAvatarDescriptor.CustomAnimLayer { type = VRCAvatarDescriptor.AnimLayerType.Action }
                };
                var injector = VRCFuryInjectorBuilder.GetInjector(avatar);
                var controllers = injector.GetService<ControllersService>();
                controllers.ClearCache(applyBaseMask: false);
                var controller = controllers.GetAction();
                var expressionParams = injector.GetService<ExpressionParamsService>();
                var paramz = expressionParams;
                var sliderAnimator = controller.GetOrCreateBool("Slider");
                var boolAnimator = controller.GetOrCreateBool("Toggle");
                Assert.That(paramz, Is.Empty, "Animator parameters must not create expression entries");
                sliderAnimator.MakeExpressionFloat();
                var modeAnimator = controller.GetOrCreateBool("Mode");
                Assert.That(paramz.Contains(modeAnimator.name), Is.False, "Animator parameters must not create expression entries");
                modeAnimator.MakeExpressionInt();
                var originals = paramz.ToArray();
                var decision = new OptimizationDecision { numberSlots = 1, compress = originals };
                if (legacy) injector.GetService<ParameterCompressorLegacyLayerService>().BuildLayer(decision, controller);
                else injector.GetService<ParameterCompressorLayerService>().BuildLayer(decision, controller);
                var transport = paramz.Except(originals)
                    .Where(p => p.IsNetworkSynced() && p.valueType != VRCExpressionParameters.ValueType.Bool).ToArray();
                Assert.That(transport.Length, Is.EqualTo(legacy ? 2 : 1));
                foreach (var parameter in transport) {
                    Assert.That(parameter.useCustomLerpDuration, Is.True, parameter.name);
                    Assert.That(parameter.customLerpDuration, Is.Zero, parameter.name);
                }
                Assert.That(originals.All(p => !p.useCustomLerpDuration), Is.True);

                // Declaring an animator parameter must not modify an existing expression entry either.
                var mode = originals.Single(p => p.valueType == VRCExpressionParameters.ValueType.Int);
                mode.useCustomLerpDuration = true;
                mode.customLerpDuration = 2;
                controller.GetOrCreateBool(mode.name);
                Assert.That(mode.useCustomLerpDuration, Is.True);
                Assert.That(mode.customLerpDuration, Is.EqualTo(2));
            } finally {
                Object.DestroyImmediate(root);
            }
        }

        [TestCase(VRCExpressionParameters.ValueType.Int)]
        [TestCase(VRCExpressionParameters.ValueType.Float)]
        public void InterpolationMergingRejectsConflictsWithoutChangingExistingDurations(VRCExpressionParameters.ValueType type) {
            var root = new GameObject("Interpolation merging");
            try {
                var paramz = GetExpressionParams(root);
                var original = new VRCExpressionParameters.Parameter { name = "Test", valueType = type };
                paramz.SetParameters(new[] { original });
                var authored = original.Clone();
                authored.useCustomLerpDuration = true;
                authored.customLerpDuration = 0.5f;
                Assert.That(original.IsSameAs(authored), Is.False);
                Assert.That(Assert.Throws<Exception>(() => paramz.AddOrMerge(authored)).Message,
                    Does.Contain("Test").And.Contain("interpolation"));
                Assert.That(original.useCustomLerpDuration, Is.False);
                Assert.That(original.customLerpDuration, Is.Zero);
                original.useCustomLerpDuration = true;
                original.customLerpDuration = 0.5f;
                paramz.AddOrMerge(authored);
                Assert.That(original.IsSameAs(authored), Is.True);
                Assert.That(authored.Clone().IsSameAs(authored), Is.True);
                Assert.That(Assert.Throws<Exception>(() => paramz.AddOrMerge(
                    new VRCExpressionParameters.Parameter { name = "Test", valueType = type })).Message,
                    Does.Contain("Test").And.Contain("interpolation"));
                Assert.That(original.useCustomLerpDuration, Is.True);
                Assert.That(original.customLerpDuration, Is.EqualTo(0.5f));
                var conflict = authored.Clone();
                conflict.DisableInterpolation();
                Assert.That(conflict.IsSameAs(authored), Is.False);
                Assert.That(Assert.Throws<Exception>(() => paramz.AddOrMerge(conflict)).Message,
                    Does.Contain("Test").And.Contain("interpolation"));
                Assert.That(original.customLerpDuration, Is.EqualTo(0.5f));
                original.useCustomLerpDuration = false;
                authored.useCustomLerpDuration = false;
                original.customLerpDuration = 5;
                Assert.That(original.IsSameAs(authored), Is.True, "Inactive durations do not affect behavior");
                paramz.AddOrMerge(authored);
                Assert.That(original.customLerpDuration, Is.EqualTo(5));
            } finally {
                Object.DestroyImmediate(root);
            }
        }
#endif

        [TestCase(VRCExpressionParameters.ValueType.Bool, 1)]
        [TestCase(VRCExpressionParameters.ValueType.Int, 8)]
        [TestCase(VRCExpressionParameters.ValueType.Float, 8)]
        public void DefaultParameterCosts(VRCExpressionParameters.ValueType type, int expected) {
            Assert.That(new VRCExpressionParameters.Parameter { valueType = type }.TypeCost(), Is.EqualTo(expected));
            Assert.That(new VRCExpressionParameters.Parameter { valueType = type }.HasStandardNetworkEncoding(), Is.True);
        }

#if VRCSDK_HAS_DYNAMIC_PARAMETER_BIT_WIDTH
        [TestCase(VRCExpressionParameters.ValueType.Int, 0, 2)]
        [TestCase(VRCExpressionParameters.ValueType.Int, 1, 2)]
        [TestCase(VRCExpressionParameters.ValueType.Int, 3, 3)]
        [TestCase(VRCExpressionParameters.ValueType.Int, 8, 8)]
        [TestCase(VRCExpressionParameters.ValueType.Int, 255, 8)]
        [TestCase(VRCExpressionParameters.ValueType.Float, 0, 2)]
        [TestCase(VRCExpressionParameters.ValueType.Float, 5, 5)]
        [TestCase(VRCExpressionParameters.ValueType.Float, 255, 8)]
        [TestCase(VRCExpressionParameters.ValueType.Bool, 8, 1)]
        public void DynamicWidthsUseSdkClamping(VRCExpressionParameters.ValueType type, int width, int expected) {
            Assert.That(new VRCExpressionParameters.Parameter {
                valueType = type, bitWidth = (byte)width
            }.TypeCost(), Is.EqualTo(expected));
        }

        [TestCase(VRCExpressionParameters.ValueType.Int, 3, false, false)]
        [TestCase(VRCExpressionParameters.ValueType.Int, 8, false, true)]
        [TestCase(VRCExpressionParameters.ValueType.Int, 8, true, false)]
        [TestCase(VRCExpressionParameters.ValueType.Int, 255, false, true)]
        [TestCase(VRCExpressionParameters.ValueType.Float, 5, false, false)]
        [TestCase(VRCExpressionParameters.ValueType.Float, 8, false, true)]
        [TestCase(VRCExpressionParameters.ValueType.Float, 8, true, false)]
        [TestCase(VRCExpressionParameters.ValueType.Bool, 3, true, true)]
        public void StandardEncodingUsesEffectiveWidthAndRepresentation(
            VRCExpressionParameters.ValueType type, int width, bool flipped, bool standard) {
            Assert.That(new VRCExpressionParameters.Parameter {
                valueType = type, bitWidth = (byte)width, flippedRepresentation = flipped
            }.HasStandardNetworkEncoding(), Is.EqualTo(standard));
        }

        [TestCase(VRCExpressionParameters.ValueType.Int, 3, false, false)]
        [TestCase(VRCExpressionParameters.ValueType.Float, 8, true, false)]
        [TestCase(VRCExpressionParameters.ValueType.Int, 8, true, false)]
        [TestCase(VRCExpressionParameters.ValueType.Int, 255, false, true)]
        [TestCase(VRCExpressionParameters.ValueType.Bool, 3, true, true)]
        public void EqualityAndMergeCheckNetworkEncoding(
            VRCExpressionParameters.ValueType type, int width, bool flipped, bool same) {
            var original = new VRCExpressionParameters.Parameter { name = "Test", valueType = type };
            var added = original.Clone();
            added.bitWidth = (byte)width;
            added.flippedRepresentation = flipped;
            Assert.That(original.IsSameAs(added), Is.EqualTo(same));
            var root = new GameObject("Network encoding merging");
            try {
                var paramz = GetExpressionParams(root);
                paramz.SetParameters(new[] { original });
                if (same) {
                    Assert.DoesNotThrow(() => paramz.AddOrMerge(added));
                } else {
                    var error = Assert.Throws<Exception>(() => paramz.AddOrMerge(added));
                    Assert.That(error.Message, Does.Contain("Test").And.Contain("bit width or signedness"));
                }
                Assert.That(paramz.Single(), Is.SameAs(original));
            } finally {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void ClonePreservesWidthAndRepresentation() {
            var original = new VRCExpressionParameters.Parameter {
                name = "Test", valueType = VRCExpressionParameters.ValueType.Int,
                bitWidth = 5, flippedRepresentation = true
            };
            var clone = original.Clone();
            Assert.That(clone, Is.Not.SameAs(original));
            Assert.That(clone.bitWidth, Is.EqualTo(5));
            Assert.That(clone.flippedRepresentation, Is.True);
            Assert.That(clone.IsSameAs(original), Is.True);
        }

        [Test]
        public void ParameterListsDetectEncodingOnlyChangesForPlatformAlignment() {
            var root = new GameObject("Parameter list equality");
            try {
                var desktop = GetExpressionParams(root);
                desktop.SetParameters(new[] { new VRCExpressionParameters.Parameter {
                    name = "Test", valueType = VRCExpressionParameters.ValueType.Int
                } });
                var mobile = desktop.GetSnapshot();
                Assert.That(desktop.IsSameAs(mobile), Is.True);
                mobile[0].bitWidth = 3;
                Assert.That(desktop.IsSameAs(mobile), Is.False);
                mobile[0].bitWidth = 8;
                mobile[0].flippedRepresentation = true;
                Assert.That(desktop.IsSameAs(mobile), Is.False);
            } finally {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void CompressorSolverExcludesNonstandardWidthsAndSignedness() {
            var obj = new GameObject("Avatar");
            var menu = ScriptableObject.CreateInstance<VRCExpressionsMenu>();
            var paramz = ScriptableObject.CreateInstance<VRCExpressionParameters>();
            try {
                var avatar = obj.AddComponent<VRCAvatarDescriptor>();
                avatar.customExpressions = true;
                avatar.expressionsMenu = menu;
                var parameters = new List<VRCExpressionParameters.Parameter>();
                var expected = new List<string>();
                foreach (var type in new[] { VRCExpressionParameters.ValueType.Int, VRCExpressionParameters.ValueType.Float }) {
                    foreach (var width in new byte[] { 3, 8 }) {
                        foreach (var flipped in new[] { false, true }) {
                            var p = new VRCExpressionParameters.Parameter {
                                name = type + "/" + width + "/" + flipped, valueType = type,
                                bitWidth = width, flippedRepresentation = flipped
                            };
                            parameters.Add(p);
                            if (width == 8 && !flipped) expected.Add(p.name);
                        }
                    }
                }
                parameters.Add(new VRCExpressionParameters.Parameter { name = "Bool", valueType = VRCExpressionParameters.ValueType.Bool });
                expected.Add("Bool");
                foreach (var p in parameters) p.SetNetworkSynced(true);
                paramz.parameters = parameters.ToArray();
                menu.controls = parameters.Select(p => new VRCExpressionsMenu.Control {
                    type = VRCExpressionsMenu.Control.ControlType.Toggle,
                    parameter = new VRCExpressionsMenu.Control.Parameter { name = p.name }
                }).ToList();
                var menuService = new MenuService();
                typeof(MenuService).VFField("avatar").SetValue(menuService, avatar);
                var solver = new ParameterCompressorSolverService();
                typeof(ParameterCompressorSolverService).VFField("menuService").SetValue(solver, menuService);
                var method = typeof(ParameterCompressorSolverService).GetMethods(BindingFlags.Instance | BindingFlags.NonPublic)
                    .Single(m => m.Name == "GetParamsToOptimize" && m.GetParameters().Length == 5);
                var decision = (OptimizationDecision)method.Invoke(solver, new object[] {
                    paramz.parameters, new HashSet<VRCExpressionsMenu.Control.ControlType> { VRCExpressionsMenu.Control.ControlType.Toggle },
                    new HashSet<string>(), 512, false
                });
                Assert.That(decision.compress.Select(p => p.name), Is.EquivalentTo(expected));
            } finally {
                Object.DestroyImmediate(obj);
                Object.DestroyImmediate(menu);
                Object.DestroyImmediate(paramz);
            }
        }
#endif
    }
}
