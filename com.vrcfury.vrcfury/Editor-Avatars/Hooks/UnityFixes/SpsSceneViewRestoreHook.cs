using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using VF.Utils;

#if UNITY_2022_1_OR_NEWER
namespace VF.Hooks.UnityFixes {
    internal static class SpsSceneViewRestoreHook {
        private const string RestoreShaderName = "Hidden/VRCFury/SpsSceneViewRestore";
        private const string CaptureBufferName = "VRCFury SceneView SPS Capture";
        private static readonly int SavedTexId = Shader.PropertyToID("_VRCFurySceneViewSpsSaved");

        private static CameraCapture? capture;
        private static Mesh fullscreenMesh;
        private static Material restoreMaterial;

        private struct CameraCapture {
            public Camera camera;
            public CommandBuffer capture;
            public RenderTexture rt;
            public int width;
            public int height;
        }

        [VFInit]
        private static void Init() {
            Camera.onPreCull += OnCameraPreCull;
            EditorApplication.update += OnEditorUpdate;
            AssemblyReloadEvents.beforeAssemblyReload += Cleanup;
            EditorApplication.quitting += Cleanup;
        }

        private static void OnCameraPreCull(Camera camera) {
            RenderForCamera(camera);
        }

        private static void OnEditorUpdate() {
            SceneView.RepaintAll();
        }

        private static void RenderForCamera(Camera camera) {
            if (camera == null || camera.cameraType != CameraType.SceneView) return;

            var width = Mathf.Max(camera.pixelWidth, 1);
            var height = Mathf.Max(camera.pixelHeight, 1);
            EnsureCapture(camera, width, height);

            EnsureResources();
            if (fullscreenMesh == null || restoreMaterial == null) return;
            if (capture == null || capture.Value.rt == null) return;

            var bounds = new Bounds(Vector3.zero, Vector3.one * 1000000);
            restoreMaterial.SetTexture(SavedTexId, capture.Value.rt);
            var restoreParams = new RenderParams(restoreMaterial) {
                camera = camera,
                worldBounds = bounds,
                shadowCastingMode = ShadowCastingMode.Off,
                receiveShadows = false,
                reflectionProbeUsage = ReflectionProbeUsage.Off,
                lightProbeUsage = LightProbeUsage.Off
            };
            Graphics.RenderMesh(restoreParams, fullscreenMesh, 0, Matrix4x4.identity);
        }

        private static void EnsureCapture(Camera camera, int width, int height) {
            if (capture != null
                && capture.Value.camera == camera
                && capture.Value.width == width
                && capture.Value.height == height) {
                return;
            }

            RemoveCapture();

            var rt = new RenderTexture(width, height, 0, RenderTextureFormat.ARGB32) {
                name = "VRCFurySceneViewSpsSaved",
                hideFlags = HideFlags.HideAndDontSave,
                useMipMap = false,
                autoGenerateMips = false,
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp
            };
            rt.Create();

            var captureBuffer = new CommandBuffer { name = CaptureBufferName };
            captureBuffer.Blit(BuiltinRenderTextureType.CameraTarget, new RenderTargetIdentifier(rt));

            camera.AddCommandBuffer(CameraEvent.BeforeForwardOpaque, captureBuffer);

            capture = new CameraCapture {
                camera = camera,
                capture = captureBuffer,
                rt = rt,
                width = width,
                height = height
            };
        }

        private static void RemoveCapture() {
            if (capture == null) return;
            var current = capture.Value;

            if (current.capture != null && current.camera != null) {
                current.camera.RemoveCommandBuffer(CameraEvent.BeforeForwardOpaque, current.capture);
                current.capture.Release();
            }
            if (current.rt != null) {
                current.rt.Release();
                Object.DestroyImmediate(current.rt);
            }

            capture = null;
        }

        private static void EnsureResources() {
            if (fullscreenMesh == null) {
                fullscreenMesh = new Mesh {
                    name = "VRCFurySceneViewFullscreenQuad",
                    hideFlags = HideFlags.HideAndDontSave
                };
                fullscreenMesh.vertices = new[] {
                    new Vector3(-1, -1, 0),
                    new Vector3(1, -1, 0),
                    new Vector3(1, 1, 0),
                    new Vector3(-1, 1, 0)
                };
                fullscreenMesh.uv = new[] {
                    new Vector2(0, 0),
                    new Vector2(1, 0),
                    new Vector2(1, 1),
                    new Vector2(0, 1)
                };
                fullscreenMesh.triangles = new[] { 0, 1, 2, 0, 2, 3 };
                fullscreenMesh.bounds = new Bounds(Vector3.zero, Vector3.one * 2);
            }

            if (restoreMaterial == null) {
                var shader = Shader.Find(RestoreShaderName);
                if (shader != null) {
                    restoreMaterial = new Material(shader) {
                        hideFlags = HideFlags.HideAndDontSave
                    };
                }
            }
        }

        private static void Cleanup() {
            Camera.onPreCull -= OnCameraPreCull;
            EditorApplication.update -= OnEditorUpdate;
            RemoveCapture();

            if (restoreMaterial != null) {
                Object.DestroyImmediate(restoreMaterial);
                restoreMaterial = null;
            }

            if (fullscreenMesh != null) {
                Object.DestroyImmediate(fullscreenMesh);
                fullscreenMesh = null;
            }
        }
    }
}
#endif
