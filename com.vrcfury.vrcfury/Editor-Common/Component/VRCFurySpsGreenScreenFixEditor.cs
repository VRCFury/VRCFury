using System;
using UnityEngine;
using UnityEngine.SceneManagement;
using VF.Component;
using VF.Utils;

namespace VF {
    internal static class VRCFurySpsGreenScreenFixEditor {
        private const string RootObjectName = "_SpsGreenScreenFix";
        private const string GrabShaderName = "Hidden/VRCFury/VFGridBakGrabPass";
        private const string RestoreShaderName = "Hidden/VRCFury/VFGridBakRestore";

        [VFInit]
        private static void Init() {
            VRCFurySpsGreenScreenFix.onCreated = EnsureRootObject;
        }

        private static void EnsureRootObject() {
            VRCFuryBuildContext.Run(() => {
                var scene = SceneManager.GetActiveScene();
                if (!scene.IsValid()) return;

                foreach (var root in scene.GetRootGameObjects()) {
                    if (root != null && root.name == RootObjectName) return;
                }

                var grabShader = Shader.Find(GrabShaderName);
                var restoreShader = Shader.Find(RestoreShaderName);
                if (grabShader == null || restoreShader == null) {
                    throw new Exception("Failed to find SPS greenscreen fix shaders");
                }

                var obj = new GameObject(RootObjectName);
                SceneManager.MoveGameObjectToScene(obj, scene);

                var mesh = CreateTriangleMesh();
                obj.AddComponent<MeshFilter>().sharedMesh = mesh;

                var materials = new[] {
                    VrcfObjectFactory.CreateMaterial(grabShader),
                    VrcfObjectFactory.CreateMaterial(restoreShader)
                };
                obj.AddComponent<MeshRenderer>().sharedMaterials = materials;

                var session = new SaveAssetsSession("SpsGreenScreenFix");
                session.SaveAssetAndChildren(mesh);
                session.SaveAssetAndChildren(materials[0]);
                session.SaveAssetAndChildren(materials[1]);
                session.Finish();
            });
        }

        private static Mesh CreateTriangleMesh() {
            var mesh = VrcfObjectFactory.Create<Mesh>();
            mesh.name = "VRCFurySpsGreenScreenFix";
            mesh.vertices = new[] {
                new Vector3(-0.005f, -0.005f, 0),
                new Vector3(-0.005f, 0.005f, 0),
                new Vector3(0.005f, 0.005f, 0)
            };
            mesh.uv = new[] {
                new Vector2(0, 0),
                new Vector2(1, 0),
                new Vector2(0, 1)
            };
            mesh.triangles = new[] { 0, 1, 2 };
            mesh.bounds = new Bounds(Vector3.zero, Vector3.one * 2000);
            return mesh;
        }
    }
}
