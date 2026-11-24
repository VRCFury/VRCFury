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

    internal class FaceTrackingEditorTest : FaceTrackingTestBase {
        [TestCase("set")]
        [TestCase("behavior")]
        public void FullControllerEditorOverridesSavedAuthoringAvatar(string field) {
            using (var build = new Build()) {
                var skin = build.Skin("Source");
                var owner = ScriptableObject.CreateInstance<AuthoringFields>();
                owner.meshes.Add((ObjectReference<SkinnedMeshRenderer>)skin);
                try {
                    using (var serialized = new SerializedObject(owner)) {
                        var property = serialized.FindProperty(field);
                        var context = VRC.SDK3A.Editor.FaceTrackingEditorContext.ForProperty(property);
                        context.SourceAvatar = build.avatar;
                        PropertyDrawer drawer = field == "set"
                            ? (PropertyDrawer)new VRC.SDK3A.Editor.FaceTrackingBlendshapesSetDrawer()
                            : new VRC.SDK3A.Editor.FaceTrackingParameterBehaviorDrawer();
                        FaceTrackingEditor.Field(property, () => drawer, serialized.FindProperty("meshes"));
                        Assert.That(context.SourceAvatar, Is.Null);
                    }
                } finally {
                    Object.DestroyImmediate(owner);
                }
            }
        }

        [TestCase(false)]
        [TestCase(true)]
        public void FullControllerClipboardRoundTripsSdkConfigurations(bool behaviors) {
            using (var build = new Build()) {
                build.NativeMesh(build.Skin("Source"));
                var set = FaceTrackingUtils.CreateBlendshapeSet("Pasted");
                SetMapping(set, (int)VRCAvatarDescriptor.FaceTrackingKey.JawOpen, "Smile", multiplier: 0.4f);
                SetMapping(set, 1, "MouthOpen", visemes: true, multiplier: 0.8f);
                var behavior = new VRCAvatarDescriptor.FaceTrackingParameterBehavior {
                    targetMode = VRCAvatarDescriptor.ParameterBehaviorTargetMode.ApplyToBlendshape,
                    outputParameterName = "", targetBlendSetName = "",
                    targetBlendshapeName = "Smile", weightBlendSetName = "Pasted",
                    combineMode = VRCAvatarDescriptor.ParameterBehaviorCombineMode.Custom,
                    customExpression = "max(Voice, Viseme_aa)",
                    inputTrackingKeys = new[] { 0, 1 }, inputTrackingKeyNames = new[] { "Voice", "Viseme_aa" }, inputInverted = new[] { true, false }
                };
                var component = build.root.AddComponent<VRCFury>();
                component.content = new FullController {
                    faceTrackingBlendshapeSets = { FaceTrackingUtils.CreateBlendshapeSet("Existing") },
                    faceTrackingParameterBehaviors = { new VRCAvatarDescriptor.FaceTrackingParameterBehavior { outputParameterName = "Existing" } }
                };
                using (var serialized = new SerializedObject(component)) {
                    var list = serialized.FindProperty("content." + (behaviors ? "faceTrackingParameterBehaviors" : "faceTrackingBlendshapeSets"));
                    list.GetArrayElementAtIndex(0).FindPropertyRelative(behaviors ? "outputParameterName" : "setName").stringValue = "Pending edit";
                    var existing = FaceTrackingEditor.Serialize(list, behaviors);
                    Assert.That(existing, Does.Contain("Pending edit"));
                    var model = (FullController)component.content;
                    Assert.That(behaviors ? model.faceTrackingParameterBehaviors[0].outputParameterName
                        : model.faceTrackingBlendshapeSets[0].setName, Is.EqualTo("Existing"));
                    var json = behaviors
                        ? VRC.SDK3A.Editor.FaceTrackingClipboard.SerializeParameterBehaviors(new[] { behavior, behavior }, build.avatar)
                        : VRC.SDK3A.Editor.FaceTrackingClipboard.SerializeBlendshapesSets(new[] { set, set }, build.avatar);
                    Assert.That(FaceTrackingEditor.Paste(list, behaviors, json, out var error), Is.True, error);
                    Assert.That(list.arraySize, Is.EqualTo(3));
                    Assert.That(FaceTrackingEditor.Serialize(list.GetArrayElementAtIndex(0), behaviors), Is.EqualTo(existing));
                    var expected = behaviors
                        ? VRC.SDK3A.Editor.FaceTrackingClipboard.SerializeParameterBehaviors(new[] { behavior })
                        : VRC.SDK3A.Editor.FaceTrackingClipboard.SerializeBlendshapesSets(new[] { set });
                    for (var i = 1; i < list.arraySize; i++) {
                        var entry = list.GetArrayElementAtIndex(i);
                        Assert.That(FaceTrackingEditor.Serialize(entry, behaviors), Is.EqualTo(expected));
                        Assert.That(VRC.SDK3A.Editor.FaceTrackingEditorContext.ForProperty(entry).SourceAvatar, Is.Null);
                    }
                    var copied = FaceTrackingEditor.Serialize(list, behaviors);
                    if (behaviors) {
                        Assert.That(VRC.SDK3A.Editor.FaceTrackingClipboard.TryParseParameterBehaviors(copied, out var parsed, out var sources, out error), Is.True, error);
                        Assert.That(parsed, Has.Count.EqualTo(3));
                        Assert.That(sources.All(string.IsNullOrEmpty), Is.True);
                    } else {
                        Assert.That(VRC.SDK3A.Editor.FaceTrackingClipboard.TryParseBlendshapesSets(copied, out var parsed, out var sources, out error), Is.True, error);
                        Assert.That(parsed, Has.Count.EqualTo(3));
                        Assert.That(sources.All(string.IsNullOrEmpty), Is.True);
                    }
                    Assert.That(((FullController)component.content).faceTrackingMeshes, Is.Empty);
                }
            }
        }

        [Test]
        public void FaceTrackingFieldStopsDrawingDeletedBehaviorAndCanRebindAfterReadding() {
            var owner = ScriptableObject.CreateInstance<AuthoringFields>();
            try {
                owner.controller.faceTrackingParameterBehaviors.Add(new VRCAvatarDescriptor.FaceTrackingParameterBehavior {
                    outputParameterName = "Original", inputTrackingKeys = new[] { 0 }, inputTrackingKeyNames = new[] { "Voice" }
                });
                using (var serialized = new SerializedObject(owner)) {
                    var list = serialized.FindProperty("controller.faceTrackingParameterBehaviors");
                    MeasuringBehaviorDrawer drawer = null;
                    var creations = 0;
                    var field = (IMGUIContainer)FaceTrackingEditor.Field(list.GetArrayElementAtIndex(0), () => {
                        creations++;
                        return drawer = new MeasuringBehaviorDrawer();
                    }, serialized.FindProperty("meshes"));
                    Assert.Throws<MeasurementCompleteException>(() => field.onGUIHandler());
                    Assert.That(drawer.output, Is.EqualTo("Original"));
                    list.DeleteArrayElementAtIndex(0);
                    serialized.ApplyModifiedProperties();
                    Assert.DoesNotThrow(() => field.onGUIHandler());
                    Assert.DoesNotThrow(() => field.onGUIHandler());
                    list.arraySize = 1;
                    list.GetArrayElementAtIndex(0).boxedValue = new VRCAvatarDescriptor.FaceTrackingParameterBehavior {
                        outputParameterName = "Replacement", inputTrackingKeys = new[] { 0 }, inputTrackingKeyNames = new[] { "Voice" }
                    };
                    serialized.ApplyModifiedProperties();
                    Assert.Throws<MeasurementCompleteException>(() => field.onGUIHandler());
                    Assert.That(drawer.output, Is.EqualTo("Replacement"));
                    Assert.That(creations, Is.EqualTo(2));
                }
            } finally {
                Object.DestroyImmediate(owner);
            }
        }

        [TestCase(0)]
        [TestCase(1)]
        public void FaceTrackingFieldRebindsSurvivingBehaviorWhenArrayShrinks(int removed) {
            var owner = ScriptableObject.CreateInstance<AuthoringFields>();
            try {
                owner.controller.faceTrackingParameterBehaviors = Enumerable.Range(0, 2).Select(i =>
                    new VRCAvatarDescriptor.FaceTrackingParameterBehavior {
                        outputParameterName = "Behavior" + i,
                        inputTrackingKeys = Enumerable.Repeat(0, i + 1).ToArray(),
                        inputTrackingKeyNames = Enumerable.Repeat("Voice", i + 1).ToArray()
                    }).ToList();
                using (var serialized = new SerializedObject(owner)) {
                    var list = serialized.FindProperty("controller.faceTrackingParameterBehaviors");
                    var drawers = new List<MeasuringBehaviorDrawer>();
                    var fields = Enumerable.Range(0, 2).Select(i => (IMGUIContainer)FaceTrackingEditor.Field(
                        list.GetArrayElementAtIndex(i), () => {
                            var drawer = new MeasuringBehaviorDrawer();
                            drawers.Add(drawer);
                            return drawer;
                        }, serialized.FindProperty("meshes"))).ToArray();
                    foreach (var field in fields) Assert.Throws<MeasurementCompleteException>(() => field.onGUIHandler());
                    list.DeleteArrayElementAtIndex(removed);
                    serialized.ApplyModifiedProperties();
                    Assert.Throws<MeasurementCompleteException>(() => fields[0].onGUIHandler());
                    Assert.That(drawers.Last().output, Is.EqualTo("Behavior" + (1 - removed)));
                    Assert.That(drawers, Has.Count.EqualTo(3), "The surviving row needs fresh SDK bindings after resizing.");
                    Assert.DoesNotThrow(() => fields[1].onGUIHandler());
                }
            } finally {
                Object.DestroyImmediate(owner);
            }
        }

        [Test]
        [TestCase("empty")]
        [TestCase("meshes")]
        [TestCase("sets")]
        [TestCase("behaviors")]
        public void FaceTrackingFoldoutStartsExpandedWhenConfigured(string configured) {
            var owner = ScriptableObject.CreateInstance<AuthoringFields>();
            try {
                if (configured == "meshes") owner.controller.faceTrackingMeshes.Add(new ObjectReference<SkinnedMeshRenderer>());
                if (configured == "sets") owner.controller.faceTrackingBlendshapeSets.Add(FaceTrackingUtils.CreateBlendshapeSet("Face"));
                if (configured == "behaviors") owner.controller.faceTrackingParameterBehaviors.Add(new VRCAvatarDescriptor.FaceTrackingParameterBehavior());
                using (var serialized = new SerializedObject(owner)) {
                    var editor = (Foldout)FaceTrackingEditor.Create(
                        serialized.FindProperty("controller.faceTrackingMeshes"),
                        serialized.FindProperty("controller.faceTrackingBlendshapeSets"),
                        serialized.FindProperty("controller.faceTrackingParameterBehaviors"));
                    Assert.That(editor.value, Is.EqualTo(configured != "empty"));
                }
            } finally {
                Object.DestroyImmediate(owner);
            }
        }

        [Test]
        public void FaceTrackingSetSelectorShowsOnlySelectedSetAndDoesNotRecreateItOnRename() {
            var owner = ScriptableObject.CreateInstance<AuthoringFields>();
            try {
                owner.controller.faceTrackingBlendshapeSets.AddRange(new[] {
                    FaceTrackingUtils.CreateBlendshapeSet("First"), FaceTrackingUtils.CreateBlendshapeSet("Second")
                });
                using (var serialized = new SerializedObject(owner)) {
                    var sets = serialized.FindProperty("controller.faceTrackingBlendshapeSets");
                    var editor = new FaceTrackingEditor.BlendshapeSetsEditor(sets, serialized.FindProperty("meshes"));
                    var selector = editor.Q<DropdownField>();
                    Assert.That(selector.choices, Is.EqualTo(new[] { "1: First", "2: Second" }));
                    Assert.That(editor.Query<IMGUIContainer>().ToList(), Has.Count.EqualTo(1));
                    editor.Select(1);
                    Assert.That(editor.SelectedIndex, Is.EqualTo(1));
                    Assert.That(selector.value, Is.EqualTo("2: Second"));
                    var field = editor.Q<IMGUIContainer>();
                    sets.GetArrayElementAtIndex(1).FindPropertyRelative("setName").stringValue = "Renamed";
                    serialized.ApplyModifiedProperties();
                    editor.Refresh();
                    Assert.That(selector.value, Is.EqualTo("2: Renamed"));
                    Assert.That(editor.Q<IMGUIContainer>(), Is.SameAs(field));
                    Assert.That(editor.Query<IMGUIContainer>().ToList(), Has.Count.EqualTo(1));
                }
            } finally {
                Object.DestroyImmediate(owner);
            }
        }

        [Test]
        public void FaceTrackingSetSelectorAllowsEmptyListsAndAddsUniqueSets() {
            var owner = ScriptableObject.CreateInstance<AuthoringFields>();
            try {
                using (var serialized = new SerializedObject(owner)) {
                    var sets = serialized.FindProperty("controller.faceTrackingBlendshapeSets");
                    var editor = new FaceTrackingEditor.BlendshapeSetsEditor(sets, serialized.FindProperty("meshes"));
                    Assert.That(sets.arraySize, Is.Zero);
                    Assert.That(editor.Q<DropdownField>().enabledSelf, Is.False);
                    Assert.That(editor.Query<IMGUIContainer>().ToList(), Is.Empty);
                    editor.AddSet();
                    editor.AddSet();
                    Assert.That(sets.arraySize, Is.EqualTo(2));
                    Assert.That(editor.SelectedIndex, Is.EqualTo(1));
                    var names = owner.controller.faceTrackingBlendshapeSets.Select(set => set.setName).ToArray();
                    Assert.That(names.Distinct().Count(), Is.EqualTo(2));
                    Assert.That(names.All(name => !string.IsNullOrEmpty(name)), Is.True);
                    editor.Move(-1);
                    Assert.That(editor.SelectedIndex, Is.Zero);
                    Assert.That(owner.controller.faceTrackingBlendshapeSets.Select(set => set.setName), Is.EqualTo(names.Reverse()));
                    editor.RemoveSet();
                    Assert.That(editor.SelectedIndex, Is.Zero);
                    editor.RemoveSet();
                    Assert.That(sets.arraySize, Is.Zero);
                    Assert.That(editor.Query<IMGUIContainer>().ToList(), Is.Empty);
                    editor.RemoveSet();
                    editor.Move(1);
                    Assert.That(sets.arraySize, Is.Zero);
                }
            } finally {
                Object.DestroyImmediate(owner);
            }
        }

        [TestCase("add")]
        [TestCase("remove")]
        [TestCase("move")]
        public void FaceTrackingSetSelectorOperationsSupportUndoRedo(string operation) {
            var owner = ScriptableObject.CreateInstance<AuthoringFields>();
            try {
                owner.controller.faceTrackingBlendshapeSets.AddRange(new[] {
                    FaceTrackingUtils.CreateBlendshapeSet("First"), FaceTrackingUtils.CreateBlendshapeSet("Second")
                });
                using (var serialized = new SerializedObject(owner)) {
                    var sets = serialized.FindProperty("controller.faceTrackingBlendshapeSets");
                    var editor = new FaceTrackingEditor.BlendshapeSetsEditor(sets, serialized.FindProperty("meshes"));
                    var before = FaceTrackingEditor.Serialize(sets, false);
                    Undo.IncrementCurrentGroup();
                    if (operation == "add") editor.AddSet();
                    else if (operation == "remove") editor.RemoveSet();
                    else editor.Move(1);
                    var after = FaceTrackingEditor.Serialize(sets, false);
                    Assert.That(after, Is.Not.EqualTo(before));
                    Undo.FlushUndoRecordObjects();
                    Undo.PerformUndo();
                    serialized.Update();
                    editor.Refresh(true);
                    Assert.That(FaceTrackingEditor.Serialize(sets, false), Is.EqualTo(before));
                    Undo.PerformRedo();
                    serialized.Update();
                    editor.Refresh(true);
                    Assert.That(FaceTrackingEditor.Serialize(sets, false), Is.EqualTo(after));
                }
            } finally {
                Undo.ClearUndo(owner);
                Object.DestroyImmediate(owner);
            }
        }

        [TestCase(false, false)]
        [TestCase(true, false)]
        [TestCase(false, true)]
        [TestCase(true, true)]
        public void FullControllerClipboardRejectsInvalidOrWrongKindWithoutChangingList(bool behaviors, bool wrongKind) {
            var owner = ScriptableObject.CreateInstance<AuthoringFields>();
            try {
                owner.controller.faceTrackingBlendshapeSets.Add(FaceTrackingUtils.CreateBlendshapeSet("Existing"));
                owner.controller.faceTrackingParameterBehaviors.Add(new VRCAvatarDescriptor.FaceTrackingParameterBehavior { outputParameterName = "Existing" });
                using (var serialized = new SerializedObject(owner)) {
                    var list = serialized.FindProperty("controller." + (behaviors ? "faceTrackingParameterBehaviors" : "faceTrackingBlendshapeSets"));
                    var before = FaceTrackingEditor.Serialize(list, behaviors);
                    var json = wrongKind ? FaceTrackingEditor.Serialize(serialized.FindProperty(
                        "controller." + (behaviors ? "faceTrackingBlendshapeSets" : "faceTrackingParameterBehaviors")), !behaviors) : "not json";
                    Assert.That(FaceTrackingEditor.Paste(list, behaviors, json, out var error), Is.False);
                    Assert.That(error, Is.Not.Null.And.Not.Empty);
                    Assert.That(FaceTrackingEditor.Serialize(list, behaviors), Is.EqualTo(before));
                }
            } finally {
                Object.DestroyImmediate(owner);
            }
        }

        [TestCase(false)]
        [TestCase(true)]
        public void FullControllerClipboardPasteSupportsUndoRedo(bool behaviors) {
            var owner = ScriptableObject.CreateInstance<AuthoringFields>();
            try {
                var json = behaviors
                    ? VRC.SDK3A.Editor.FaceTrackingClipboard.SerializeParameterBehaviors(new[] { owner.behavior, owner.behavior })
                    : VRC.SDK3A.Editor.FaceTrackingClipboard.SerializeBlendshapesSets(new[] { owner.set, owner.set });
                using (var serialized = new SerializedObject(owner)) {
                    var list = serialized.FindProperty("controller." + (behaviors ? "faceTrackingParameterBehaviors" : "faceTrackingBlendshapeSets"));
                    Undo.IncrementCurrentGroup();
                    Assert.That(FaceTrackingEditor.Paste(list, behaviors, json, out var error), Is.True, error);
                    Assert.That(list.arraySize, Is.EqualTo(2));
                    Undo.FlushUndoRecordObjects();
                    Undo.PerformUndo();
                    serialized.Update();
                    Assert.That(list.arraySize, Is.Zero);
                    Undo.PerformRedo();
                    serialized.Update();
                    Assert.That(list.arraySize, Is.EqualTo(2));
                }
            } finally {
                Undo.ClearUndo(owner);
                Object.DestroyImmediate(owner);
            }
        }

        [TestCase(false, 0, 1)]
        [TestCase(true, 0, 1)]
        [TestCase(false, 1, 2)]
        [TestCase(true, 1, 2)]
        [TestCase(false, 1, 1)]
        [TestCase(true, 1, 1)]
        public void FullControllerClipboardEnforcesLimitWithoutPartialPaste(bool behaviors, int remaining, int count) {
            var owner = ScriptableObject.CreateInstance<AuthoringFields>();
            try {
                var max = behaviors ? FaceTrackingUtils.MaxParameterBehaviors : FaceTrackingUtils.MaxBlendshapeSets;
                owner.controller.faceTrackingBlendshapeSets = Enumerable.Range(0, max - remaining)
                    .Select(i => FaceTrackingUtils.CreateBlendshapeSet("Existing" + i)).ToList();
                owner.controller.faceTrackingParameterBehaviors = Enumerable.Range(0, max - remaining)
                    .Select(i => new VRCAvatarDescriptor.FaceTrackingParameterBehavior { outputParameterName = "Existing" + i }).ToList();
                var json = behaviors
                    ? VRC.SDK3A.Editor.FaceTrackingClipboard.SerializeParameterBehaviors(Enumerable.Repeat(owner.behavior, count))
                    : VRC.SDK3A.Editor.FaceTrackingClipboard.SerializeBlendshapesSets(Enumerable.Repeat(owner.set, count));
                using (var serialized = new SerializedObject(owner)) {
                    var list = serialized.FindProperty("controller." + (behaviors ? "faceTrackingParameterBehaviors" : "faceTrackingBlendshapeSets"));
                    var before = FaceTrackingEditor.Serialize(list, behaviors);
                    Assert.That(FaceTrackingEditor.Paste(list, behaviors, json, out var error), Is.EqualTo(count <= remaining));
                    Assert.That(list.arraySize, Is.EqualTo(count <= remaining ? max - remaining + count : max - remaining));
                    if (count > remaining) {
                        Assert.That(error, Does.Contain("no configurations were pasted"));
                        Assert.That(FaceTrackingEditor.Serialize(list, behaviors), Is.EqualTo(before));
                    }
                }
            } finally {
                Object.DestroyImmediate(owner);
            }
        }

        [TestCase("Body", true)]
        [TestCase("Nested/Body", false)]
        [TestCase("", false)]
        [TestCase("direct", true)]
        [TestCase("nested", false)]
        [TestCase("bone", false)]
        public void DampeningInspectorUsesPrimaryMeshReferences(string target, bool expected) {
            using (var build = new Build()) {
                var owner = ScriptableObject.CreateInstance<AuthoringFields>();
                try {
                    var skin = build.Skin("Body");
                    var entry = new ObjectReference<SkinnedMeshRenderer> { useBone = false, offset = target };
                    if (target == "direct" || target == "nested") {
                        entry.useObj = true;
                        entry.obj = skin.gameObject;
                        entry.offset = "";
                        if (target == "nested") {
                            var parent = new GameObject("Nested");
                            parent.transform.SetParent(build.root.transform);
                            skin.transform.SetParent(parent.transform);
                        }
                    }
                    if (target == "bone") entry.useBone = true;
                    owner.controller.faceTrackingMeshes.Add(entry);
                    using (var serialized = new SerializedObject(owner)) {
                        Assert.That(FaceTrackingEditor.CanConfigureVisemeDampening(
                            serialized.FindProperty("controller.faceTrackingMeshes")), Is.EqualTo(expected));
                    }
                } finally {
                    Object.DestroyImmediate(owner);
                }
            }
        }

        private sealed class MeasuringBehaviorDrawer : VRC.SDK3A.Editor.FaceTrackingParameterBehaviorDrawer {
            public string output;
            public override float GetPropertyHeight(SerializedProperty property, GUIContent label) {
                // Exercise the real SDK cache without requiring its OnGUI-only native view/width calculation.
                var state = typeof(VRC.SDK3A.Editor.FaceTrackingParameterBehaviorDrawer)
                    .GetMethod("GetState", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(this, new object[] { property });
                output = ((SerializedProperty)state.GetType().GetField("outputParameterName").GetValue(state)).stringValue;
                // Stop before the wrapper requests IMGUI layout outside OnGUI.
                throw new MeasurementCompleteException();
            }
        }

        private sealed class MeasurementCompleteException : System.Exception { }

    }
}
#endif
