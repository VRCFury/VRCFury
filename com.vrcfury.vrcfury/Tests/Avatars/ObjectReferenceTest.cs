using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
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

namespace VF.Tests {
    [Category("VRCFury")]
    internal class ObjectReferenceTest {
        private static VRCFArmatureCache CachedHips(GameObject hips) {
            var cache = new VRCFArmatureCache();
            var snapshot = Activator.CreateInstance(typeof(VRCFArmatureCache).GetNestedType("Snapshot", BindingFlags.NonPublic), true);
            ((IDictionary)snapshot.GetType().VFField("boneObjects").GetValue(snapshot))[HumanBodyBones.Hips] = (VFGameObject)hips;
            ((IList)typeof(VRCFArmatureCache).VFField("snapshots").GetValue(cache)).Add(snapshot);
            return cache;
        }

        private class Fields : ScriptableObject {
            public ObjectReference target = new ObjectReference();
            public List<ObjectReference> targets = new List<ObjectReference>();
            public ObjectReference<SkinnedMeshRenderer> typed = new ObjectReference<SkinnedMeshRenderer>();
        }

        [Test]
        public void TypedReferencesResolveComponentsAndOverridesBypassObjectResolution() {
            var root = new GameObject("Avatar");
            var child = new GameObject("Mesh");
            var outside = new GameObject("Override");
            var owner = ScriptableObject.CreateInstance<Fields>();
            child.transform.SetParent(root.transform);
            try {
                var skin = child.AddComponent<SkinnedMeshRenderer>();
                var overrideSkin = outside.AddComponent<SkinnedMeshRenderer>();
                owner.typed = skin;
                Assert.That(owner.typed.Resolve(root), Is.SameAs(skin));
                Assert.That(owner.typed.componentOverride, Is.SameAs(skin));
                Assert.That(owner.typed.obj, Is.Null);
                owner.typed = new ObjectReference<SkinnedMeshRenderer> { useBone = false, useObj = true, obj = child };
                using (var serialized = new SerializedObject(owner)) {
                    var property = serialized.FindProperty("typed");
                    Assert.That(property.FindPropertyRelative("obj").objectReferenceValue, Is.SameAs(child));
                    Assert.That(property.FindPropertyRelative("componentOverride"), Is.Null);
                    var gui = ObjectReferenceDrawer.CreateEditor(property, new ObjectReferenceDrawer.Options());
                    Assert.That(gui.Q<ObjectField>().bindingPath, Is.EqualTo("typed.obj"));
                }
                owner.typed = new ObjectReference<SkinnedMeshRenderer> { useBone = false, offset = "Mesh" };
                Assert.That(owner.typed.Resolve(root), Is.SameAs(skin));
                owner.typed.offset = "Missing";
                Assert.Throws<Exception>(() => owner.typed.Resolve(root));
                owner.typed.componentOverride = overrideSkin;
                Assert.That(owner.typed.Resolve(root), Is.SameAs(overrideSkin));
                Assert.That(owner.typed.Resolve(null), Is.SameAs(overrideSkin));
                owner.typed.componentOverride = null;
                owner.typed.offset = "";
                Assert.That(Assert.Throws<Exception>(() => owner.typed.Resolve(root)).Message,
                    Does.Contain("has no SkinnedMeshRenderer"));
                Assert.Throws<Exception>(() => ((ObjectReference<SkinnedMeshRenderer>)null).Resolve(root));
            } finally {
                Object.DestroyImmediate(owner);
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(outside);
            }
        }

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

        [TestCase(false, false)]
        [TestCase(true, false)]
        [TestCase(false, true)]
        public void ArmatureShowsOffsetsForAllTargetsWhenRequestedOrAlreadyUsed(bool showOffset, bool existingOffset) {
            var owner = ScriptableObject.CreateInstance<Fields>();
            try {
                owner.targets.Add(new ObjectReference());
                owner.targets.Add(new ObjectReference { offset = existingOffset ? "Child" : "" });
                using (var serialized = new SerializedObject(owner)) {
                    var gui = ArmatureLinkBuilder.LinkTargetsEditor(serialized.FindProperty("targets"), showOffset);
                    var list = gui.Q<ListView>();
                    for (var i = 0; i < 2; i++) {
                        var row = list.makeItem();
                        list.bindItem(row, i);
                        Assert.That(row.Query<Label>().ToList().Any(label => label.text == "Offset Path"),
                            Is.EqualTo(showOffset || existingOffset));
                        list.unbindItem(row, i);
                    }
                }
            } finally {
                Object.DestroyImmediate(owner);
            }
        }

        [TestCase(false, false, "Animator is missing")]
        [TestCase(true, false, "Animator doesn't have an Avatar set")]
        [TestCase(true, true, "rig is not set to humanoid")]
        public void MissingBonesReportTheActualAnimatorSetup(bool hasAnimator, bool hasAvatar, string message) {
            var root = new GameObject("Avatar");
            Avatar rig = null;
            try {
                if (hasAnimator) {
                    var animator = root.AddComponent<Animator>();
                    if (hasAvatar) {
                        rig = AvatarBuilder.BuildGenericAvatar(root, "");
                        animator.avatar = rig;
                    }
                }
                var cache = new VRCFArmatureCache();
                typeof(VRCFArmatureCache).VFField("avatarObject").SetValue(cache, (VFGameObject)root);
                var error = Assert.Throws<Exception>(() => cache.FindBoneOnArmatureOrException(HumanBodyBones.Hips));
                Assert.That(error.Message, Does.Contain(message));
            } finally {
                Object.DestroyImmediate(root);
                if (rig != null) Object.DestroyImmediate(rig);
            }
        }

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(3)]
        public void ArmatureTargetsOnlyUseAListForMultipleOrMissingEntries(int count) {
            var owner = ScriptableObject.CreateInstance<Fields>();
            try {
                for (var i = 0; i < count; i++) owner.targets.Add(new ObjectReference());
                using (var serialized = new SerializedObject(owner)) {
                    var gui = ArmatureLinkBuilder.LinkTargetsEditor(serialized.FindProperty("targets"));
                    var list = gui.Q<ListView>();
                    Assert.That(list == null, Is.EqualTo(count == 1));
                    if (count == 1) {
                        Assert.That(gui.Query<PopupField<string>>().ToList()
                            .Count(field => field.choices.Contains("Object Reference")), Is.EqualTo(1));
                    } else {
                        var row = list.makeItem();
                        for (var i = 0; i < count; i++) {
                            list.bindItem(row, i);
                            Assert.That(row.Query<Label>().ToList()
                                .Where(label => label.text.StartsWith("Fallback #")).Select(label => label.text),
                                Is.EqualTo(i == 0 ? new string[0] : new[] { $"Fallback #{i}" }));
                            list.unbindItem(row, i);
                        }
                    }
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
            var bone = new GameObject("Bone");
            var other = new GameObject("Other");
            var caches = (IDictionary)typeof(VRCFArmatureCache)
                .GetField("perFrame", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
            try {
                bone.transform.SetParent(avatar.transform);
                other.transform.SetParent(avatar.transform);
                Assert.That(ObjectReferenceDrawer.IsBone(bone, avatar), Is.False);
                caches[(VFGameObject)avatar] = CachedHips(bone);
                Assert.That(ObjectReferenceDrawer.IsBone(bone, avatar), Is.True);
                bone.SetActive(false);
                Assert.That(ObjectReferenceDrawer.IsBone(bone, avatar), Is.True);
                Assert.That(ObjectReferenceDrawer.IsBone(other, avatar), Is.False);
                Assert.That(ObjectReferenceDrawer.IsBone(null, avatar), Is.False);
                Assert.That(ObjectReferenceDrawer.IsBone(bone, null), Is.False);
            } finally {
                caches.Remove((VFGameObject)avatar);
                Object.DestroyImmediate(avatar);
            }
        }

        [Test]
        public void GameObjectsConvertToObjectMode() {
            var obj = new GameObject("Renderer");
            try {
                ObjectReference gameObject = obj;
                Assert.That(gameObject.mode, Is.EqualTo(ObjectReference.Mode.ObjectReference));
                Assert.That(gameObject.obj, Is.SameAs(obj));
                ObjectReference empty = (GameObject)null;
                Assert.That(empty, Is.Not.Null);
                Assert.That(empty.obj, Is.Null);
            } finally {
                Object.DestroyImmediate(obj);
            }
        }

        [Test]
        public void OutsideObjectReferencesReportTheAvatarMismatchBeforeResolvingOffsets() {
            var avatar = new GameObject("Avatar");
            var outside = new GameObject("Outside");
            var child = new GameObject("Child");
            child.transform.SetParent(outside.transform);
            try {
                ObjectReference reference = outside;
                reference.offset = "Child";
                var error = Assert.Throws<Exception>(() => reference.Resolve(avatar));
                Assert.That(error.Message, Does.Contain("Outside is not a child of Avatar"));
                Assert.That(error.Message, Does.Not.Contain("Failed to find object at path"));
            } finally {
                Object.DestroyImmediate(avatar);
                Object.DestroyImmediate(outside);
            }
        }

        [Test]
        public void ObjectAndAvatarPathsResolveOffsetsAndPreserveCachedPaths() {
            var root = new GameObject("Avatar");
            var child = new GameObject("Body");
            var mesh = new GameObject("Mesh");
            child.transform.SetParent(root.transform);
            mesh.transform.SetParent(child.transform);
            try {
                var paths = VRCFObjectPathCache.GetPerFrame(root);
                var reference = new ObjectReference { useBone = false, offset = "Body/Mesh" };
                Assert.That((GameObject)reference.Resolve(root, paths), Is.SameAs(mesh));
                child.name = "Renamed";
                Assert.That((GameObject)reference.Resolve(root, paths), Is.SameAs(mesh));
                reference = child;
                reference.offset = "Mesh";
                Assert.That((GameObject)reference.Resolve(root, paths), Is.SameAs(mesh));
                reference = new ObjectReference { useBone = false };
                Assert.That((GameObject)reference.Resolve(root), Is.SameAs(root));
                reference.offset = "Missing";
                Assert.Throws<Exception>(() => reference.Resolve(root));
            } finally {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void BoneOffsetsAndArmatureLinkShareTheCachedResolver() {
            var root = new GameObject("Avatar");
            var hips = new GameObject("Hips");
            var target = new GameObject("Target");
            var prop = new GameObject("Prop");
            hips.transform.SetParent(root.transform);
            target.transform.SetParent(hips.transform);
            prop.transform.SetParent(root.transform);
            try {
                var bones = CachedHips(hips);
                var reference = new ObjectReference { offset = "Target" };
                var paths = VRCFObjectPathCache.GetPerFrame(root);
                Assert.That((GameObject)reference.Resolve(root, paths, bones), Is.SameAs(target));
                var model = new ArmatureLink { propBone = prop };
                model.linkTo.Clear();
                model.linkTo.Add(reference);
                var links = ArmatureLinkService.GetLinks(model, root, paths, bones);
                Assert.That((GameObject)links.mergeBones.Single().Item2, Is.SameAs(target));
            } finally {
                Object.DestroyImmediate(root);
            }
        }

#if VRCSDK_HAS_FACE_TRACKING
        [Test]
        public void FullControllerResolvesFaceTrackingMeshFromCachedBoneAndOffset() {
            var root = new GameObject("Avatar");
            var hips = new GameObject("Hips");
            var target = new GameObject("Mesh");
            hips.transform.SetParent(root.transform);
            target.transform.SetParent(hips.transform);
            var mesh = new Mesh { vertices = new[] { Vector3.zero } };
            var skin = target.AddComponent<SkinnedMeshRenderer>();
            mesh.AddBlendShapeFrame("Smile", 100, new[] { Vector3.up }, null, null);
            skin.sharedMesh = mesh;
            try {
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
                Inject(builder, "armatureCache", CachedHips(hips));
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
            }
        }
#endif
    }
}
