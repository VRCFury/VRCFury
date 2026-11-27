using NUnit.Framework;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using VF.Hooks.UnityFixes;
using VF.Utils;

namespace VF.Tests {
    internal class Unity6RendererFixHookTest {
        [Test]
        public void SkinnedRendererStillDrawsAfterItsMeshAndMaterialAreSaved() {
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null) {
                Assert.Ignore("Requires a graphics device.");
            }

            var scene = EditorSceneManager.NewPreviewScene();
            var path = AssetDatabase.GenerateUniqueAssetPath("Assets/VF Renderer Save Test.asset");
            var target = RenderTexture.GetTemporary(16, 16, 24);
            var pixel = new Texture2D(1, 1);
            var material = VrcfObjectFactory.CreateMaterial(Shader.Find("Unlit/Color"));
            material.color = Color.red;
            Mesh mesh = null;
            try {
                var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
                SceneManager.MoveGameObjectToScene(quad, scene);
                mesh = VrcfObjectFactory.Register(Object.Instantiate(quad.GetComponent<MeshFilter>().sharedMesh));
                mesh.bindposes = new[] { Matrix4x4.identity };
                mesh.boneWeights = Enumerable.Repeat(new BoneWeight { weight0 = 1 }, mesh.vertexCount).ToArray();
                mesh.AddBlendShapeFrame("Test Shape", 100, new Vector3[mesh.vertexCount], null, null);
                Object.DestroyImmediate(quad.GetComponent<MeshRenderer>());
                Object.DestroyImmediate(quad.GetComponent<MeshFilter>());
                var renderer = quad.AddComponent<SkinnedMeshRenderer>();
                renderer.bones = new[] { quad.transform };
                renderer.sharedMesh = mesh;
                renderer.sharedMaterial = material;
                renderer.updateWhenOffscreen = true;
                renderer.SetBlendShapeWeight(0, 25);
                renderer.localBounds = new Bounds(Vector3.zero, Vector3.one * 2);
                var bounds = renderer.localBounds;

                var camera = new GameObject("Renderer test camera").AddComponent<Camera>();
                SceneManager.MoveGameObjectToScene(camera.gameObject, scene);
                camera.scene = scene;
                camera.transform.position = Vector3.back * 3;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = Color.black;
                camera.targetTexture = target;
                camera.enabled = false;

                AssertDrawsRed(camera, pixel);
                using (VRCFuryAssetDatabase.WithAssetEditing()) {
                    var container = VrcfObjectFactory.Create<BinaryContainer>();
                    VRCFuryAssetDatabase.SaveAsset(container, path);
                    VRCFuryAssetDatabase.AttachAsset(mesh, container);
                    VRCFuryAssetDatabase.AttachAsset(material, container);
                    AssetDatabase.SaveAssetIfDirty(container);
                    Unity6RendererFixHook.Register(renderer);
                }

                Assert.That(AssetDatabase.GetAssetPath(renderer.sharedMesh), Is.EqualTo(path));
                Assert.That(AssetDatabase.GetAssetPath(renderer.sharedMaterial), Is.EqualTo(path));
                Assert.That(renderer.GetBlendShapeWeight(0), Is.EqualTo(25));
                Assert.That(renderer.localBounds, Is.EqualTo(bounds));
                AssertDrawsRed(camera, pixel);
            } finally {
                EditorSceneManager.ClosePreviewScene(scene);
                RenderTexture.ReleaseTemporary(target);
                Object.DestroyImmediate(pixel);
                if (mesh != null && !mesh.IsSaved()) Object.DestroyImmediate(mesh);
                if (material != null && !material.IsSaved()) Object.DestroyImmediate(material);
                AssetDatabase.DeleteAsset(path);
            }
        }

        private static void AssertDrawsRed(Camera camera, Texture2D pixel) {
            camera.Render();
            var previous = RenderTexture.active;
            try {
                RenderTexture.active = camera.targetTexture;
                pixel.ReadPixels(new Rect(8, 8, 1, 1), 0, 0);
                var color = pixel.GetPixel(0, 0);
                Assert.That(color.r, Is.GreaterThan(.9f));
                Assert.That(color.g, Is.LessThan(.1f));
                Assert.That(color.b, Is.LessThan(.1f));
            } finally {
                RenderTexture.active = previous;
            }
        }
    }
}
