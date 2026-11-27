namespace VF.Tests {
    using System;
    using NUnit.Framework;
    using UnityEditor;
    using UnityEngine;
    using VF.Builder.Haptics;
    using VF.Utils;
    using Object = UnityEngine.Object;

    internal class PlugSizeDetectorTest {
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

    }
}
