using System;
using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
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
