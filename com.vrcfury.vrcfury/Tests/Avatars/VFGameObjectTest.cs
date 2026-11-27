using System;
using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Animations;
using VF.Exceptions;
using VF.Utils;
using VRC.SDK3.Avatars.Components;
using VRC.SDK3.Dynamics.Contact.Components;
using VRC.SDK3.Dynamics.PhysBone.Components;
using Object = UnityEngine.Object;

namespace VF.Tests {
    internal class VFGameObjectTest {
        private GameObject root;
        private string folder;

        [SetUp]
        public void SetUp() {
            var name = "VFGameObjectTest_" + Guid.NewGuid().ToString("N");
            AssetDatabase.CreateFolder("Assets", name);
            folder = "Assets/" + name;
            root = new GameObject("Avatar");
            root.AddComponent<VRCAvatarDescriptor>();
        }

        [TearDown]
        public void TearDown() {
            Object.DestroyImmediate(root);
            AssetDatabase.DeleteAsset(folder);
        }

        [Test]
        public void ComponentScansSkipMissingScriptsAndKeepInterfaceMatches() {
            var child = new GameObject("Components");
            child.transform.SetParent(root.transform);
            child.AddComponent<VRCContactSender>();
            child.AddComponent<ParentConstraint>();
            var path = folder + "/Components.prefab";
            PrefabUtility.SaveAsPrefabAsset(child, path);
            File.WriteAllText(
                path,
                Regex.Replace(
                    File.ReadAllText(path),
                    @"(m_Script: \{fileID: -?\d+, guid: )[0-9a-f]{32}",
                    "${1}" + Guid.NewGuid().ToString("N")
                )
            );
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            Assert.That(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(prefab), Is.EqualTo(1));

            var constraint = prefab.GetComponent<ParentConstraint>();
            var wrapped = prefab.asVf();
            Assert.That(wrapped.GetComponents(), Is.EqualTo(new UnityEngine.Component[] { prefab.transform, constraint }));
            Assert.That(wrapped.GetComponentsInSelfAndChildren<IConstraint>(), Is.EqualTo(new[] { constraint }));
        }

        [Test]
        public void DestroyAllRemovesDynamicsThatUseAnyDeletedBranch() {
            var first = new GameObject("First");
            first.transform.SetParent(root.transform);
            var child = new GameObject("Child");
            child.transform.SetParent(first.transform);
            var second = new GameObject("Second");
            second.transform.SetParent(root.transform);

            var receiver = root.AddComponent<VRCContactReceiver>();
            receiver.rootTransform = child.transform;
            var collider = root.AddComponent<VRCPhysBoneCollider>();
            collider.rootTransform = second.transform;
            var unrelated = root.AddComponent<VRCContactSender>();

            VFGameObject.DestroyAll(new[] { first.asVf(), second.asVf() });

            Assert.That(first == null, Is.True);
            Assert.That(second == null, Is.True);
            Assert.That(receiver == null, Is.True);
            Assert.That(collider == null, Is.True);
            Assert.That(unrelated != null, Is.True);
        }

        [TestCase(typeof(VRCContactReceiver), "rootTransform")]
        [TestCase(typeof(VRCContactSender), "rootTransform")]
        [TestCase(typeof(VRCPhysBone), "rootTransform")]
        [TestCase(typeof(VRCPhysBoneCollider), "rootTransform")]
        public void DestroyReportsComponentWithMalformedTransform(Type type, string fieldName) {
            var source = new GameObject("Dynamics");
            source.SetActive(false);
            source.transform.SetParent(root.transform, false);
            var target = new GameObject("Target");
            target.transform.SetParent(source.transform, false);
            var rootField = type.GetField(fieldName);
            rootField.SetValue(source.AddComponent(type), target.transform);
            var path = folder + "/Dynamics.prefab";
            var prefab = PrefabUtility.SaveAsPrefabAsset(source, path);
            Object.DestroyImmediate(source);
            AssetDatabase.TryGetGUIDAndLocalFileIdentifier(
                prefab.transform.Find("Target").gameObject,
                out _,
                out long gameObjectId
            );
            // Editor assignments sanitize this. Loading malformed YAML creates a Transform
            // wrapper whose instance ID belongs to a GameObject, so Unity's null check passes.
            File.WriteAllText(
                path,
                Regex.Replace(
                    File.ReadAllText(path),
                    fieldName + @": \{fileID: -?\d+\}",
                    fieldName + ": {fileID: " + gameObjectId + "}"
                )
            );
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(path));
            instance.transform.SetParent(root.transform, false);
            target = instance.transform.Find("Target").gameObject;
            var component = instance.GetComponent(type);
            var brokenRoot = (Transform)rootField.GetValue(component);
            Assert.That(brokenRoot != null, Is.True);
            Assert.Throws<MissingReferenceException>(() => brokenRoot.IsChildOf(root.transform));
            var unrelated = new GameObject("Unrelated legacy object");
            unrelated.transform.SetParent(root.transform, false);
            var error = Assert.Throws<ExceptionWithCause>(() => unrelated.asVf().Destroy());
            Assert.That(error.Message, Does.Contain(type.Name).And.Contain(component.owner().GetDebugPath()));
            Assert.That(error.InnerException, Is.TypeOf<MissingReferenceException>());
            var batchError = Assert.Throws<ExceptionWithCause>(() => VFGameObject.DestroyAll(new[] { unrelated.asVf() }));
            Assert.That(batchError.Message, Is.EqualTo(error.Message));
            Assert.That(batchError.InnerException, Is.TypeOf<MissingReferenceException>());
            Assert.That(unrelated != null, Is.True);
            Assert.That(component != null, Is.True);
            Assert.That(
                rootField.GetValue(component),
                Is.SameAs(brokenRoot),
                "Malformed references must not be rewritten"
            );
        }

        [Test]
        public void DestroyReportsConstraintWithMalformedTransform() {
            var type = ReflectionUtils.GetTypeFromAnyAssembly("VRC.SDK3.Dynamics.Constraint.Components.VRCParentConstraint");
            if (type == null) Assert.Ignore("VRC constraints are not available in this SDK");
            DestroyReportsComponentWithMalformedTransform(type, "TargetTransform");
        }
    }
}
