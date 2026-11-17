using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using VF.Utils;

namespace VF.Builder {
    internal static class MeshBaker {
        private class Cache {
            public Mesh mesh;
            public int meshVersion;
            public int rendererVersion;
            public long editorVersion = -1;
            public string assetPath;
            public int assetVersion;
            public Transform rootBone;
            public (Transform transform, Matrix4x4 matrix)[] transforms;
            public BakedMesh baked;
        }

        private static readonly ConditionalWeakTable<Renderer, Cache> caches = new();
        private static readonly Dictionary<string, int> assetVersions = new();

        private class MeshImports : AssetPostprocessor {
            private static void OnPostprocessAllAssets(
                string[] imported,
                string[] deleted,
                string[] moved,
                string[] movedFrom
            ) {
                foreach (var path in imported.Concat(deleted).Concat(moved).Concat(movedFrom)) {
                    if (assetVersions.TryGetValue(path, out var version)) assetVersions[path] = version + 1;
                }
            }
        }

        private static BakedMesh GetLocalMesh(Renderer renderer) {
            var cache = caches.GetOrCreateValue(renderer);
            var mesh = renderer.GetMesh();
            var meshVersion = mesh != null ? EditorUtility.GetDirtyCount(mesh) : 0;
            var rendererVersion = EditorUtility.GetDirtyCount(renderer);
            var skin = renderer as SkinnedMeshRenderer;
            var rootBone = skin != null ? skin.rootBone : null;
            var unchanged = cache.editorVersion == EditorChanges.NonTransformVersion
                && ReferenceEquals(cache.mesh, mesh) && cache.meshVersion == meshVersion
                && cache.assetVersion == assetVersions.GetOrDefault(cache.assetPath)
                && cache.rendererVersion == rendererVersion && cache.rootBone == rootBone
                && cache.transforms.All(t => t.transform != null && t.transform.localToWorldMatrix == t.matrix);
            if (unchanged) return cache.baked;

            cache.baked = BakeLocalMesh(renderer);
            cache.mesh = mesh;
            cache.meshVersion = meshVersion;
            cache.rendererVersion = rendererVersion;
            cache.editorVersion = EditorChanges.NonTransformVersion;
            cache.assetPath = mesh != null ? AssetDatabase.GetAssetPath(mesh) : null;
            cache.assetVersion = string.IsNullOrEmpty(cache.assetPath)
                ? 0 : assetVersions.GetOrCreate(cache.assetPath, () => 0);
            cache.rootBone = rootBone;
            // Bone assignments dirty the renderer, but rootBone assignments and bone/ancestor
            // movement do not. Only read the allocating bones array when rebuilding dependencies.
            var bones = skin != null ? skin.bones : Array.Empty<Transform>();
            cache.transforms = (skin != null ? bones.Append(renderer.transform).Append(rootBone) : Enumerable.Empty<Transform>())
                .Where(t => t != null).Distinct().Select(t => (t, t.localToWorldMatrix)).ToArray();
            return cache.baked;
        }

        /**
         * Returns a baked mesh, where the vertices are in local space in relation to the renderer's transform.
         * This is true even for skinned meshes (where in other places, the offsets are commonly in relation to the root bone)
         * The returned arrays may be shared by cached bakes and must not be modified.
         */
        public static BakedMesh BakeMesh(Renderer renderer, Transform origin = null, bool applyScale = false) {
            var baked = GetLocalMesh(renderer);
            if (baked == null || origin == null) return baked;
            var conversion = origin.worldToLocalMatrix * renderer.localToWorldMatrix;
            if (applyScale) conversion = Matrix4x4.Scale(origin.lossyScale) * conversion;
            // Preserve magnitude: Poiyomi uses the world-space normal length as an effect scale.
            return new BakedMesh {
                vertices = baked.vertices.Select(v => conversion.MultiplyPoint3x4(v)).ToArray(),
                normals = baked.normals.Select(v => conversion.MultiplyVector(v)).ToArray(),
                tangents = baked.tangents.Select(v => conversion.MultiplyVector(v)).ToArray()
            };
        }

        private static BakedMesh BakeLocalMesh(Renderer renderer) {
            var mesh = renderer.GetMesh();
            var inverseScale = Vector3.one;
            Mesh temporaryMesh = null;
            try {
                if (renderer is SkinnedMeshRenderer skin) {
                    temporaryMesh = new Mesh();
                    skin.BakeMesh(temporaryMesh);

                    // Unweighted meshes are treated like static meshes, with no baked-in scale.
                    var actuallySkinned = mesh != null && mesh.HasVertexAttribute(VertexAttribute.BlendWeight);
                    if (actuallySkinned) {
                        var scale = skin.owner().worldScale;
                        inverseScale = new Vector3(1 / scale.x, 1 / scale.y, 1 / scale.z);
                    }

                    mesh = temporaryMesh;
                }

                if (mesh == null) return null;

                Vector3[] vertices = mesh.vertices;
                Vector3[] normals = mesh.normals;
                Vector3[] tangents = mesh.tangents.Select(t => new Vector3(t.x, t.y, t.z)).ToArray();
                // BakeMesh scales skinned vertices, normals, and tangents. Return them in renderer-local
                // space so the conversion above applies scale exactly once for every renderer type.
                if (!inverseScale.Equals(Vector3.one)) {
                    ApplyScale(vertices, inverseScale);
                    ApplyScale(normals, inverseScale);
                    ApplyScale(tangents, inverseScale);
                }
                return new BakedMesh() {
                    vertices = vertices,
                    normals = normals,
                    tangents = tangents
                };
            } finally {
                if (temporaryMesh != null) UnityEngine.Object.DestroyImmediate(temporaryMesh);
            }
        }

        private static void ApplyScale(Vector3[] verts, Vector3 scale) {
            for (var i = 0; i < verts.Length; i++) {
                verts[i].Scale(scale);
            }
        }

        public class BakedMesh {
            public Vector3[] vertices;
            public Vector3[] normals;
            public Vector3[] tangents;
        }
    }
}
