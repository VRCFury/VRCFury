using NUnit.Framework;
using UnityEngine;
using VF.Utils;

namespace VF.Tests {
    internal class BlendshapeUtilsTest {
        [Test]
        public void CopyingFramesOnTheSameMeshPreservesEachFrame() {
            var mesh = new Mesh();
            try {
                mesh.vertices = new[] { Vector3.zero, Vector3.up };
                var firstPositions = new[] { Vector3.right, Vector3.zero };
                var secondPositions = new[] { Vector3.zero, Vector3.forward };
                var firstNormals = new[] { Vector3.zero, Vector3.up };
                var secondNormals = new[] { Vector3.right, Vector3.zero };
                var firstTangents = new[] { Vector3.forward, Vector3.zero };
                var secondTangents = new[] { Vector3.zero, Vector3.right };
                mesh.AddBlendShapeFrame("Original", 40, firstPositions, firstNormals, firstTangents);
                mesh.AddBlendShapeFrame("Original", 100, secondPositions, secondNormals, secondTangents);

                var frames = new BlendshapeUtils.BlendshapeFrameBuffer(mesh.vertexCount);
                frames.CopyShape(mesh, 0, mesh, "Copy");

                Assert.That(mesh.GetBlendShapeName(1), Is.EqualTo("Copy"));
                Assert.That(mesh.GetBlendShapeFrameCount(1), Is.EqualTo(2));
                var positions = new Vector3[mesh.vertexCount];
                var normals = new Vector3[mesh.vertexCount];
                var tangents = new Vector3[mesh.vertexCount];
                mesh.GetBlendShapeFrameVertices(1, 0, positions, normals, tangents);
                Assert.That(mesh.GetBlendShapeFrameWeight(1, 0), Is.EqualTo(40));
                Assert.That(positions, Is.EqualTo(firstPositions));
                Assert.That(normals, Is.EqualTo(firstNormals));
                Assert.That(tangents, Is.EqualTo(firstTangents));
                mesh.GetBlendShapeFrameVertices(1, 1, positions, normals, tangents);
                Assert.That(mesh.GetBlendShapeFrameWeight(1, 1), Is.EqualTo(100));
                Assert.That(positions, Is.EqualTo(secondPositions));
                Assert.That(normals, Is.EqualTo(secondNormals));
                Assert.That(tangents, Is.EqualTo(secondTangents));
            } finally {
                Object.DestroyImmediate(mesh);
            }
        }
    }
}
