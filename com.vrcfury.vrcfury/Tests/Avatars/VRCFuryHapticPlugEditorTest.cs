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

    internal class VRCFuryHapticPlugEditorTest : SpsOptionsTestBase {
        [TestCase(false)]
        [TestCase(true)]
        public void ChannelEditorUsesSuppliedSettingsWithoutAPlugOrSocketComponent(bool isPlug) {
            var root = new GameObject("Supplied channel settings");
            try {
                var component = root.AddComponent<VF.Model.VRCFury>();
                component.content = new VF.Model.Feature.Toggle {
                    state = new VF.Model.State {
                        actions = { new VF.Model.StateAction.ChangeSpsChannelsAction {
                            channelSettings = new VF.Model.SpsChannelSettings {
                                useSharedTag = false,
                                channels = { new VF.Model.SpsChannelSettings.Channel { id = "custom" } }
                            }
                        } }
                    }
                };
                var settings = new UnityEditor.SerializedObject(component).FindProperty("content")
                    .FindPropertyRelative("state").FindPropertyRelative("actions").GetArrayElementAtIndex(0)
                    .FindPropertyRelative("channelSettings");
                var editor = VRCFuryHapticPlugEditor.SpsChannelsEditor(root, isPlug, settings);
                Assert.That(editor.Query<Label>().ToList().Single(l => l.text.StartsWith("Final channel list:")).text,
                    Is.EqualTo("Final channel list:\ncustom"));
            } finally { Object.DestroyImmediate(root); }
        }

        [TestCase(false, false)]
        [TestCase(false, true)]
        [TestCase(true, false)]
        [TestCase(true, true)]
        public void ChannelPreviewIncludesCalculatedChannelsAndActionOverrides(bool socket, bool action) {
            var root = new GameObject("Channel preview");
            try {
                var injector = VRCFuryPerFrameInjector.GetPerFrameInjector(root);
                injector.Set("autoChannelGenerator", new ChannelPreviewBones());
                UnityEngine.Component target;
                if (socket) target = root.AddComponent<VRCFuryHapticSocket>();
                else {
                    var plug = root.AddComponent<VRCFuryHapticPlug>();
                    plug.channelSettings.channels.Add(new VF.Model.SpsChannelSettings.Channel { id = "selfonly", allowOthers = false });
                    plug.channelSettings.channels.Add(new VF.Model.SpsChannelSettings.Channel { id = "othersonly", allowSelf = false });
                    target = plug;
                }
                VisualElement preview;
                if (action) {
                    var component = root.AddComponent<VF.Model.VRCFury>();
                    component.content = new VF.Model.Feature.Toggle {
                        state = new VF.Model.State {
                            actions = { new VF.Model.StateAction.ChangeSpsChannelsAction {
                                target = root, channelSettings = new VF.Model.SpsChannelSettings {
                                    useSharedTag = false, channels = { new VF.Model.SpsChannelSettings.Channel { id = "override", allowOthers = false } }
                                }
                            } }
                        }
                    };
                    var serialized = new UnityEditor.SerializedObject(component);
                    var prop = serialized.FindProperty("content").FindPropertyRelative("state")
                        .FindPropertyRelative("actions").GetArrayElementAtIndex(0);
                    preview = VF.Actions.ChangeSpsChannelsActionBuilder.Editor(prop);
                } else preview = VRCFuryHapticPlugEditor.SpsChannelsEditor(target.owner(), !socket, new UnityEditor.SerializedObject(target).FindProperty("channelSettings"));
                var text = preview.Query<Label>().ToList().Single(l => l.text.StartsWith("Final channel list:")).text;
                Assert.That(text, Is.EqualTo(action
                    ? "Final channel list:\noverride (Self)"
                    : socket ? "Final channel list:\n_public\nhead\nnothips (Self)"
                    : "Final channel list:\n_public\nothersonly (Others)\nselfonly (Self)"));
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
    }
}

namespace VF.Tests {
    using System;
    using System.Collections.Generic;
    using NUnit.Framework;
    using UnityEditor;
    using UnityEngine;
    using VF.Builder.Haptics;
    using VF.Component;
    using VF.Utils;
    using VRC.SDK3.Avatars.Components;
    using Object = UnityEngine.Object;

    internal class VRCFuryHapticPlugEditorGizmoTest {
        [Test]
        public void GizmoUpgradesLegacyPrefabUnitsAndSizingWithoutChangingThePrefab() {
            var path = "Assets/PlugGizmoTest-" + Guid.NewGuid().ToString("N") + ".prefab";
            var obj = new GameObject("Legacy plug");
            GameObject instance = null;
            try {
                var plug = obj.AddComponent<VRCFuryHapticPlug>();
                plug.Version = 0;
                plug.length = 0.3f;
                plug.radius = 0.05f;
                obj.transform.localScale = Vector3.one * 2;
                var prefab = PrefabUtility.SaveAsPrefabAsset(obj, path);
                instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                var original = instance.GetComponent<VRCFuryHapticPlug>();
                var before = EditorJsonUtility.ToJson(original);
                var size = VF.Inspector.VRCFuryHapticPlugEditor.GetGizmoSize(original, out var error);
                Assert.That(error, Is.Null);
                Assert.That(size.worldLength, Is.EqualTo(0.3f));
                Assert.That(size.worldRadius, Is.EqualTo(0.05f));
                for (var i = 0; i < 100; i++) {
                    Assert.That(VF.Inspector.VRCFuryHapticPlugEditor.GetGizmoSize(original, out _), Is.SameAs(size));
                }
                Assert.That(EditorJsonUtility.ToJson(original), Is.EqualTo(before));
                Assert.That(PrefabUtility.HasPrefabInstanceAnyOverrides(instance, false), Is.False);

                instance.transform.localScale = Vector3.one * 4;
                var scaled = VF.Inspector.VRCFuryHapticPlugEditor.GetGizmoSize(original, out error);
                Assert.That(error, Is.Null);
                Assert.That(scaled, Is.Not.SameAs(size));
                Assert.That(scaled.worldLength, Is.EqualTo(0.3f), "Legacy dimensions are in meters");
                Assert.That(scaled.worldRadius, Is.EqualTo(0.05f));
                Assert.That(original.Version, Is.EqualTo(0));
            } finally {
                if (instance != null) Object.DestroyImmediate(instance);
                Object.DestroyImmediate(obj);
                AssetDatabase.DeleteAsset(path);
            }
        }

        [Test]
        public void GizmoSizeStaysCachedUntilInputsChange() {
            var obj = new GameObject("Plug");
            try {
                var plug = obj.AddComponent<VRCFuryHapticPlug>();
                plug.enableSps = false;
                plug.autoRenderer = false;
                plug.autoLength = plug.autoRadius = false;
                plug.length = 0.3f;
                plug.radius = 0.05f;
                var size = VF.Inspector.VRCFuryHapticPlugEditor.GetGizmoSize(plug, out var error);
                Assert.That(error, Is.Null);
                Assert.That(size.worldLength, Is.EqualTo(0.3f));
                for (var i = 0; i < 100; i++) {
                    VF.Utils.EditorChanges.Changed(transformsOnly: true);
                    Assert.That(VF.Inspector.VRCFuryHapticPlugEditor.GetGizmoSize(plug, out _), Is.SameAs(size));
                }
                plug.length = 0.4f;
                VF.Utils.EditorChanges.Changed();
                var changed = VF.Inspector.VRCFuryHapticPlugEditor.GetGizmoSize(plug, out _);
                Assert.That(changed, Is.Not.SameAs(size));
                Assert.That(changed.worldLength, Is.EqualTo(0.4f));
                obj.transform.localScale = Vector3.one * 2;
                var scaled = VF.Inspector.VRCFuryHapticPlugEditor.GetGizmoSize(plug, out _);
                Assert.That(scaled.worldLength, Is.EqualTo(0.8f));
                Assert.That(scaled.worldRadius, Is.EqualTo(0.1f));
            } finally {
                Object.DestroyImmediate(obj);
            }
        }

        [TestCase(false)]
        [TestCase(true)]
        public void GizmoTracksRendererAndBoneMovementButIgnoresUnrelatedTransforms(bool skinned) {
            var root = new GameObject("Avatar");
            var obj = new GameObject("Plug");
            var mesh = new Mesh {
                vertices = new[] {
                    new Vector3(-.02f, 0, .1f),
                    new Vector3(.02f, 0, .1f),
                    new Vector3(0, .02f, .2f)
                },
                triangles = new[] { 0, 1, 2 }
            };
            var renderObject = new GameObject("Renderer");
            var bone = new GameObject("Bone");
            var unrelated = new GameObject("Unrelated");
            try {
                obj.transform.SetParent(root.transform, false);
                renderObject.transform.SetParent(root.transform, false);
                bone.transform.SetParent(root.transform, false);
                Renderer renderer;
                if (skinned) {
                    var skin = renderObject.AddComponent<SkinnedMeshRenderer>();
                    mesh.bindposes = new[] { Matrix4x4.identity };
                    mesh.boneWeights = new[] {
                        new BoneWeight { boneIndex0 = 0, weight0 = 1 },
                        new BoneWeight { boneIndex0 = 0, weight0 = 1 },
                        new BoneWeight { boneIndex0 = 0, weight0 = 1 }
                    };
                    skin.bones = new[] { bone.transform };
                    skin.sharedMesh = mesh;
                    renderer = skin;
                } else {
                    renderObject.AddComponent<MeshFilter>().sharedMesh = mesh;
                    renderer = renderObject.AddComponent<MeshRenderer>();
                }
                var plug = obj.AddComponent<VRCFuryHapticPlug>();
                plug.autoRenderer = false;
                plug.useBoneMask = false;
                plug.configureTpsMesh.Add(renderer);
                var size = VF.Inspector.VRCFuryHapticPlugEditor.GetGizmoSize(plug, out var error, includeRadiusCurve: true);
                Assert.That(error, Is.Null);
                Assert.That(size.worldLength, Is.EqualTo(.2f).Within(.00001f));
                root.transform.SetPositionAndRotation(Vector3.one, Quaternion.Euler(20, 30, 40));
                EditorChanges.Changed(transformsOnly: true);
                Assert.That(VF.Inspector.VRCFuryHapticPlugEditor.GetGizmoSize(plug, out _), Is.SameAs(size),
                    "Moving the avatar rigidly must not remeasure its mesh");
                root.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                if (skinned) {
                    renderObject.transform.position = Vector3.left;
                    EditorChanges.Changed(transformsOnly: true);
                    Assert.That(VF.Inspector.VRCFuryHapticPlugEditor.GetGizmoSize(plug, out _), Is.SameAs(size),
                        "Moving the renderer object does not move its weighted bones");
                    renderObject.transform.position = Vector3.zero;
                }
                unrelated.transform.position = Vector3.one;
                EditorChanges.Changed(transformsOnly: true);
                Assert.That(VF.Inspector.VRCFuryHapticPlugEditor.GetGizmoSize(plug, out _), Is.SameAs(size));

                // Moving just the marker must reuse the posed mesh but recompute its local measurements.
                obj.transform.position = Vector3.forward * .05f;
                EditorChanges.Changed(transformsOnly: true);
                var shifted = VF.Inspector.VRCFuryHapticPlugEditor.GetGizmoSize(plug, out error);
                Assert.That(error, Is.Null);
                Assert.That(shifted.worldLength, Is.EqualTo(.15f).Within(.00001f));

                (skinned ? bone : renderObject).transform.position = Vector3.forward * .1f;
                EditorChanges.Changed(transformsOnly: true);
                var moved = VF.Inspector.VRCFuryHapticPlugEditor.GetGizmoSize(plug, out error);
                Assert.That(error, Is.Null);
                Assert.That(moved, Is.Not.SameAs(size));
                Assert.That(moved.worldLength, Is.EqualTo(.25f).Within(.00001f));

                var editedVertices = mesh.vertices;
                editedVertices[2].z = .4f;
                mesh.vertices = editedVertices;
                EditorChanges.Changed();
                var edited = VF.Inspector.VRCFuryHapticPlugEditor.GetGizmoSize(plug, out error);
                Assert.That(error, Is.Null);
                Assert.That(edited.worldLength, Is.EqualTo(.45f).Within(.00001f),
                    "Editing mesh data must invalidate the cached pose as well as the measurements");
            } finally {
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(obj);
                Object.DestroyImmediate(renderObject);
                Object.DestroyImmediate(bone);
                Object.DestroyImmediate(unrelated);
                Object.DestroyImmediate(mesh);
            }
        }
    }
}
