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

namespace VF.Tests {
    [Category("VRCFury")]
    public class SpsOptionsTest {
        [TestCase(false, false)]
        [TestCase(true, false)]
        [TestCase(false, true)]
        public void AnimationFilterEditorKeepsEachPairEnabled(bool plug, bool radius) {
            var root = new GameObject("Animation filters");
            try {
                UnityEngine.Component owner;
                if (plug) {
                    var component = root.AddComponent<VRCFuryHapticPlug>();
                    component.depthActions2.Add(new VRCFuryHapticSocket.DepthActionNew());
                    owner = component;
                } else {
                    var component = root.AddComponent<VRCFuryHapticSocket>();
                    component.depthActions2.Add(new VRCFuryHapticSocket.DepthActionNew());
                    component.plugEntryRadiusActions.Add(new VRCFuryHapticSocket.PlugEntryRadiusAction());
                    owner = component;
                }
                var serialized = new UnityEditor.SerializedObject(owner);
                var action = serialized.FindProperty(radius ? "plugEntryRadiusActions" : "depthActions2").GetArrayElementAtIndex(0);
                var editor = radius ? new VRCFuryHapticSocketEditor.PlugEntryRadiusActionDrawer().CreatePropertyGUI(action)
                    : new VRCFuryHapticSocketEditor.DepthActionDrawer().CreatePropertyGUI(action);
                var toggles = editor.Query<UnityEngine.UIElements.Toggle>().ToList();
                foreach (var (first, second) in new[] { ("Allow Self", "Allow Others"), ("Allow Hole", "Allow Ring") }) {
                    var a = toggles.SingleOrDefault(t => t.name == first);
                    var b = toggles.SingleOrDefault(t => t.name == second);
                    if (first != "Allow Self" && !plug) {
                        Assert.That(a, Is.Null);
                        Assert.That(b, Is.Null);
                        continue;
                    }
                    Assert.That(a.value && b.value, Is.True);
                    a.value = false;
                    b.value = false;
                    var field = first == "Allow Self" ? "enableSelf" : "allowHole";
                    Assert.That(action.FindPropertyRelative(field).boolValue, Is.True, "Disabling the last option must enable its partner");
                }
            } finally { Object.DestroyImmediate(root); }
        }

        [TestCase(false)]
        [TestCase(true)]
        public void CanHandleRejectsMissingOrUnsupportedSource(bool missing) {
            var root = new GameObject("Unsupported source");
            try {
                var component = root.AddComponent<VF.Model.VRCFury>();
                component.content = new FullController {
                    injectParams = { new FullController.InjectParamEntry {
                        sourceObject = missing ? null : root, sourceParam = VRCFuryHapticPlugEditor.SpsDepthMeters
                    } }
                };
                var serialized = new UnityEditor.SerializedObject(component);
                var inject = serialized.FindProperty("content").FindPropertyRelative("injectParams").GetArrayElementAtIndex(0);
                Assert.That(VRCFuryHapticPlugEditor.CanHandle(inject), Is.False);
                var editor = new VF.Feature.FullControllerBuilder.InjectParamDrawer().CreatePropertyGUI(inject);
                Assert.That(editor.Query<PopupField<string>>().ToList(), Is.Empty);
                Assert.That(editor.Q<TextField>("SourceParam")?.value, Is.EqualTo(VRCFuryHapticPlugEditor.SpsDepthMeters));
            } finally {
                Object.DestroyImmediate(root);
            }
        }

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

        [TestCase(false, null)]
        [TestCase(true, null)]
        [TestCase(false, VRCFuryHapticPlugEditor.SpsParamInteractingWithLegacy)]
        [TestCase(true, VRCFuryHapticPlugEditor.SpsParamInteractingWithLegacy)]
        [TestCase(false, VRCFuryHapticPlugEditor.SpsParamInteractingWithOthers)]
        [TestCase(true, VRCFuryHapticPlugEditor.SpsParamInteractingWithOthers)]
        [TestCase(false, VRCFuryHapticPlugEditor.SpsParamSocketIsHole)]
        [TestCase(true, VRCFuryHapticPlugEditor.SpsParamSocketIsHole)]
        public void InjectParametersOffersPlugEntryRadiusForSockets(bool socketSource, string unitlessParameter) {
            var unitless = unitlessParameter != null;
            var root = new GameObject("Inject parameters");
            try {
                if (socketSource) root.AddComponent<VRCFuryHapticSocket>();
                else root.AddComponent<VRCFuryHapticPlug>();
                var component = root.AddComponent<VF.Model.VRCFury>();
                var model = new FullController();
                model.injectParams.Add(new FullController.InjectParamEntry {
                    sourceObject = root, sourceParam = unitless ? VRCFuryHapticPlugEditor.GetSpsMagicParam(unitlessParameter, null)
                        : socketSource ? VRCFuryHapticPlugEditor.SpsPlugEntryRadiusLocal : VRCFuryHapticPlugEditor.SpsDepthMeters
                });
                component.content = model;
                var serialized = new UnityEditor.SerializedObject(component);
                var inject = serialized.FindProperty("content").FindPropertyRelative("injectParams").GetArrayElementAtIndex(0);
                var editor = new VF.Feature.FullControllerBuilder.InjectParamDrawer().CreatePropertyGUI(inject);
                var parameter = editor.Query<PopupField<string>>().ToList().Single(field => field.label == "Parameter");
                Assert.That(VRCFuryHapticPlugEditor.CanHandle(inject), Is.True);
                Assert.That(parameter.choices.Contains(VRCFuryHapticPlugEditor.SpsParamPlugEntryRadius), Is.EqualTo(socketSource));
                Assert.That(parameter.choices, Does.Contain(VRCFuryHapticPlugEditor.SpsParamInteractingWithLegacy));
                var metersWarning = editor.Q("spsMetersWarning");
                var selectedUnits = VRCFuryHapticPlugEditor.ParseSpsMagicParam(model.injectParams[0].sourceParam, socketSource)?.units;
                Assert.That(metersWarning.style.display.value, Is.EqualTo(selectedUnits == VRCFuryHapticPlugEditor.SpsUnitsMeters
                    ? DisplayStyle.Flex : DisplayStyle.None));
                if (unitless) {
                    Assert.That(parameter.value, Is.EqualTo(unitlessParameter));
                    var label = unitlessParameter switch {
                        VRCFuryHapticPlugEditor.SpsParamInteractingWithLegacy => socketSource ? "Plug Is Legacy" : "Socket Is Legacy",
                        VRCFuryHapticPlugEditor.SpsParamInteractingWithOthers => socketSource ? "Plug is Other Player" : "Socket is Other Player",
                        _ => unitlessParameter
                    };
                    Assert.That(parameter.formatSelectedValueCallback(parameter.value), Is.EqualTo(label));
                    Assert.That(parameter.formatListItemCallback(parameter.value), Is.EqualTo(label));
                    var units = editor.Query<PopupField<string>>().ToList().Single(field => field.label == "Units");
                    Assert.That(units.style.display.value, Is.EqualTo(DisplayStyle.None));
                    Assert.That(VRCFuryHapticPlugEditor.ParseSpsMagicParam(model.injectParams[0].sourceParam, socketSource)?.units, Is.Null);
                } else if (socketSource) {
                    Assert.That(parameter.value, Is.EqualTo(VRCFuryHapticPlugEditor.SpsParamPlugEntryRadius));
                    var units = editor.Query<PopupField<string>>().ToList().Single(field => field.label == "Units");
                    Assert.That(units.value, Is.EqualTo(VRCFuryHapticPlugEditor.SpsUnitsLocal));
                    Assert.That(units.choices, Is.EqualTo(VRCFuryHapticPlugEditor.SpsUnits));
                }
            } finally {
                Object.DestroyImmediate(root);
            }
        }

        [TestCase(false)]
        [TestCase(true)]
        public void PlugEntryRadiusEditorRendersSliderAndPreservesEditableRange(bool existingPlugUnits) {
            var root = new GameObject("Radius editor");
            try {
                root.transform.localScale = Vector3.one * 2;
                var socket = root.AddComponent<VRCFuryHapticSocket>();
                socket.plugEntryRadiusActions.Add(new VRCFuryHapticSocket.PlugEntryRadiusAction());
                if (existingPlugUnits) socket.plugEntryRadiusActions[0].units = VRCFuryHapticSocket.DepthActionUnits.Plugs;
                var serialized = new UnityEditor.SerializedObject(socket);
                var action = serialized.FindProperty("plugEntryRadiusActions").GetArrayElementAtIndex(0);
                var editor = new VRCFuryHapticSocketEditor.PlugEntryRadiusActionDrawer().CreatePropertyGUI(action);
                Assert.That(editor.Query<Label>().ToList().Any(label => label.text == "Reverse clip (Deprecated, Do not use)"), Is.False);
                var unitField = editor.Query<PopupField<string>>().ToList()
                    .Single(field => field.choices.SequenceEqual(new[] { "Local", "Meters" }));
                Assert.That(unitField.value, Is.EqualTo(existingPlugUnits ? "Plugs" : "Local"));
                Assert.That(socket.plugEntryRadiusActions[0].units, Is.EqualTo(existingPlugUnits
                    ? VRCFuryHapticSocket.DepthActionUnits.Plugs : VRCFuryHapticSocket.DepthActionUnits.Local));
                var control = editor.Q<DepthActionSlider>();
                Assert.That(control, Is.Not.Null);
                var slider = control.Q<MinMaxSlider>();
                Assert.That(slider, Is.Not.Null);
                Assert.That(slider.lowLimit, Is.Zero);
                control.SetValueWithoutNotify(new Vector2(0.02f, 0.5f));
                Assert.That(slider.value, Is.EqualTo(control.value));
                control.SetValueWithoutNotify(new Vector2(0.025f, 0.075f));
                Assert.That(slider.value, Is.EqualTo(control.value));
                Assert.That(control.Q<Label>("rangeMetersMin")?.text, Is.EqualTo(existingPlugUnits ? null : $"{0.05f:G4} m"));
                Assert.That(control.Q<Label>("rangeMetersMax")?.text, Is.EqualTo(existingPlugUnits ? null : $"{0.15f:G4} m"));
            } finally {
                Object.DestroyImmediate(root);
            }
        }

        [TestCase(false, false, false)]
        [TestCase(false, true, true)]
        [TestCase(true, false, false)]
        [TestCase(true, false, true)]
        [TestCase(true, true, false)]
        [TestCase(true, true, true)]
        public void ModesRespectAvailabilityDefaultsAndIndependentSaving(bool available, bool defaultOn, bool saved) {
            var root = new GameObject("SPS options");
            try {
                var avatar = root.AddComponent<VRCAvatarDescriptor>();
                avatar.baseAnimationLayers = new[] { new VRCAvatarDescriptor.CustomAnimLayer { type = VRCAvatarDescriptor.AnimLayerType.FX } };
                avatar.specialAnimationLayers = Array.Empty<VRCAvatarDescriptor.CustomAnimLayer>();
                var injector = VRCFuryInjectorBuilder.GetInjector(avatar);
                var options = new SpsOptions {
                    autoModeAvailable = available, autoModeDefaultOn = defaultOn, autoModeSaved = saved,
                    legacyModeAvailable = available, legacyModeEnabledOnAvatarLoad = defaultOn, legacyModeSaved = saved,
                    saveSockets = !saved
                };
                injector.GetService<GlobalsService>().allFeaturesInRun = new List<FeatureModel> { options };
                var controllers = injector.GetService<ControllersService>();
                controllers.ClearCache(applyBaseMask: false);
                var service = injector.GetService<SpsOptionsService>();
                var auto = service.GetAutoParameter();
                var legacy = service.GetLegacyParameter();
                if (!available) {
                    Assert.That(auto, Is.Null);
                    Assert.That(legacy, Is.Null);
                    Assert.That(service.GetLegacyOnClip(), Is.Null);
                    Assert.That(avatar.expressionParameters, Is.Null);
                    Assert.That(avatar.expressionsMenu, Is.Null);
                    return;
                }
                Assert.That(service.GetAutoParameter(), Is.SameAs(auto));
                Assert.That(service.GetLegacyParameter(), Is.SameAs(legacy));
                Assert.That(service.GetLegacyOnClip(), Is.Not.Null);
                foreach (var parameter in new[] { auto, legacy }) {
                    Assert.That(parameter.GetDefault() != 0, Is.EqualTo(defaultOn));
                    var expression = avatar.expressionParameters.parameters.Single(p => p.name == parameter.name);
                    Assert.That(expression.saved, Is.EqualTo(saved));
                    Assert.That(expression.defaultValue, Is.EqualTo(defaultOn ? 1 : 0));
                }
                Assert.That(avatar.expressionParameters.parameters, Has.Length.EqualTo(2));
            } finally {
                Object.DestroyImmediate(root);
            }
        }

        [TestCase(true)]
        [TestCase(false)]
        public void GlobalLegacyAvailabilityOverridesObsoleteSocketSetting(bool available) {
            if (!VF.Utils.BuildTargetUtils.IsDesktop()) Assert.Ignore("Socket lights are desktop-only.");
            var root = new GameObject("Socket legacy options");
            try {
                var socket = root.AddComponent<VRCFuryHapticSocket>();
                socket.addLight = VRCFuryHapticSocket.AddLight.Hole;
                socket.useLights = !available;
                var injector = new VRCFuryInjector();
                injector.ImportScan(typeof(VFServiceAttribute));
                var result = injector.GetService<VF.Inspector.VRCFuryHapticSocketBaker>().Bake(socket, legacyPlugCompatibility: available);
                Assert.That(result.legacyLights != null, Is.EqualTo(available));
            } finally {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void ModesAreAvailableByDefault() {
            var options = new SpsOptions();
            Assert.That(options.autoModeAvailable && options.legacyModeAvailable, Is.True);
            Assert.That(options.tpsDepthAnimationCompatibility, Is.False);
            Assert.That(options.legacySocketCompatibility, Is.True);
        }

        [TestCase(false, false)]
        [TestCase(false, true)]
        [TestCase(true, false)]
        [TestCase(true, true)]
        public void LegacySocketCompatibilityControlsLegacyPlugSenders(bool enabled, bool shared) {
            if (!BuildTargetUtils.IsDesktop()) Assert.Ignore("Contact senders are desktop-only.");
            var root = new GameObject("Legacy socket compatibility");
            try {
                var plug = root.AddComponent<VRCFuryHapticPlug>();
                plug.autoRenderer = plug.autoLength = plug.autoRadius = plug.enableSps = false;
                plug.unitsInMeters = true;
                plug.length = 0.4f;
                plug.radius = 0.03f;
                plug.useSharedTag = shared;
                plug.includeTags.Add(new VRCFuryHapticPlug.TagRule { tag = "custom" });
                var injector = new VRCFuryInjector();
                injector.ImportScan(typeof(VFServiceAttribute));
                var bake = injector.GetService<VRCFuryHapticPlugBaker>().Bake(plug, legacySocketCompatibility: enabled);
                var senders = bake.bakeRoot.GetComponentsInSelfAndChildren<VRCContactSender>();
                Assert.That(senders.Count, Is.EqualTo(2 + (enabled && shared ? 2 : 0)));
                var main = senders.Single(s => s.name == "Sender - Sphere");
                Assert.That(main.collisionTags.Contains(SpsTags.LegacyPlugSphere), Is.EqualTo(enabled && shared));
                Assert.That(main.collisionTags, Does.Contain(SpsTags.FormatPlug("custom")));
                Assert.That(main.collisionTags.Contains(SpsTags.FormatPlug(SpsTags.PublicChannel)), Is.EqualTo(shared));
                Assert.That(senders.Any(s => s.collisionTags.Contains(SpsTags.LegacyPlugWidth)), Is.EqualTo(enabled && shared));
                Assert.That(senders.Any(s => s.collisionTags.Contains(SpsTags.LegacyPlugRoot)), Is.EqualTo(enabled && shared));
                Assert.That(senders.Single(s => s.name == "Sender - Envelope").collisionTags, Does.Contain(SpsTags.PlugClose));
            } finally {
                Object.DestroyImmediate(root);
            }
        }

        [TestCase(false, false)]
        [TestCase(false, true)]
        [TestCase(true, false)]
        [TestCase(true, true)]
        public void TpsDepthCompatibilityRequiresLegacyPlugCompatibility(bool enabled, bool legacy) {
            if (!VF.Utils.BuildTargetUtils.IsDesktop()) Assert.Ignore("Contact senders are desktop-only.");
            var root = new GameObject("TPS depth compatibility");
            try {
                var socket = root.AddComponent<VRCFuryHapticSocket>();
                socket.addLight = VRCFuryHapticSocket.AddLight.Hole;
                var injector = new VRCFuryInjector();
                injector.ImportScan(typeof(VFServiceAttribute));
                var result = injector.GetService<VF.Inspector.VRCFuryHapticSocketBaker>().Bake(socket,
                    legacyPlugCompatibility: legacy, tpsDepthAnimationCompatibility: enabled);
                var fronts = result.bakeRoot.GetComponentsInSelfAndChildren<VRC.SDK3.Dynamics.Contact.Components.VRCContactSender>()
                    .Where(sender => sender.collisionTags.Contains(SpsTags.LegacySocketFront)).ToArray();
                Assert.That(fronts.Length, Is.EqualTo(enabled && legacy ? 1 : 0));
                Assert.That(result.legacyLights != null, Is.EqualTo(legacy));
                var rootSender = result.bakeRoot.GetComponentsInSelfAndChildren<VRCContactSender>()
                    .Single(sender => sender.name == "Sender - Root");
                Assert.That(rootSender.collisionTags.Contains(SpsTags.LegacySocket2), Is.EqualTo(legacy),
                    "SPS2 depth animations must not be triggered when legacy plug compatibility is unavailable");
                Assert.That(rootSender.collisionTags.Contains(SpsTags.LegacySocket), Is.EqualTo(legacy));
                Assert.That(rootSender.collisionTags, Does.Contain(SpsTags.FormatSocket(SpsTags.PublicChannel)));
                if (enabled && legacy) {
                    var front = fronts.Single();
                    Assert.That(front.radius, Is.EqualTo(0.001f).Within(0.00001f));
                    Assert.That(front.position, Is.EqualTo(Vector3.forward * 0.01f));
                    Assert.That(front.collisionTags, Does.Not.Contain(SpsTags.LegacySocketFront + "_SelfNotOnHips"));
                }
            } finally {
                Object.DestroyImmediate(root);
            }
        }
    }
}
