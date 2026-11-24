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

    internal class ObjectReferenceDrawerTest : ObjectReferenceTestBase {
        [TestCase(ObjectReference.Mode.Bone, "", false, false)]
        [TestCase(ObjectReference.Mode.ObjectReference, "", false, false)]
        [TestCase(ObjectReference.Mode.Bone, "Child", false, true)]
        [TestCase(ObjectReference.Mode.ObjectReference, "Child", false, true)]
        [TestCase(ObjectReference.Mode.Bone, "", true, true)]
        [TestCase(ObjectReference.Mode.ObjectReference, "", true, true)]
        [TestCase(ObjectReference.Mode.ObjectPath, "", false, true)]
        public void OffsetFieldsAreHiddenUnlessNeeded(ObjectReference.Mode mode, string offset, bool showOffset, bool visible) {
            var owner = ScriptableObject.CreateInstance<Fields>();
            try {
                using (var serialized = new SerializedObject(owner)) {
                    var property = serialized.FindProperty("target");
                    ObjectReferenceDrawer.SetMode(property, mode);
                    property.FindPropertyRelative("offset").stringValue = offset;
                    serialized.ApplyModifiedProperties();
                    var gui = ObjectReferenceDrawer.CreateEditor(property,
                        new ObjectReferenceDrawer.Options { showOffsetPath = showOffset });
                    var pathVisible = mode == ObjectReference.Mode.ObjectPath
                        ? gui.Query<TextField>().ToList().Any(field => field.bindingPath == property.FindPropertyRelative("offset").propertyPath)
                        : gui.Query<Label>().ToList().Any(label => label.text == "Offset Path");
                    Assert.That(pathVisible, Is.EqualTo(visible));
                }
            } finally {
                Object.DestroyImmediate(owner);
            }
        }

        [TestCase(ObjectReference.Mode.Bone)]
        [TestCase(ObjectReference.Mode.ObjectReference)]
        [TestCase(ObjectReference.Mode.ObjectPath)]
        public void ModeChangesClearPathsButReselectingTheSameModePreservesThem(ObjectReference.Mode initialMode) {
            var owner = ScriptableObject.CreateInstance<Fields>();
            try {
                using (var serialized = new SerializedObject(owner)) {
                    var property = serialized.FindProperty("target");
                    foreach (ObjectReference.Mode nextMode in Enum.GetValues(typeof(ObjectReference.Mode))) {
                        ObjectReferenceDrawer.SetMode(property, initialMode);
                        property.FindPropertyRelative("offset").stringValue = "Child/Path";
                        serialized.ApplyModifiedProperties();
                        ObjectReferenceDrawer.SetMode(property, nextMode);
                        Assert.That(owner.target.offset, Is.EqualTo(nextMode == initialMode ? "Child/Path" : ""));
                    }
                }
            } finally {
                Object.DestroyImmediate(owner);
            }
        }

        [TestCase(ObjectReference.Mode.Bone)]
        [TestCase(ObjectReference.Mode.ObjectReference)]
        [TestCase(ObjectReference.Mode.ObjectPath)]
        public void ModeAndMainFieldShareOneRowWithOffsetBelow(ObjectReference.Mode mode) {
            var owner = ScriptableObject.CreateInstance<Fields>();
            try {
                using (var serialized = new SerializedObject(owner)) {
                    var property = serialized.FindProperty("target");
                    ObjectReferenceDrawer.SetMode(property, mode);
                    var gui = ObjectReferenceDrawer.CreateEditor(property,
                        new ObjectReferenceDrawer.Options { showOffsetPath = true });
                    var dropdown = gui.Q<PopupField<string>>();
                    var row = dropdown.parent;
                    Assert.That(row.style.flexDirection.value, Is.EqualTo(FlexDirection.Row));
                    Assert.That(row.childCount, Is.EqualTo(2));
                    Assert.That(row[0], Is.SameAs(dropdown));
                    Assert.That(dropdown.style.width.value.value, Is.EqualTo(140));
                    Assert.That(row[1].style.flexGrow.value, Is.EqualTo(1));
                    Assert.That(row[1].style.flexBasis.value.value, Is.Zero);
                    if (mode == ObjectReference.Mode.Bone)
                        Assert.That(row[1].Q<PopupField<string>>().bindingPath, Is.EqualTo("target.bone"));
                    else if (mode == ObjectReference.Mode.ObjectReference)
                        Assert.That(row[1].Q<ObjectField>().bindingPath, Is.EqualTo("target.obj"));
                    else
                        Assert.That(((TextField)row[1]).bindingPath, Is.EqualTo("target.offset"));
                    Assert.That(row.Query<Label>().ToList().Any(label => label.text == "Offset Path"), Is.False);
                    Assert.That(gui.Query<Label>().ToList().Any(label => label.text == "Offset Path"),
                        Is.EqualTo(mode != ObjectReference.Mode.ObjectPath));
                }
            } finally {
                Object.DestroyImmediate(owner);
            }
        }

        [TestCase("target")]
        [TestCase("targets.Array.data[0]")]
        public void ObjectPickerIsBoundToTheSerializedReference(string path) {
            var owner = ScriptableObject.CreateInstance<Fields>();
            var obj = new GameObject("Object");
            try {
                owner.target = obj;
                owner.targets.Add(obj);
                using (var serialized = new SerializedObject(owner)) {
                    var property = serialized.FindProperty(path);
                    var gui = ObjectReferenceDrawer.CreateEditor(property, new ObjectReferenceDrawer.Options());
                    var field = gui.Q<ObjectField>();
                    Assert.That(field, Is.Not.Null);
                    Assert.That(field.objectType, Is.EqualTo(typeof(GameObject)));
                    Assert.That(field.bindingPath, Is.EqualTo(property.FindPropertyRelative("obj").propertyPath));
                }
            } finally {
                Object.DestroyImmediate(owner);
                Object.DestroyImmediate(obj);
            }
        }

        [TestCase(ObjectReference.Mode.Bone)]
        [TestCase(ObjectReference.Mode.ObjectPath)]
        public void LeavingObjectModeClearsTheReference(ObjectReference.Mode mode) {
            var owner = ScriptableObject.CreateInstance<Fields>();
            var obj = new GameObject("Object");
            try {
                owner.target = obj;
                using (var serialized = new SerializedObject(owner)) {
                    var property = serialized.FindProperty("target");
                    var gui = ObjectReferenceDrawer.CreateEditor(property, new ObjectReferenceDrawer.Options());
                    var dropdown = gui.Q<PopupField<string>>();
                    Assert.That(dropdown.choices, Is.EqualTo(new[] { "Avatar Bone", "Object Reference", "Object Path" }));
                    Assert.That(gui.Q<ObjectField>().objectType, Is.EqualTo(typeof(GameObject)));
                    // Detached fields do not send value-change events; invoke the dropdown callback's setter.
                    ObjectReferenceDrawer.SetMode(property, mode);
                    serialized.Update();
                    Assert.That(owner.target.mode, Is.EqualTo(mode));
                    Assert.That(property.FindPropertyRelative("obj").objectReferenceValue, Is.Null);
                    ObjectReferenceDrawer.SetMode(property, ObjectReference.Mode.ObjectReference);
                    Assert.That(owner.target.obj, Is.Null, "Switching back must not restore a stale reference");
                }
            } finally {
                Object.DestroyImmediate(owner);
                Object.DestroyImmediate(obj);
            }
        }

        [TestCase(ObjectReference.Mode.Bone)]
        [TestCase(ObjectReference.Mode.ObjectReference)]
        [TestCase(ObjectReference.Mode.ObjectPath)]
        public void SameSlotReorderAndTransientDeselectionPreserveTheReference(ObjectReference.Mode mode) {
            var owner = ScriptableObject.CreateInstance<Fields>();
            var obj = new GameObject("Object");
            try {
                owner.targets.Add(obj);
                using (var serialized = new SerializedObject(owner)) {
                    var list = serialized.FindProperty("targets");
                    var entry = list.GetArrayElementAtIndex(0);
                    ObjectReferenceDrawer.SetMode(entry, mode);
                    entry.FindPropertyRelative("bone").enumValueIndex = (int)HumanBodyBones.Head;
                    entry.FindPropertyRelative("offset").stringValue = "Some/Offset";
                    serialized.ApplyModifiedProperties();
                    var before = JsonUtility.ToJson(owner.targets.Single());
                    list.MoveArrayElement(0, 0);
                    serialized.ApplyModifiedProperties();
                    // Transient no-selection values must never become an Object Path request.
                    ObjectReferenceDrawer.SetMode(list.GetArrayElementAtIndex(0), (ObjectReference.Mode)(-1));
                    Assert.That(JsonUtility.ToJson(owner.targets.Single()), Is.EqualTo(before));
                    Assert.That(owner.targets.Single().mode, Is.EqualTo(mode));
                    if (mode == ObjectReference.Mode.ObjectReference)
                        Assert.That(owner.targets.Single().obj, Is.SameAs(obj));
                }
            } finally {
                Object.DestroyImmediate(owner);
                Object.DestroyImmediate(obj);
            }
        }

        [TestCase(ObjectReference.Mode.Bone, true)]
        [TestCase(ObjectReference.Mode.ObjectReference, false)]
        [TestCase(ObjectReference.Mode.ObjectPath, false)]
        public void FaceTrackingHidesBoneUnlessAlreadySelected(ObjectReference.Mode mode, bool boneVisible) {
            var owner = ScriptableObject.CreateInstance<Fields>();
            try {
                using (var serialized = new SerializedObject(owner)) {
                    var property = serialized.FindProperty("target");
                    ObjectReferenceDrawer.SetMode(property, mode);
                    var gui = ObjectReferenceDrawer.CreateEditor(property, new ObjectReferenceDrawer.Options {
                        allowBone = false, componentType = typeof(SkinnedMeshRenderer)
                    });
                    var dropdown = gui.Q<PopupField<string>>();
                    Assert.That(dropdown.choices.Contains("Avatar Bone"), Is.EqualTo(boneVisible));
                    Assert.That(dropdown.choices, Does.Contain("Object Reference").And.Contain("Object Path"));
                    Assert.That(dropdown.value, Is.EqualTo(mode == ObjectReference.Mode.Bone ? "Avatar Bone"
                        : mode == ObjectReference.Mode.ObjectReference ? "Object Reference" : "Object Path"));
                }
            } finally {
                Object.DestroyImmediate(owner);
            }
        }

        [TestCase(ObjectReference.Mode.Bone)]
        [TestCase(ObjectReference.Mode.ObjectReference)]
        public void NewEntriesUseTheContextDefault(ObjectReference.Mode mode) {
            var owner = ScriptableObject.CreateInstance<Fields>();
            try {
                using (var serialized = new SerializedObject(owner)) {
                    ObjectReferenceDrawer.AddEntry(serialized.FindProperty("targets"), new ObjectReferenceDrawer.Options { defaultMode = mode });
                    Assert.That(owner.targets.Single().mode, Is.EqualTo(mode));
                }
            } finally {
                Object.DestroyImmediate(owner);
            }
        }

        [TestCase(typeof(SkinnedMeshRenderer))]
        [TestCase(typeof(BoxCollider))]
        public void ComponentWarningsUseTheContextType(Type componentType) {
            var owner = ScriptableObject.CreateInstance<Fields>();
            var obj = new GameObject("Object");
            try {
                owner.target = obj;
                using (var serialized = new SerializedObject(owner)) {
                    var property = serialized.FindProperty("target");
                    var options = new ObjectReferenceDrawer.Options { componentType = componentType };
                    var gui = ObjectReferenceDrawer.CreateEditor(property, options);
                    Assert.That(gui.Query<Label>().ToList().Any(label => label.text.Contains("has no " + componentType.Name)), Is.True);
                    obj.AddComponent(componentType);
                    gui = ObjectReferenceDrawer.CreateEditor(property, options);
                    Assert.That(gui.Query<Label>().ToList().Any(label => label.text.Contains("has no " + componentType.Name)), Is.False);
                }
            } finally {
                Object.DestroyImmediate(owner);
                Object.DestroyImmediate(obj);
            }
        }

        [TestCase(ObjectReference.Mode.Bone, false)]
        [TestCase(ObjectReference.Mode.ObjectReference, false)]
        [TestCase(ObjectReference.Mode.ObjectPath, false)]
        public void ArmatureContextDoesNotRecommendBonesWithoutAnAvatar(ObjectReference.Mode mode, bool warning) {
            var owner = ScriptableObject.CreateInstance<Fields>();
            try {
                using (var serialized = new SerializedObject(owner)) {
                    var property = serialized.FindProperty("target");
                    ObjectReferenceDrawer.SetMode(property, mode);
                    var gui = ObjectReferenceDrawer.CreateEditor(property, new ObjectReferenceDrawer.Options { allowBone = true });
                    Assert.That(gui.Query<Label>().ToList().Any(label => label.text.Contains("Use Avatar Bone mode")), Is.EqualTo(warning));
                }
            } finally {
                Object.DestroyImmediate(owner);
            }
        }

        [Test]
        public void BoneGuidanceUsesTheAvatarsArmatureCache() {
            var avatar = new GameObject("Avatar");
            var other = new GameObject("Other");
            Avatar rig = null;
            try {
                var (humanoid, _, hips) = HumanoidTestRig.Create(avatar);
                rig = humanoid;
                var bone = hips.gameObject;
                other.transform.SetParent(avatar.transform);
                Assert.That(ObjectReferenceDrawer.IsBone(bone, avatar), Is.False);
                avatar.AddComponent<Animator>().avatar = rig;
                Assert.That(ObjectReferenceDrawer.IsBone(bone, avatar), Is.True);
                bone.SetActive(false);
                Assert.That(ObjectReferenceDrawer.IsBone(bone, avatar), Is.True);
                Assert.That(ObjectReferenceDrawer.IsBone(other, avatar), Is.False);
                Assert.That(ObjectReferenceDrawer.IsBone(null, avatar), Is.False);
                Assert.That(ObjectReferenceDrawer.IsBone(bone, null), Is.False);
            } finally {
                Object.DestroyImmediate(avatar);
                Object.DestroyImmediate(rig);
            }
        }
    }
}
