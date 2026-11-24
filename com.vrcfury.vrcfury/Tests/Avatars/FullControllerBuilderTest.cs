namespace VF.Tests {
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using NUnit.Framework;
    using UnityEngine;
    using UnityEngine.UIElements;
    using VF.Builder;
    using VF.Builder.Haptics;
    using VF.Component;
    using VF.Inspector;
    using VF.Feature.Base;
    using VF.Injector;
    using VF.Model.Feature;
    using VF.Service;
    using VF.Utils;
    using VRC.SDK3.Avatars.Components;
    using VRC.SDK3.Dynamics.Contact.Components;
    using Object = UnityEngine.Object;

    internal class FullControllerBuilderTest : SpsOptionsTestBase {
        [Test]
        public void EditingUnsupportedInjectedParameterPreservesFieldAndFocus() {
            var root = new GameObject("Inject parameters");
            var panelOwner = ScriptableObject.CreateInstance<PanelSettings>();
            IPanel panel = null;
            try {
                root.AddComponent<VRCFuryHapticSocket>();
                var component = root.AddComponent<VF.Model.VRCFury>();
                component.content = new FullController {
                    injectParams = { new FullController.InjectParamEntry { sourceObject = root, sourceParam = "custom" } }
                };
                var serialized = new UnityEditor.SerializedObject(component);
                var inject = serialized.FindProperty("content").FindPropertyRelative("injectParams").GetArrayElementAtIndex(0);
                var sourceParam = inject.FindPropertyRelative("sourceParam");
                var editor = new VF.Feature.FullControllerBuilder.InjectParamDrawer().CreatePropertyGUI(inject);
                var panelType = typeof(VisualElement).Assembly.GetType("UnityEngine.UIElements.Panel");
                var flags = System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic;
                panel = (IPanel)panelType.GetMethod("CreateEditorPanel", flags | System.Reflection.BindingFlags.Static)
                    .Invoke(null, new object[] { panelOwner });
                var update = panelType.GetMethod("UpdateBindings", flags | System.Reflection.BindingFlags.Instance);
                var updateData = panelType.GetMethod("UpdateDataBinding", flags | System.Reflection.BindingFlags.Instance);
                var scheduler = panelType.GetProperty("scheduler", flags | System.Reflection.BindingFlags.Instance).GetValue(panel);
                var tick = scheduler.GetType().GetMethod("UpdateScheduledEvents", flags | System.Reflection.BindingFlags.Instance);
                void Pump() {
                    for (var i = 0; i < 3; i++) {
                        System.Threading.Thread.Sleep(110);
                        update.Invoke(panel, null);
                        updateData?.Invoke(panel, null);
                        tick.Invoke(scheduler, null);
                    }
                }
                panel.visualTree.Add(editor);
                UnityEditor.UIElements.BindingExtensions.Bind(editor, serialized);
                Pump();
                UnityEditor.UIElements.BindingExtensions.Unbind(editor);
                UnityEditor.UIElements.BindingExtensions.Bind(editor, serialized);
                Pump();
                TextField SourceField() => editor.Q<TextField>("SourceParam");
                var field = SourceField();
                field.Focus();
                var focused = panel.focusController.focusedElement;
                Assert.That(focused, Is.Not.Null);
                foreach (var value in new[] { "custom1", "custom12", "custom123" }) {
                    field.value = value;
                    Pump();
                    Assert.That(sourceParam.stringValue, Is.EqualTo(value));
                    Assert.That(SourceField(), Is.SameAs(field));
                    Assert.That(panel.focusController.focusedElement, Is.SameAs(focused));
                }
                field.value = VRCFuryHapticPlugEditor.SpsDepthLocal;
                Pump();
                Assert.That(editor.Query<PopupField<string>>().ToList(), Has.Count.EqualTo(2));
                sourceParam.stringValue = "unknown";
                serialized.ApplyModifiedProperties();
                Pump();
                UnityEditor.Undo.IncrementCurrentGroup();
                SourceField().value = "";
                // Advancing the group must not split the edit and its automatic default selection.
                UnityEditor.Undo.IncrementCurrentGroup();
                Pump();
                Assert.That(sourceParam.stringValue, Is.EqualTo(VRCFuryHapticPlugEditor.SpsDepthMeters));
                Assert.That(editor.Query<PopupField<string>>().ToList(), Has.Count.EqualTo(2));
                UnityEditor.Undo.FlushUndoRecordObjects();
                UnityEditor.Undo.PerformUndo();
                serialized.Update();
                Pump();
                Assert.That(sourceParam.stringValue, Is.EqualTo("unknown"), "Clearing and selecting the default must be one undo operation");
                UnityEditor.Undo.PerformRedo();
                serialized.Update();
                Pump();
                Assert.That(sourceParam.stringValue, Is.EqualTo(VRCFuryHapticPlugEditor.SpsDepthMeters));
                var plug = GameObjects.Create("Plug", root).AddComponent<VRCFuryHapticPlug>();
                sourceParam.stringValue = VRCFuryHapticPlugEditor.SpsPlugEntryRadiusLocal;
                serialized.ApplyModifiedProperties();
                Pump();
                UnityEditor.Undo.IncrementCurrentGroup();
                editor.Q<UnityEditor.UIElements.ObjectField>("SourceObject").value = plug.gameObject;
                Pump();
                Assert.That(sourceParam.stringValue, Is.EqualTo(VRCFuryHapticPlugEditor.SpsDepthMeters));
                UnityEditor.Undo.FlushUndoRecordObjects();
                UnityEditor.Undo.PerformUndo();
                serialized.Update();
                Pump();
                Assert.That(inject.FindPropertyRelative("sourceObject").objectReferenceValue, Is.SameAs(root));
                Assert.That(sourceParam.stringValue, Is.EqualTo(VRCFuryHapticPlugEditor.SpsPlugEntryRadiusLocal),
                    "Changing the source and selecting a supported default must be one undo operation");
                sourceParam.stringValue = VRCFuryHapticPlugEditor.SpsDepthLocal;
                serialized.ApplyModifiedProperties();
                Pump();
                editor.Q<UnityEditor.UIElements.ObjectField>("SourceObject").value = plug.gameObject;
                Pump();
                Assert.That(sourceParam.stringValue, Is.EqualTo(VRCFuryHapticPlugEditor.SpsDepthLocal),
                    "Changing sources must preserve a supported parameter and its units");
                var unitsField = editor.Query<PopupField<string>>().ToList().Single(field => field.label == "Units");
                UnityEditor.Undo.IncrementCurrentGroup();
                unitsField.value = VRCFuryHapticPlugEditor.SpsUnitsMeters;
                Pump();
                Assert.That(editor.Q("spsMetersWarning").style.display.value, Is.EqualTo(DisplayStyle.Flex));
                UnityEditor.Undo.FlushUndoRecordObjects();
                UnityEditor.Undo.PerformUndo();
                serialized.Update();
                Pump();
                Assert.That(sourceParam.stringValue, Is.EqualTo(VRCFuryHapticPlugEditor.SpsDepthLocal));
                Assert.That(unitsField.value, Is.EqualTo(VRCFuryHapticPlugEditor.SpsUnitsLocal), "Units must follow Undo");
                Assert.That(editor.Q("spsMetersWarning").style.display.value, Is.EqualTo(DisplayStyle.None));
                var parameterField = editor.Query<PopupField<string>>().ToList().Single(field => field.label == "Parameter");
                UnityEditor.Undo.IncrementCurrentGroup();
                parameterField.value = VRCFuryHapticPlugEditor.SpsParamInteractingWithLegacy;
                Pump();
                Assert.That(unitsField.style.display.value, Is.EqualTo(DisplayStyle.None));
                UnityEditor.Undo.FlushUndoRecordObjects();
                UnityEditor.Undo.PerformUndo();
                serialized.Update();
                Pump();
                Assert.That(parameterField.value, Is.EqualTo(VRCFuryHapticPlugEditor.SpsParamDepth), "Parameter selection must follow Undo");
                Assert.That(unitsField.value, Is.EqualTo(VRCFuryHapticPlugEditor.SpsUnitsLocal));
                Assert.That(unitsField.style.display.value, Is.EqualTo(DisplayStyle.Flex));
                UnityEditor.Undo.PerformRedo();
                serialized.Update();
                Pump();
                Assert.That(parameterField.value, Is.EqualTo(VRCFuryHapticPlugEditor.SpsParamInteractingWithLegacy));
                Assert.That(unitsField.style.display.value, Is.EqualTo(DisplayStyle.None));
            } finally {
                (panel as IDisposable)?.Dispose();
                Object.DestroyImmediate(panelOwner);
                Object.DestroyImmediate(root);
            }
        }

        [TestCase(true, "__sps_unknown_meters")]
        [TestCase(true, "__sps_depth_unknown")]
        [TestCase(true, "")]
        [TestCase(false, VRCFuryHapticPlugEditor.SpsPlugEntryRadiusLocal)]
        public void UnsupportedInjectedParametersUseTextFieldWithoutChangingValue(bool socketSource, string sourceParam) {
            var root = new GameObject("Inject parameters");
            try {
                if (socketSource) root.AddComponent<VRCFuryHapticSocket>();
                else root.AddComponent<VRCFuryHapticPlug>();
                var component = root.AddComponent<VF.Model.VRCFury>();
                component.content = new FullController {
                    injectParams = { new FullController.InjectParamEntry { sourceObject = root, sourceParam = sourceParam } }
                };
                var serialized = new UnityEditor.SerializedObject(component);
                var inject = serialized.FindProperty("content").FindPropertyRelative("injectParams").GetArrayElementAtIndex(0);
                var editor = new VF.Feature.FullControllerBuilder.InjectParamDrawer().CreatePropertyGUI(inject);
                Assert.That(editor.Query<PopupField<string>>().ToList(), Is.Empty);
                Assert.That(VRCFuryHapticPlugEditor.RenderSpsInjectParamEditor(inject), Is.Null);
                Assert.That(VRCFuryHapticPlugEditor.CanHandle(inject), Is.False);
                Assert.That(editor.Q<TextField>("SourceParam")?.value, Is.EqualTo(sourceParam));
                Assert.That(inject.FindPropertyRelative("sourceParam").stringValue, Is.EqualTo(sourceParam));
            } finally {
                Object.DestroyImmediate(root);
            }
        }
    }
}

namespace VF.Tests {
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using NUnit.Framework;
    using UnityEngine;
    using VF.Feature;
    using VF.Model.Feature;
    using VF.Service;
    using VF.Utils;
    using VRC.SDK3.Avatars.Components;
    using VRC.SDK3.Avatars.ScriptableObjects;
    using VRC.SDK3.Dynamics.Contact.Components;
#if VRCSDK_HAS_CONTACT_PARAMETER_FLAGS
    using AdvancedParameter = VRC.Dynamics.ContactReceiver.AdvancedParameter;
#endif
    using Object = UnityEngine.Object;

    internal class FullControllerContactParametersTest : ContactReceiverTestBase {
        [TestCaseSource(nameof(Outputs))]
        public void FullControllerRenamesReceiverWhenOnlyOneOutputIsUsed(string suffix) =>
            CheckFullControllerReceiver(suffix, null);

        [TestCase(true)]
        [TestCase(false)]
        public void FullControllerReceiverUsesObjectScopeRegardlessOfSync(bool synced) =>
            CheckFullControllerReceiver("_PointSeparation", synced);

        private static void CheckFullControllerReceiver(string suffix, bool? synced) {
            var root = new GameObject("Avatar");
            var asset = ScriptableObject.CreateInstance<VRCExpressionParameters>();
            try {
                var avatar = root.AddComponent<VRCAvatarDescriptor>();
                avatar.baseAnimationLayers = Array.Empty<VRCAvatarDescriptor.CustomAnimLayer>();
                avatar.specialAnimationLayers = Array.Empty<VRCAvatarDescriptor.CustomAnimLayer>();
                var receiver = root.AddComponent<VRCContactReceiver>();
                receiver.parameter = "Contact";
                receiver.collisionTags = new List<string> { "Test_Tag" };
                if (suffix != "") EnableAdvanced(receiver);

                var globals = new GlobalsService { avatarObject = root, currentFeatureNum = 7 };
                var parameters = new ExpressionParamsService();
                Inject(parameters, "avatar", avatar);
                Inject(parameters, "globals", globals);
                var controllers = new ControllersService();
                Inject(controllers, "avatar", avatar);
                Inject(controllers, "globals", globals);
                var uniqueParams = new UniqueParamService();
                Inject(controllers, "uniqueParams", new Lazy<UniqueParamService>(() => uniqueParams));
                Inject(controllers, "expressionParams", parameters);
                Inject(uniqueParams, "controllers", controllers);
                Inject(uniqueParams, "expressionParams", parameters);
                Inject(uniqueParams, "avatarObject", (VFGameObject)root);
                Inject(uniqueParams, "pipelineId", new PipelineIdService(root));
                var builder = new FullControllerBuilder { featureBaseObject = root, model = new FullController() };
                if (synced.HasValue) {
                    asset.parameters = new[] { ExpressionParam.Create("Contact" + suffix).Float().Synced(synced.Value) };
                    builder.model.prms.Add(new FullController.ParamsEntry { parameters = asset });
                    Inject(builder, "expressionParams", parameters);
                }
                Inject(builder, "avatar", avatar);
                Inject(builder, "globals", globals);
                Inject(builder, "controllers", controllers);
                Inject(builder, "uniqueParams", uniqueParams);
                Inject(builder, "animatorLayerControlManager", new AnimatorLayerControlOffsetService());
#if VRCSDK_HAS_FACE_TRACKING
                var faceTracking = new FaceTrackingService();
                Inject(faceTracking, "avatar", avatar);
                Inject(faceTracking, "controllers", controllers);
                Inject(builder, "faceTracking", faceTracking);
#endif
                var rewrite = builder.GetType().VFMethod("RewriteParamName");
                var output = (string)rewrite.Invoke(builder, new object[] { "Contact" + suffix });
                var unrelated = (string)rewrite.Invoke(builder, new object[] { "Camera_Angle" });
                Assert.That(unrelated, Is.EqualTo("VF_sGqeWWch_Camera_Angle"));
                builder.Apply();
                Assert.That(receiver.parameter, Is.EqualTo("VF_sGqeWWch_Contact"));
                Assert.That(output, Is.EqualTo(receiver.parameter + suffix));
            } finally {
                Object.DestroyImmediate(asset);
                Object.DestroyImmediate(root);
            }
        }
    }
}

namespace VF.Tests {

    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;
    using NUnit.Framework;
    using UnityEngine;
    using VF.Exceptions;
    using VF.Feature;
    using VF.Feature.Base;
    using VF.Model.Feature;
    using VF.Service;
    using VF.Utils;
    using VF.Utils.Controller;
    using VRC.SDK3.Avatars.Components;

    public class FullControllerBaseLayersTest {
        [TestCase("UserEyeTracking")]
        [TestCase("UserFaceTracking")]
        public void TrackingAvailabilityParametersAreNotRenamedWhenImported(string name) {
            var builder = new FullControllerBuilder { model = new FullController() };
            var rewritten = typeof(FullControllerBuilder).VFMethod("RewriteParamName").Invoke(builder, new object[] { name });
            Assert.That(rewritten, Is.EqualTo(name));
        }

        private static readonly VRCAvatarDescriptor.AnimLayerType[] LocomotionTypes = {
            VRCAvatarDescriptor.AnimLayerType.Base, VRCAvatarDescriptor.AnimLayerType.TPose,
            VRCAvatarDescriptor.AnimLayerType.IKPose, VRCAvatarDescriptor.AnimLayerType.Sitting
        };
        private class Build {
            public readonly GlobalsService globals = new GlobalsService {
                allBuildersInRun = new List<FeatureBuilder>()
            };
            private readonly LayerSourceService sources = new LayerSourceService();
            public readonly VFAvatarController controller;

            public Build(VRCAvatarDescriptor.AnimLayerType type = VRCAvatarDescriptor.AnimLayerType.Base,
                string originalOwner = LayerSourceService.AvatarDescriptorSource) {
                typeof(LayerSourceService).VFField("globals").SetValue(sources, globals);
                controller = new VFAvatarController(VFController.Create(), type, () => 0, null, sources);
                foreach (var name in new[] { "AvatarA", "AvatarB" }) {
                    var layer = controller.NewLayer(name);
                    layer.NewState(name);
                    sources.SetSource(layer, originalOwner);
                }
            }

            public void Merge(string name, bool overwrite) {
                globals.currentFeatureName = name;
                var builder = new FullControllerBuilder {
                    model = new FullController { overwriteLocomotion = overwrite }
                };
                typeof(FullControllerBuilder).VFField("globals").SetValue(builder, globals);
                typeof(FullControllerBuilder).VFField("layerSourceService").SetValue(builder, sources);
                globals.allBuildersInRun.Add(builder);
                var from = VFController.Create();
                from.NewLayer(name + "A").NewState(name + "A");
                from.NewLayer(name + "B").NewState(name + "B");
                typeof(FullControllerBuilder).VFMethod("Merge").Invoke(builder, new object[] { from, controller });
            }

            public string[] States => controller.layers.Select(layer => layer.allStates.Single().name).ToArray();
        }

        [Test]
        public void OverwriteDefaultsOnWithoutAComponentVersionChange() {
            var model = JsonUtility.FromJson<FullController>("{}");
            Assert.That(model.overwriteLocomotion, Is.True);
            Assert.That(model.GetLatestVersion(), Is.EqualTo(4));
        }

        [Test]
        public void OptionControlsWhetherDescriptorLayersSurvive(
            [Values] bool overwrite,
            [Values(LayerSourceService.AvatarDescriptorSource, LayerSourceService.VrcDefaultSource)] string originalOwner,
            [ValueSource(nameof(LocomotionTypes))] VRCAvatarDescriptor.AnimLayerType type) {
            var build = new Build(type, originalOwner);
            build.Merge("Imported", overwrite);
            Assert.That(build.States, Is.EqualTo(overwrite
                ? new[] { "ImportedA", "ImportedB" }
                : new[] { "AvatarA", "AvatarB", "ImportedA", "ImportedB" }));
        }

        [Test]
        public void OverwritingLayersPrecedeAllAppendedLayersRegardlessOfBuildOrder(
            [Values] bool overwriteFirst,
            [ValueSource(nameof(LocomotionTypes))] VRCAvatarDescriptor.AnimLayerType type) {
            var build = new Build(type);
            if (overwriteFirst) build.Merge("Replacement", true);
            build.Merge("Append1", false);
            if (!overwriteFirst) build.Merge("Replacement", true);
            build.Merge("Append2", false);
            Assert.That(build.States, Is.EqualTo(new[] {
                "ReplacementA", "ReplacementB", "Append1A", "Append1B", "Append2A", "Append2B"
            }));
        }

        [Test]
        public void MultipleAppendControllersPreserveTheDescriptorAndEachOther(
            [ValueSource(nameof(LocomotionTypes))] VRCAvatarDescriptor.AnimLayerType type) {
            var build = new Build(type);
            build.Merge("Append1", false);
            build.Merge("Append2", false);
            Assert.That(build.States, Is.EqualTo(new[] {
                "AvatarA", "AvatarB", "Append1A", "Append1B", "Append2A", "Append2B"
            }));
        }

        [Test]
        public void MultipleOverwritingControllersStillConflict(
            [ValueSource(nameof(LocomotionTypes))] VRCAvatarDescriptor.AnimLayerType type) {
            var build = new Build(type);
            build.Merge("First", true);
            var exception = Assert.Throws<TargetInvocationException>(() => build.Merge("Second", true));
            Assert.That(exception.InnerException, Is.InstanceOf<VRCFBuilderException>());
            Assert.That(exception.InnerException.Message, Does.Contain("multiple locomotion implementations"));
        }

        [Test]
        public void OptionDoesNotChangeOtherControllerTypes(
            [Values] bool overwrite,
            [Values(VRCAvatarDescriptor.AnimLayerType.FX, VRCAvatarDescriptor.AnimLayerType.Gesture,
                VRCAvatarDescriptor.AnimLayerType.Additive)] VRCAvatarDescriptor.AnimLayerType type) {
            var build = new Build(type);
            build.Merge("Imported", overwrite);
            Assert.That(build.States, Is.EqualTo(new[] { "AvatarA", "AvatarB", "ImportedA", "ImportedB" }));
        }
    }
}

namespace VF.Tests {

    using System;
    using NUnit.Framework;
    using UnityEditor.Animations;
    using UnityEngine;
    using VF.Builder;
    using VF.Feature;
    using VF.Model.Feature;
    using VF.Service;
    using VF.Utils;
    using VRC.SDK3.Avatars.Components;
    using VRC.SDK3.Avatars.ScriptableObjects;
    using VRC.SDK3.Dynamics.PhysBone.Components;
    using Object = UnityEngine.Object;

    public class FullControllerGlobalParametersTest {
        [TestCase(false, false)]
        [TestCase(true, false)]
        [TestCase(false, true)]
        public void DeprecatedFlagActsAsGlobalWildcard(bool deprecatedFlag, bool wildcard) {
            var root = new GameObject("Avatar");
            var raw = new AnimatorController();
            var expressionParameters = ScriptableObject.CreateInstance<VRCExpressionParameters>();
            try {
                var avatar = root.AddComponent<VRCAvatarDescriptor>();
                avatar.customizeAnimationLayers = true;
                avatar.baseAnimationLayers = new[] {
                    new VRCAvatarDescriptor.CustomAnimLayer { type = VRCAvatarDescriptor.AnimLayerType.FX }
                };
                avatar.specialAnimationLayers = Array.Empty<VRCAvatarDescriptor.CustomAnimLayer>();
                var owner = GameObjects.Create("Shirt", root);
                var physbone = owner.AddComponent<VRCPhysBone>();
                physbone.parameter = "shirt";
                raw.AddParameter("shirt_Stretch", AnimatorControllerParameterType.Float);
                raw.AddParameter("Private", AnimatorControllerParameterType.Bool);
                expressionParameters.parameters = new[] { ExpressionParam.Create("Synced").Bool() };

                var injector = VRCFuryInjectorBuilder.GetInjector(avatar);
                injector.Set("componentObject", new Func<VFGameObject>(() => owner));
                var controllers = injector.GetService<ControllersService>();
                controllers.ClearCache(applyBaseMask: false);
                var builder = injector.CreateAndFillObject<FullControllerBuilder>();
                builder.featureBaseObject = owner;
                builder.model = new FullController {
                    controllers = { new FullController.ControllerEntry { controller = raw } },
                    prms = { new FullController.ParamsEntry { parameters = expressionParameters } },
                    globalParams = { "shirt_Stretch", "!Private" }
                };
                builder.model.allNonsyncedAreGlobal = deprecatedFlag;
                if (wildcard) builder.model.globalParams.Add("*");

                var allGlobal = deprecatedFlag || wildcard;
                var privateSyncedParams = allGlobal ? Array.Empty<string>() : new[] { "Synced" };
                Assert.That(builder.GetPrivateSyncedParameters(), Is.EqualTo(privateSyncedParams));
                builder.Apply();

                Assert.That(physbone.parameter == "shirt", Is.EqualTo(allGlobal));
                Assert.That(controllers.GetFx().TryGetParam("shirt_Stretch", out _), Is.True);
                Assert.That(controllers.GetFx().TryGetParam("Private", out _), Is.False);
                Assert.That(injector.GetService<ExpressionParamsService>().Contains("Synced"), Is.EqualTo(allGlobal));
            } finally {
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(raw);
                Object.DestroyImmediate(expressionParameters);
            }
        }
    }
}

#if VRCSDK_HAS_FACE_TRACKING
namespace VF.Tests {
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;
    using NUnit.Framework;
    using UnityEditor;
    using UnityEngine;
    using UnityEngine.TestTools;
    using UnityEngine.UIElements;
    using VF.Feature;
    using VF.Builder;
    using VF.Feature.Base;
    using VF.Inspector;
    using VF.Model;
    using VF.Model.Feature;
    using VF.Model.StateAction;
    using VF.Service;
    using VF.Utils;
    using VF.Utils.Controller;
    using VRC.SDK3.Avatars.Components;
    using FtBehaviourControl = VRC.SDKBase.VRC_AnimatorFaceTrackingBlendshapesSet.BlendSet;
    using Toggle = VF.Model.Feature.Toggle;

    internal class FullControllerFaceTrackingImportTest : FaceTrackingTestBase {
        [Test]
        public void FullControllerFaceTrackingFoldoutIsAboveAdvancedOptions() {
            var owner = ScriptableObject.CreateInstance<AuthoringFields>();
            try {
                using (var serialized = new SerializedObject(owner)) {
                    var editor = FullControllerBuilder.Editor(serialized.FindProperty("controller"), null, null, owner.controller);
                    var foldouts = editor.Children().OfType<Foldout>().ToArray();
                    Assert.That(foldouts.Select(foldout => foldout.text), Is.EqualTo(new[] { "Face Tracking", "Advanced Options" }));
                    Assert.That(foldouts[0].Q<FaceTrackingEditor.BlendshapeSetsEditor>(), Is.Not.Null);
                    Assert.That(foldouts[1].Q<FaceTrackingEditor.BlendshapeSetsEditor>(), Is.Null);
                }
            } finally {
                Object.DestroyImmediate(owner);
            }
        }

        [TestCase(false)]
        [TestCase(true)]
        public void FullControllerUsesOnlyExplicitFaceTrackingMeshes(bool empty) {
            using (var build = new Build()) {
                var owner = new GameObject("Controller");
                owner.transform.SetParent(build.root.transform);
                var unlisted = build.Skin("UnlistedChild");
                unlisted.transform.SetParent(owner.transform);
                var selected = build.Skin("ExplicitSibling");
                var behavior = new VRCAvatarDescriptor.FaceTrackingParameterBehavior {
                    targetMode = VRCAvatarDescriptor.ParameterBehaviorTargetMode.ApplyToBlendshape,
                    targetBlendshapeName = "Smile"
                };
                build.Full(owner, skins: empty ? new SkinnedMeshRenderer[0] : new[] { selected, selected },
                    behaviors: new[] { behavior });
                build.Run();
                Assert.That(build.avatar.faceTrackingMeshes,
                    Is.EquivalentTo(empty ? new SkinnedMeshRenderer[0] : new[] { selected }));
                Assert.That(unlisted.HasBlendshape("Smile"), Is.True);
                Assert.That(unlisted.sharedMesh.blendShapeCount, Is.EqualTo(1));
                if (empty) {
                    Assert.That(selected.HasBlendshape("Smile"), Is.True);
                    Assert.That(build.avatar.faceTrackingParameterBehaviors, Is.Empty);
                } else {
                    var name = build.avatar.faceTrackingBlendshapesSetList.Single()
                        .faceTrackingBlendshapeNames[(int)VRCAvatarDescriptor.FaceTrackingKey.JawOpen];
                    Assert.That(selected.HasBlendshape(name), Is.True);
                    Assert.That(unlisted.HasBlendshape(name), Is.False);
                    Assert.That(build.avatar.faceTrackingParameterBehaviors.Single().targetBlendshapeName, Is.EqualTo(name));
                }
            }
        }

        [Test]
        public void FullControllerResolvesFaceTrackingPathsFromAvatarNotPrefab() {
            using (var build = new Build()) {
                var prefab = build.Skin("Prefab");
                var body = build.Skin("Body");
                build.Full(prefab.gameObject, path: "Body");
                build.Run();
                Assert.That(build.avatar.faceTrackingMeshes, Is.EquivalentTo(new[] { body }));
                Assert.That(prefab.HasBlendshape("Smile"), Is.True);
            }
        }

        [TestCase("Missing")]
        [TestCase("")]
        public void FullControllerRejectsInvalidFaceTrackingMeshPaths(string path) {
            using (var build = new Build()) {
                Assert.Throws<VF.Exceptions.VRCFBuilderException>(() => build.Full(build.root, path: path));
            }
        }

        [Test]
        public void FullControllerRejectsMissingFaceTrackingMeshReferences() {
            using (var build = new Build()) {
                var error = Assert.Throws<VF.Exceptions.VRCFBuilderException>(() =>
                    build.Full(build.root, skins: new SkinnedMeshRenderer[] { null }));
                Assert.That(error.Message, Does.Contain("Face Tracking mesh could not be resolved: Object reference is missing."));
            }
        }

        [TestCase(false)]
        [TestCase(true)]
        public void FullControllerRejectsNullOrUnsetFaceTrackingMeshEntries(bool unset) {
            using (var build = new Build()) {
                var builder = build.Full(build.root, skins: System.Array.Empty<SkinnedMeshRenderer>(), includeSet: false);
                builder.model.faceTrackingMeshes.Add(unset ? new ObjectReference<SkinnedMeshRenderer> { useBone = false, useObj = true } : null);
                var error = Assert.Throws<VF.Exceptions.VRCFBuilderException>(() => builder.Apply());
                Assert.That(error.Message, Does.Contain("Object reference is missing"));
            }
        }

        [Test]
        public void FullControllerRejectsDestroyedFaceTrackingMesh() {
            using (var build = new Build()) {
                var skin = build.Skin("Destroyed");
                Object.DestroyImmediate(skin);
                Assert.Throws<VF.Exceptions.VRCFBuilderException>(() => build.Full(build.root, skins: new[] { skin }));
            }
        }

        [TestCase(false)]
        [TestCase(true)]
        public void FullControllerRejectsFaceTrackingMeshesConfiguredOnDescriptor(bool enabled) {
            using (var build = new Build()) {
                var skin = build.Skin("BaseMesh");
                build.avatar.enableFaceTracking = enabled;
                build.NativeMesh(skin);
                var error = Assert.Throws<VF.Exceptions.VRCFBuilderException>(() => build.Full(skin.gameObject));
                Assert.That(error.Message, Does.Contain("BaseMesh").And.Contain("already configured on the avatar descriptor"));
            }
        }

        [TestCase(false)]
        [TestCase(true)]
        public void FullControllerReservesFaceTrackingMeshesEvenWithoutSetsOrBehaviors(bool usePath) {
            using (var build = new Build()) {
                var skin = build.Skin("Reserved");
                build.Full(skin.gameObject, includeSet: false);
                Assert.That(build.avatar.enableFaceTracking, Is.False);
                var error = Assert.Throws<VF.Exceptions.VRCFBuilderException>(() => {
                    if (usePath) build.Full(build.root, path: "Reserved");
                    else build.Full(skin.gameObject);
                });
                Assert.That(error.Message, Does.Contain("Reserved").And.Contain("already configured in another Full Controller"));
            }
        }

        [TestCase(false)]
        [TestCase(true)]
        public void FailedFullControllerMeshReservationDoesNotClaimOtherMeshes(bool conflictFirst) {
            using (var build = new Build()) {
                var claimed = build.Skin("Claimed");
                var available = build.Skin("Available");
                build.Full(claimed.gameObject);
                Assert.Throws<VF.Exceptions.VRCFBuilderException>(() => build.Full(build.root,
                    skins: conflictFirst ? new[] { claimed, available } : new[] { available, claimed }));
                Assert.DoesNotThrow(() => build.Full(available.gameObject));
                build.Run();
                Assert.That(build.avatar.faceTrackingMeshes, Is.EquivalentTo(new[] { claimed, available }));
            }
        }

        [Test]
        public void FullControllerRejectsMeshesAlreadyClaimedByOtherFullControllers() {
            using (var build = new Build()) {
                var left = build.Skin("Left");
                var right = build.Skin("Right");
                build.avatar.enableFaceTracking = true;
                build.Full(left.gameObject);
                build.Full(right.gameObject);
                var error = Assert.Throws<VF.Exceptions.VRCFBuilderException>(() => build.Full(build.root, setWeight: 0.35f, skins: new[] { left, right }, behaviors: new[] {
                    new VRCAvatarDescriptor.FaceTrackingParameterBehavior {
                        targetMode = VRCAvatarDescriptor.ParameterBehaviorTargetMode.ApplyBlendSetWeight,
                        targetBlendSetName = "Face"
                    }
                }));
                Assert.That(error.Message, Does.Contain("Left").And.Contain("already configured in another Full Controller"));
                build.Run();
                var sets = build.avatar.faceTrackingBlendshapesSetList;
                var leftName = sets.Single(set => set.setName == "VF1_Face").faceTrackingBlendshapeNames[(int)VRCAvatarDescriptor.FaceTrackingKey.JawOpen];
                var rightName = sets.Single(set => set.setName == "VF2_Face").faceTrackingBlendshapeNames[(int)VRCAvatarDescriptor.FaceTrackingKey.JawOpen];
                Assert.That(left.HasBlendshape(leftName), Is.True);
                Assert.That(right.HasBlendshape(leftName), Is.False);
                Assert.That(right.HasBlendshape(rightName), Is.True);
                Assert.That(left.HasBlendshape(rightName), Is.False);
                Assert.That(sets, Has.Count.EqualTo(2));
                Assert.That(build.avatar.faceTrackingParameterBehaviors, Is.Empty);
            }
        }

        [Test]
        public void FaceTrackingWeightParameterIsRewrittenAsAnInput() {
            var raw = ScriptableObject.CreateInstance<VRCAnimatorFaceTrackingBlendshapesSet>();
            try {
                raw.blendSets = new List<FtBehaviourControl> {
                    new FtBehaviourControl {
                        type = VRC.SDKBase.VRC_AnimatorFaceTrackingBlendshapesSet.SourceType.Parameter,
                        sourceParameter = "Weight", blendshapesSetName = "Face"
                    }
                };
                var behavior = new VF.Utils.Controller.VFBehaviour(raw);
                behavior.RewriteParameters(name => "VF_" + name, includeWrites: false);
                var rewritten = behavior.Read<VRCAnimatorFaceTrackingBlendshapesSet>().blendSets.Single();
                Assert.That(rewritten.sourceParameter, Is.EqualTo("VF_Weight"));
                Assert.That(rewritten.blendshapesSetName, Is.EqualTo("Face"));
                Object.DestroyImmediate(behavior.Read<VRCAnimatorFaceTrackingBlendshapesSet>());
            } finally {
                Object.DestroyImmediate(raw);
            }
        }
    }
}
#endif

namespace VF.Tests {
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using NUnit.Framework;
    using UnityEditor;
    using UnityEditor.UIElements;
    using UnityEngine;
    using UnityEngine.UIElements;
    using VF.Builder;
    using VF.Feature;
    using VF.Inspector;
    using VF.Model;
    using VF.Model.Feature;
    using VF.Service;
    using VF.Utils;
    using Object = UnityEngine.Object;

    internal class FullControllerReferenceResolutionTest : ObjectReferenceTestBase {
#if VRCSDK_HAS_FACE_TRACKING
        [Test]
        public void FullControllerResolvesFaceTrackingMeshFromCachedBoneAndOffset() {
            var root = new GameObject("Avatar");
            var target = new GameObject("Mesh");
            target.transform.SetParent(root.transform);
            var mesh = new Mesh { vertices = new[] { Vector3.zero } };
            var skin = target.AddComponent<SkinnedMeshRenderer>();
            mesh.AddBlendShapeFrame("Smile", 100, new[] { Vector3.up }, null, null);
            skin.sharedMesh = mesh;
            Avatar rig = null;
            try {
                var (humanoid, _, hips) = HumanoidTestRig.Create(root);
                rig = humanoid;
                target.transform.SetParent(hips, false);
                root.AddComponent<Animator>().avatar = rig;
                var avatar = root.AddComponent<VRC.SDK3.Avatars.Components.VRCAvatarDescriptor>();
                avatar.baseAnimationLayers = Array.Empty<VRC.SDK3.Avatars.Components.VRCAvatarDescriptor.CustomAnimLayer>();
                avatar.specialAnimationLayers = Array.Empty<VRC.SDK3.Avatars.Components.VRCAvatarDescriptor.CustomAnimLayer>();
                var globals = new GlobalsService { avatarObject = root };
                var controllers = new ControllersService();
                var ft = new FaceTrackingService();
                void Inject(object obj, string field, object value) => obj.GetType().VFField(field).SetValue(obj, value);
                Inject(controllers, "avatar", avatar);
                Inject(ft, "avatar", avatar);
                Inject(ft, "controllers", controllers);
                var set = FaceTrackingUtils.CreateBlendshapeSet("Face");
                set.faceTrackingBlendshapeNames[(int)VRC.SDK3.Avatars.Components.VRCAvatarDescriptor.FaceTrackingKey.JawOpen] = "Smile";
                var builder = new FullControllerBuilder {
                    featureBaseObject = root,
                    model = new FullController {
                        faceTrackingMeshes = { new ObjectReference<SkinnedMeshRenderer> { offset = "Mesh" } },
                        faceTrackingBlendshapeSets = { set }
                    }
                };
                Inject(builder, "globals", globals);
                Inject(builder, "avatar", avatar);
                Inject(builder, "controllers", controllers);
                Inject(builder, "faceTracking", ft);
                Inject(builder, "armatureCache", VRCFArmatureCache.GetPerFrame(root));
                Inject(builder, "animatorLayerControlManager", new AnimatorLayerControlOffsetService());
                builder.Apply();
                ft.Apply();
                Assert.That(avatar.faceTrackingMeshes, Is.EqualTo(new[] { skin }));
                Assert.That(avatar.faceTrackingBlendshapesSetList.Single().faceTrackingBlendshapeNames[
                    (int)VRC.SDK3.Avatars.Components.VRCAvatarDescriptor.FaceTrackingKey.JawOpen],
                    Is.EqualTo(skin.sharedMesh.GetBlendShapeName(0)));
            } finally {
                if (skin.sharedMesh != mesh) Object.DestroyImmediate(skin.sharedMesh);
                Object.DestroyImmediate(mesh);
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(rig);
            }
        }
#endif
    }
}
