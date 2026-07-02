using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using VF.Utils;

namespace VF.Builder.Haptics {
    internal static class SpsColorSampler {
        private const int PreviewSize = 128;
        private const float BoundsPadding = 0.05f;
        private const float AlphaThreshold = 0.001f;

        public static Color GetColor(IEnumerable<Renderer> renderers) {
            Renderer bestRenderer = null;
            var bestVertexCount = -1;

            foreach (var renderer in renderers ?? Enumerable.Empty<Renderer>()) {
                var vertexCount = renderer?.GetVertexCount() ?? 0;
                if (vertexCount <= bestVertexCount) continue;
                bestRenderer = renderer;
                bestVertexCount = vertexCount;
            }

            return GetColor(bestRenderer);
        }

        private static Color GetColor(Renderer renderer) {
            if (renderer == null) return Color.clear;

            var mesh = renderer.GetMesh();
            var materials = renderer.sharedMaterials ?? Array.Empty<Material>();
            if (mesh == null || mesh.vertexCount <= 0) {
                return GetColor(materials.FirstOrDefault(m => m != null), mesh);
            }

            Material bestMaterial = null;
            int bestSubmeshIndex = 0;
            int[] bestVertexIndexes = null;
            var bestVertexCount = -1;

            for (var materialIndex = 0; materialIndex < materials.Length; materialIndex++) {
                var material = materials[materialIndex];
                if (material == null) continue;

                var submeshIndex = Mathf.Min(materialIndex, Mathf.Max(mesh.subMeshCount - 1, 0));
                var vertexIndexes = GetSubmeshVertexIndexes(mesh, submeshIndex);
                if (vertexIndexes.Length <= bestVertexCount) continue;

                bestMaterial = material;
                bestSubmeshIndex = submeshIndex;
                bestVertexIndexes = vertexIndexes;
                bestVertexCount = vertexIndexes.Length;
            }

            return bestMaterial == null ? Color.clear : RenderAverage(bestMaterial, mesh, bestSubmeshIndex, bestVertexIndexes);
        }

        private static Color GetColor(Material material, Mesh mesh) {
            if (material == null) return Color.clear;
            if (mesh == null || mesh.vertexCount <= 0) return Color.clear;
            return RenderAverage(material, mesh, -1, GetAllVertexIndexes(mesh));
        }

        private static Color RenderAverage(Material material, Mesh mesh, int submeshIndex, int[] vertexIndexes) {
            if (material == null || mesh == null || vertexIndexes == null || vertexIndexes.Length <= 0) return Color.clear;

            var bounds = GetBounds(mesh, vertexIndexes);
            if (bounds.size.sqrMagnitude <= 0) return Color.clear;

            var preview = new PreviewRenderUtility();
            var previousActive = RenderTexture.active;

            try {
                var extents = bounds.extents + Vector3.one * BoundsPadding;
                preview.ambientColor = Color.white;
                preview.lights[0].color = Color.white;
                preview.lights[0].intensity = 1.25f;
                preview.lights[0].transform.rotation = Quaternion.LookRotation(Vector3.forward, Vector3.up);
                preview.lights[1].color = Color.white;
                preview.lights[1].intensity = 1.25f;
                preview.lights[1].transform.rotation = Quaternion.LookRotation(Vector3.back, Vector3.up);
                preview.camera.clearFlags = CameraClearFlags.SolidColor;
                preview.camera.backgroundColor = Color.clear;
                preview.camera.orthographic = true;
                preview.camera.allowHDR = true;
                preview.camera.aspect = 1;
                preview.camera.orthographicSize = Mathf.Max(extents.y, extents.x, 0.01f);
                var distance = extents.z + 1f;
                preview.camera.nearClipPlane = 0.01f;
                preview.camera.farClipPlane = distance * 2f + 1f;
                preview.camera.transform.position = bounds.center - Vector3.forward * distance;
                preview.camera.transform.rotation = Quaternion.identity;

                preview.BeginPreview(new Rect(0, 0, PreviewSize, PreviewSize), GUIStyle.none);
                var firstSubmesh = submeshIndex < 0 ? 0 : submeshIndex;
                var lastSubmesh = submeshIndex < 0 ? Mathf.Max(mesh.subMeshCount - 1, 0) : submeshIndex;
                for (var currentSubmesh = firstSubmesh; currentSubmesh <= lastSubmesh; currentSubmesh++) {
                    preview.DrawMesh(
                        mesh,
                        Matrix4x4.identity,
                        material,
                        currentSubmesh
                    );
                }
                preview.camera.Render();

                var rendered = preview.EndPreview() as RenderTexture;
                if (rendered == null) return Color.clear;
                RenderTexture.active = rendered;

                var readback = VrcfObjectFactory.CreateTexture2D(
                    PreviewSize,
                    PreviewSize,
                    TextureFormat.RGBAFloat,
                    false,
                    true
                );
                readback.name = "SpsColorSamplerPreview";
                readback.hideFlags = HideFlags.HideAndDontSave;
                readback.ReadPixels(new Rect(0, 0, PreviewSize, PreviewSize), 0, 0);
                readback.Apply();
                //SavePreviewTexture(readback);
                var pixels = readback.GetPixels();

                Color total = Color.clear;
                var count = 0;
                foreach (var pixel in pixels) {
                    if (pixel.a <= AlphaThreshold) continue;
                    total += pixel;
                    count++;
                }

                if (count <= 0) return Color.clear;
                var average = total / count;
                average.a = 1;
                return average;
            } finally {
                RenderTexture.active = previousActive;
                try {
                    preview.Cleanup();
                } catch (Exception e) {
                    Debug.LogException(e);
                }
            }
        }

        private static Bounds GetBounds(Mesh mesh, IEnumerable<int> vertexIndexes) {
            var vertices = mesh.vertices;
            var hasBounds = false;
            var bounds = new Bounds(Vector3.zero, Vector3.zero);

            foreach (var index in vertexIndexes) {
                if (index < 0 || index >= vertices.Length) continue;
                if (!hasBounds) {
                    bounds = new Bounds(vertices[index], Vector3.zero);
                    hasBounds = true;
                } else {
                    bounds.Encapsulate(vertices[index]);
                }
            }

            return hasBounds ? bounds : new Bounds(Vector3.zero, Vector3.zero);
        }

        private static int[] GetSubmeshVertexIndexes(Mesh mesh, int submeshIndex) {
            if (mesh == null || mesh.vertexCount <= 0) return Array.Empty<int>();
            if (mesh.subMeshCount <= 0) return GetAllVertexIndexes(mesh);

            var used = new HashSet<int>();
            foreach (var index in mesh.GetTriangles(Mathf.Clamp(submeshIndex, 0, mesh.subMeshCount - 1))) {
                if (index < 0 || index >= mesh.vertexCount) continue;
                used.Add(index);
            }
            return used.ToArray();
        }

        private static int[] GetAllVertexIndexes(Mesh mesh) {
            if (mesh == null || mesh.vertexCount <= 0) return Array.Empty<int>();
            return Enumerable.Range(0, mesh.vertexCount).ToArray();
        }

        private static void SavePreviewTexture(Texture2D texture) {
            var tmpPath = TmpFilePackage.GetPathNullable();
            if (string.IsNullOrEmpty(tmpPath)) return;

            var dir = tmpPath + "/Builds/SpsColorSampler";
            var path = dir + "/LastPreview.asset";
            VRCFuryAssetDatabase.CreateFolder(dir);
            VRCFuryAssetDatabase.Delete(path);
            texture.hideFlags = HideFlags.None;
            VRCFuryAssetDatabase.SaveAsset(texture, path);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
        }
    }
}
