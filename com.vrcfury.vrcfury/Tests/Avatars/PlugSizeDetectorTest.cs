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

namespace VF.Tests {
    internal class PlugSizeDetectorTest {
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

        [Test]
        public void SizeAndMaterialSlotsUseForwardVerticesAndPreserveRadiusPercentile(
            [Values(0.5f, 2f)] float scale,
            [Values] bool lastSlotUsed
        ) {
            var root = new GameObject("Size test");
            var mesh = new Mesh {
                vertices = new[] {
                    new Vector3(0.01f, 0, 0.1f),
                    new Vector3(0.02f, 0, 0.2f),
                    new Vector3(0.03f, 0, 0.3f),
                    new Vector3(0.04f, 0, 0.4f),
                    new Vector3(0.05f, 0, 0.5f),
                    new Vector3(0.06f, 0, 0.6f),
                    new Vector3(0.07f, 0, 0.7f),
                    new Vector3(10, 0, 0.8f),
                    new Vector3(0.99f, 0, -0.1f),
                    Vector3.zero,
                    new Vector3(0.08f, 0, 0.9f)
                },
                subMeshCount = 3
            };
            try {
                mesh.SetTriangles(new[] { 0, 1, 2 }, 0);
                mesh.SetTriangles(new[] { 8, 9, 8 }, 1);
                mesh.SetTriangles(lastSlotUsed ? new[] { 3, 4, 5 } : new[] { 8, 9, 8 }, 2);
                root.transform.SetPositionAndRotation(new Vector3(1, 2, -3), Quaternion.Euler(20, 30, 40));
                root.transform.localScale = Vector3.one * scale;
                root.AddComponent<MeshFilter>().sharedMesh = mesh;
                var renderer = root.AddComponent<MeshRenderer>();
                renderer.sharedMaterials = new Material[5];
                var size = PlugSizeDetector.GetAutoWorldSize(new[] { renderer }, root.transform.position, root.transform.rotation);
                Assert.That(size.Item1, Is.EqualTo(0.9f * scale).Within(0.00001f), "Unreferenced forward vertices still contribute to size");
                Assert.That(size.Item2, Is.EqualTo(0.07f * scale).Within(0.00001f), "Retain the existing 75th-percentile radius");
                Assert.That(size.Item3.Get(root), Is.EquivalentTo(lastSlotUsed ? new[] { 0, 2, 3, 4 } : new[] { 0 }));
            } finally {
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(mesh);
            }
        }

        [Test]
        public void AutoRendererSelectionIncludesDescendantBonesAndObservesChangedWeights() {
            var root = new GameObject("Avatar");
            var meshes = new List<Mesh>();
            try {
                root.AddComponent<VRCAvatarDescriptor>();
                var plugObject = new GameObject("Plug");
                plugObject.transform.SetParent(root.transform, false);
                var bone = new GameObject("Child bone").transform;
                bone.SetParent(plugObject.transform, false);
                var plug = plugObject.AddComponent<VRCFuryHapticPlug>();
                plug.autoRenderer = true;
                plug.useLegacyRendererFinder = false;
                SkinnedMeshRenderer Skin(string name, int matchedVertices) {
                    var obj = new GameObject(name);
                    obj.transform.SetParent(root.transform, false);
                    var mesh = new Mesh { vertices = new Vector3[3] };
                    meshes.Add(mesh);
                    mesh.boneWeights = new[] {
                        new BoneWeight { boneIndex0 = matchedVertices > 0 ? 0 : 1, weight0 = 1 },
                        new BoneWeight { boneIndex0 = matchedVertices > 1 ? 0 : 1, weight0 = 1 },
                        new BoneWeight { boneIndex0 = 1, weight0 = 1 }
                    };
                    var skin = obj.AddComponent<SkinnedMeshRenderer>();
                    skin.sharedMesh = mesh;
                    skin.bones = new[] { bone, root.transform };
                    return skin;
                }
                var first = Skin("First", 2);
                var second = Skin("Second", 1);
                first.gameObject.SetActive(false);
                Assert.That(PlugRendererFinder.GetRenderers(plug), Is.EquivalentTo(new[] { first }));
                first.sharedMesh.boneWeights = new[] {
                    new BoneWeight { boneIndex0 = 1, weight0 = 1 },
                    new BoneWeight { boneIndex0 = 1, weight0 = 1 },
                    new BoneWeight { boneIndex0 = 1, weight0 = 1 }
                };
                Assert.That(PlugRendererFinder.GetRenderers(plug), Is.EquivalentTo(new[] { second }));
            } finally {
                Object.DestroyImmediate(root);
                foreach (var mesh in meshes) Object.DestroyImmediate(mesh);
            }
        }
    }
}
