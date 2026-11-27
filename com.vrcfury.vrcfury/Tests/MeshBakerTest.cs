using System;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using VF.Builder;
using VF.Utils;
using Object = UnityEngine.Object;

public class MeshBakerTest {
    [Test]
    public void BoneAssignmentsAndAncestorMovementRefreshDependencies() {
        var root = new GameObject("Avatar");
        var mesh = new Mesh {
            vertices = new[] { Vector3.right },
            boneWeights = new[] { new BoneWeight { boneIndex0 = 0, weight0 = 1 } },
            bindposes = new[] { Matrix4x4.identity }
        };
        try {
            var obj = new GameObject("Renderer");
            obj.transform.SetParent(root.transform, false);
            var parent = new GameObject("Bone parent");
            parent.transform.SetParent(root.transform, false);
            var first = new GameObject("First bone");
            first.transform.SetParent(parent.transform, false);
            var second = new GameObject("Second bone");
            second.transform.SetParent(parent.transform, false);
            second.transform.localPosition = Vector3.up;
            var skin = obj.AddComponent<SkinnedMeshRenderer>();
            skin.sharedMesh = mesh;
            skin.bones = new[] { first.transform };
            var initial = MeshBaker.BakeMesh(skin);
            skin.bones = new[] { second.transform };
            var reassigned = MeshBaker.BakeMesh(skin);
            Assert.That(reassigned, Is.Not.SameAs(initial));
            Assert.That(Vector3.Distance(reassigned.vertices[0], Vector3.right + Vector3.up), Is.LessThan(.00001f));
            second.transform.localPosition = Vector3.up * 2;
            var moved = MeshBaker.BakeMesh(skin);
            Assert.That(Vector3.Distance(moved.vertices[0], Vector3.right + Vector3.up * 2), Is.LessThan(.00001f));

            // A parent changes the posed mesh without dirtying the renderer or the bone.
            var rendererVersion = EditorUtility.GetDirtyCount(skin);
            var boneVersion = EditorUtility.GetDirtyCount(second.transform);
            parent.transform.localPosition = Vector3.forward;
            Assert.That(EditorUtility.GetDirtyCount(skin), Is.EqualTo(rendererVersion));
            Assert.That(EditorUtility.GetDirtyCount(second.transform), Is.EqualTo(boneVersion));
            var parentMoved = MeshBaker.BakeMesh(skin);
            Assert.That(parentMoved, Is.Not.SameAs(moved));
            Assert.That(Vector3.Distance(parentMoved.vertices[0], Vector3.right + Vector3.up * 2 + Vector3.forward), Is.LessThan(.00001f));

            // rootBone has a different setter: it does not increment the renderer version.
            skin.rootBone = second.transform;
            Assert.That(EditorUtility.GetDirtyCount(skin), Is.EqualTo(rendererVersion));
            var rootChanged = MeshBaker.BakeMesh(skin);
            Assert.That(rootChanged, Is.Not.SameAs(parentMoved));
            Assert.That(MeshBaker.BakeMesh(skin), Is.SameAs(rootChanged));

            obj.transform.localPosition = Vector3.right;
            var rendererMoved = MeshBaker.BakeMesh(skin);
            Assert.That(Vector3.Distance(rendererMoved.vertices[0], Vector3.up * 2 + Vector3.forward), Is.LessThan(.00001f));
        } finally {
            Object.DestroyImmediate(root);
            Object.DestroyImmediate(mesh);
        }
    }

    [TestCase(0, .1f)]
    [TestCase(0, 1f)]
    [TestCase(0, 2f)]
    [TestCase(1, .1f)]
    [TestCase(1, 1f)]
    [TestCase(1, 2f)]
    [TestCase(2, .1f)]
    [TestCase(2, 1f)]
    [TestCase(2, 2f)]
    public void PreservesNormalAndTangentMagnitudeAcrossScales(int rendererType, float avatarScale) {
        var root = new GameObject("Avatar");
        var obj = new GameObject("Renderer");
        obj.transform.SetParent(root.transform, false);
        var origin = new GameObject("Bake origin");
        origin.transform.SetParent(root.transform, false);
        var bone = new GameObject("Bone");
        bone.transform.SetParent(root.transform, false);
        var mesh = new Mesh {
            vertices = new[] { Vector3.right },
            normals = new[] { Vector3.up * 2 },
            tangents = new[] { new Vector4(3, 0, 0, -1) }
        };
        try {
            Renderer renderer;
            if (rendererType == 0) {
                obj.AddComponent<MeshFilter>().sharedMesh = mesh;
                renderer = obj.AddComponent<MeshRenderer>();
            } else {
                if (rendererType == 2) {
                    mesh.boneWeights = new[] { new BoneWeight { boneIndex0 = 0, weight0 = 1 } };
                    mesh.bindposes = new[] { Matrix4x4.identity };
                }
                var skin = obj.AddComponent<SkinnedMeshRenderer>();
                skin.sharedMesh = mesh;
                if (rendererType == 2) {
                    skin.bones = new[] { bone.transform };
                    skin.rootBone = bone.transform;
                }
                renderer = skin;
            }
            root.transform.localScale = Vector3.one * avatarScale;
            root.transform.rotation = Quaternion.Euler(20, 30, 40);
            origin.transform.localRotation = Quaternion.Euler(10, 15, 25);
            origin.transform.localScale = Vector3.one * .7f;
            foreach (var rendererScale in new[] { 1f, 3f }) {
                obj.transform.localScale = Vector3.one * rendererScale;
                // Skinning follows the bone; static and unweighted meshes follow the renderer.
                var worldScale = avatarScale * (rendererType == 2 ? 1 : rendererScale);
                foreach (var applyScale in new[] { false, true }) {
                    var baked = MeshBaker.BakeMesh(renderer, origin.transform, applyScale);
                    var normal = applyScale
                        ? origin.transform.TransformDirection(baked.normals[0])
                        : origin.transform.TransformVector(baked.normals[0]);
                    var tangent = applyScale
                        ? origin.transform.TransformDirection(baked.tangents[0])
                        : origin.transform.TransformVector(baked.tangents[0]);
                    var expectedNormal = root.transform.TransformDirection(Vector3.up * (2 * worldScale));
                    var expectedTangent = root.transform.TransformDirection(Vector3.right * (3 * worldScale));
                    Assert.That(Vector3.Distance(normal, expectedNormal), Is.LessThan(.00001f));
                    Assert.That(Vector3.Distance(tangent, expectedTangent), Is.LessThan(.00001f));
                }
            }
            Assert.That(mesh.normals[0], Is.EqualTo(Vector3.up * 2));
            Assert.That(mesh.tangents[0], Is.EqualTo(new Vector4(3, 0, 0, -1)));
        } finally {
            Object.DestroyImmediate(root);
            Object.DestroyImmediate(mesh);
        }
    }

    [Test]
    public void OriginConversionsDoNotModifyTheCachedMesh() {
        var root = new GameObject("Renderer");
        var origin = new GameObject("Origin");
        var mesh = new Mesh {
            vertices = new[] { Vector3.right },
            normals = new[] { Vector3.up }
        };
        try {
            root.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = root.AddComponent<MeshRenderer>();
            var local = MeshBaker.BakeMesh(renderer);
            origin.transform.position = Vector3.forward;
            origin.transform.localScale = Vector3.one * 2;
            var converted = MeshBaker.BakeMesh(renderer, origin.transform);
            Assert.That(converted.vertices[0], Is.EqualTo(new Vector3(.5f, 0, -.5f)));
            var scaled = MeshBaker.BakeMesh(renderer, origin.transform, true);
            Assert.That(scaled.vertices[0], Is.EqualTo(new Vector3(1, 0, -1)));
            Assert.That(MeshBaker.BakeMesh(renderer), Is.SameAs(local));
            Assert.That(local.vertices[0], Is.EqualTo(Vector3.right));
        } finally {
            Object.DestroyImmediate(root);
            Object.DestroyImmediate(origin);
            Object.DestroyImmediate(mesh);
        }
    }

    [TestCase(false)]
    [TestCase(true)]
    public void LargeOriginConversionWritesEveryVertexAndDirection(bool hasDirections) {
        const int vertexCount = 32771;
        var owner = new GameObject("Renderer");
        var origin = new GameObject("Origin");
        var mesh = new Mesh {
            vertices = Enumerable.Range(0, vertexCount).Select(i => new Vector3(i * 0.01f, i % 3, i % 7)).ToArray()
        };
        try {
            if (hasDirections) {
                mesh.normals = Enumerable.Repeat(Vector3.up * 2, vertexCount).ToArray();
                mesh.tangents = Enumerable.Repeat(new Vector4(3, 1, 0, -1), vertexCount).ToArray();
            }
            owner.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = owner.AddComponent<MeshRenderer>();
            owner.transform.SetPositionAndRotation(Vector3.forward, Quaternion.Euler(5, 10, 15));
            origin.transform.SetPositionAndRotation(Vector3.right, Quaternion.Euler(25, 30, 35));
            origin.transform.localScale = new Vector3(0.5f, 1.25f, 1.75f);
            var local = MeshBaker.BakeMesh(renderer);
            var matrix = Matrix4x4.Scale(origin.transform.lossyScale)
                * (origin.transform.worldToLocalMatrix * renderer.localToWorldMatrix);
            var converted = MeshBaker.BakeMesh(renderer, origin.transform, true);
            CollectionAssert.AreEqual(local.vertices.Select(matrix.MultiplyPoint3x4), converted.vertices);
            CollectionAssert.AreEqual(local.normals.Select(matrix.MultiplyVector), converted.normals);
            CollectionAssert.AreEqual(local.tangents.Select(matrix.MultiplyVector), converted.tangents);
            Assert.That(MeshBaker.BakeMesh(renderer), Is.SameAs(local));
        } finally {
            Object.DestroyImmediate(owner);
            Object.DestroyImmediate(origin);
            Object.DestroyImmediate(mesh);
        }
    }

    [Test]
    public void ReimportedMeshesInvalidateTheCache() {
        var path = "Assets/MeshBakerTest-" + Guid.NewGuid().ToString("N") + ".asset";
        var root = new GameObject("Renderer");
        var mesh = new Mesh { vertices = new[] { Vector3.right } };
        try {
            AssetDatabase.CreateAsset(mesh, path);
            root.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = root.AddComponent<MeshRenderer>();
            var initial = MeshBaker.BakeMesh(renderer);
            mesh.vertices = new[] { Vector3.up };
            EditorUtility.SetDirty(mesh);
            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            var imported = MeshBaker.BakeMesh(renderer);
            Assert.That(imported, Is.Not.SameAs(initial));
            Assert.That(imported.vertices[0], Is.EqualTo(Vector3.up));
        } finally {
            Object.DestroyImmediate(root);
            AssetDatabase.DeleteAsset(path);
        }
    }

    [TestCase(false)]
    [TestCase(true)]
    public void ReusesGeometryAndObservesMeshAndPoseChanges(bool skinned) {
        var root = new GameObject("Mesh baker test");
        var unrelated = new GameObject("Unrelated");
        var mesh = new Mesh {
            vertices = new[] { Vector3.right, Vector3.up, Vector3.forward },
            normals = new[] { Vector3.up, Vector3.up, Vector3.up },
            triangles = new[] { 0, 1, 2 }
        };
        try {
            var bone = new GameObject("Bone");
            bone.transform.SetParent(root.transform, false);
            Renderer renderer;
            if (skinned) {
                mesh.boneWeights = new[] {
                    new BoneWeight { boneIndex0 = 0, weight0 = 1 },
                    new BoneWeight { boneIndex0 = 0, weight0 = 1 },
                    new BoneWeight { boneIndex0 = 0, weight0 = 1 }
                };
                mesh.bindposes = new[] { Matrix4x4.identity };
                mesh.AddBlendShapeFrame("Test", 100, new[] { Vector3.up, Vector3.up, Vector3.up }, null, null);
                var skin = root.AddComponent<SkinnedMeshRenderer>();
                skin.sharedMesh = mesh;
                skin.bones = new[] { bone.transform };
                renderer = skin;
            } else {
                root.AddComponent<MeshFilter>().sharedMesh = mesh;
                renderer = root.AddComponent<MeshRenderer>();
            }
            var initial = MeshBaker.BakeMesh(renderer);
            Assert.That(MeshBaker.BakeMesh(renderer), Is.SameAs(initial));
            unrelated.transform.position = Vector3.one;
            EditorChanges.Changed(transformsOnly: true);
            Assert.That(MeshBaker.BakeMesh(renderer), Is.SameAs(initial));
            if (renderer is SkinnedMeshRenderer skinnedRenderer) {
                bone.transform.position = Vector3.right * .1f;
                var moved = MeshBaker.BakeMesh(renderer);
                Assert.That(moved, Is.Not.SameAs(initial));
                Assert.That(moved.vertices[0].x, Is.EqualTo(1.1f).Within(.00001f));
                skinnedRenderer.SetBlendShapeWeight(0, 100);
                var shaped = MeshBaker.BakeMesh(renderer);
                Assert.That(shaped.vertices[0].y, Is.EqualTo(1).Within(.00001f));
                Assert.That(initial.vertices[0].y, Is.Zero, "Previously returned snapshots must remain unchanged");
            }

            var vertices = mesh.vertices;
            vertices[0].z = .5f;
            mesh.vertices = vertices;
            var edited = MeshBaker.BakeMesh(renderer);
            Assert.That(edited.vertices[0].z, Is.EqualTo(.5f).Within(.00001f), "Observe in-place mesh edits");
            EditorUtility.SetDirty(mesh);
            Assert.That(MeshBaker.BakeMesh(renderer), Is.Not.SameAs(edited));
        } finally {
            Object.DestroyImmediate(root);
            Object.DestroyImmediate(unrelated);
            Object.DestroyImmediate(mesh);
        }
    }
}
