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

    internal class ObjectReferenceExtensionsTest : ObjectReferenceTestBase {
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
    }
}
