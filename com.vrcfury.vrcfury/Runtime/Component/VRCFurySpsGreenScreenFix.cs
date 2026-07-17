using System;
using UnityEngine;
using UnityEngine.SceneManagement;
using VF.VrcfEditorOnly;

namespace VF.Component {
    [AddComponentMenu("")]
    internal class VRCFurySpsGreenScreenFix : VRCFuryPlayComponent, IVrcfEditorOnly {
        private const string RootObjectName = "_SpsGreenScreenFix";
        private const string GrabShaderName = "Hidden/VRCFury/VFGridBakGrabPass";
        private const string RestoreShaderName = "Hidden/VRCFury/VFGridBakRestore";
        public static Action<MeshRenderer> onCreated;

        private void Start() {
            if (!Application.isPlaying) return;

            try {
                EnsureCameraExists();
                EnsureRootObject();
            } finally {
                DestroyImmediate(this);
            }
        }

        private static void EnsureCameraExists() {
            foreach (var root in SceneManager.GetActiveScene().GetRootGameObjects()) {
                foreach (var camera in root.GetComponentsInChildren<Camera>(true)) {
                    if (camera != null && camera.isActiveAndEnabled) return;
                }
            }

            var cameraObj = new GameObject("Scene Camera");
            SceneManager.MoveGameObjectToScene(cameraObj, SceneManager.GetActiveScene());
            cameraObj.AddComponent<Camera>();
        }

        private static void EnsureRootObject() {
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

            var meshFilter = obj.AddComponent<MeshFilter>();
            meshFilter.sharedMesh = CreateTriangleMesh();

            var renderer = obj.AddComponent<MeshRenderer>();
            renderer.sharedMaterials = new[] {
                new Material(grabShader),
                new Material(restoreShader)
            };
            onCreated?.Invoke(renderer);
        }

        private static Mesh CreateTriangleMesh() {
            var mesh = new Mesh {
                name = "VRCFurySpsGreenScreenFix"
            };
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
