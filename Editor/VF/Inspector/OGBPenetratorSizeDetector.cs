using System;
using System.Linq;
using UnityEngine;

namespace VF.Inspector {
    static class OGBPenetratorSizeDetector {
        public static Vector3? GetAutoForward(GameObject obj, bool directOnly = false) {
            var t = ForEachMesh(obj, directOnly, renderer => {
                var forward = GetMaterialDpsForward(renderer);
                return forward != null ? Tuple.Create(forward) : null;
            });
            return t?.Item1;
        }

        public static Tuple<float, float> GetAutoSize(GameObject obj, bool directOnly, Vector3? forward = null) {
            Vector3 f = forward ?? GetAutoForward(obj, directOnly) ?? Vector3.forward;
            return ForEachMesh(obj, directOnly, renderer => {
                return GetAutoSize(renderer, f);
            });
        }

        private static T ForEachMesh<T>(GameObject obj, bool directOnly, Func<Renderer, T> each) where T : class {
            foreach (var r in obj.GetComponents<Renderer>()) {
                if (GetMaterialDpsForward(r) == null) continue;
                var auto = each(r);
                if (auto != null) return auto;
            }
            if (directOnly) return null;
            foreach (var r in obj.GetComponentsInChildren<Renderer>(true)) {
                if (GetMaterialDpsForward(r) == null) continue;
                var auto = each(r);
                if (auto != null) return auto;
            }
            foreach (var r in obj.GetComponentsInChildren<Renderer>(true)) {
                if (GetMaterialDpsForward(r) != null) continue;
                var auto = each(r);
                if (auto != null) return auto;
            }
            return null;
        }

        private static Tuple<float, float> GetAutoSize(Renderer renderer, Vector3 forward) {
            if (renderer is MeshRenderer) {
                var meshFilter = renderer.GetComponent<MeshFilter>();
                if (!meshFilter || !meshFilter.sharedMesh) return null;
                return GetAutoSize(renderer.gameObject, meshFilter.sharedMesh, forward);
            }
            if (renderer is SkinnedMeshRenderer skin) {
                // If the skinned mesh doesn't have any bones attached, it's treated like a regular mesh and BakeMesh applies no transforms
                // So we have to skip calling BakeMesh, because otherwise we'd apply the inverse scale inappropriately and it would be too small.
                bool actuallySkinned = skin.bones.Any(b => b != null);
                Mesh mesh;
                // TODO: Is this actually right? Or is it just because we're not using rootBone's scale if it's set on the renderer
                if (actuallySkinned) {
                    var temporaryMesh = new Mesh();
                    skin.BakeMesh(temporaryMesh);
                    var verts = temporaryMesh.vertices;
                    var scale = skin.transform.lossyScale;
                    var inverseScale = new Vector3(1 / scale.x, 1 / scale.y, 1 / scale.z);
                    for (var i = 0; i < verts.Length; i++) {
                        verts[i].Scale(inverseScale);
                    }
                    temporaryMesh.vertices = verts;
                    mesh = temporaryMesh;
                } else {
                    mesh = skin.sharedMesh;
                }

                if (!mesh) return null;
                return GetAutoSize(skin.gameObject, mesh, forward);
            }
            return null;
        }

        private static Tuple<float, float> GetAutoSize(GameObject obj, Mesh mesh, Vector3 forward) {
            forward = forward.normalized;
            var worldScale = obj.transform.lossyScale.x;
            var length = mesh.vertices
                .Select(v => Vector3.Dot(v, forward))
                .DefaultIfEmpty(0)
                .Max() * worldScale;
            var verticesInFront = mesh.vertices
                .Where(v => Vector3.Dot(v, forward) > 0)
                .ToArray();
            var verticesInFrontCount = verticesInFront.Count();
            float radius = verticesInFront
                .Select(v => Vector3.Cross(v, forward).magnitude)
                .OrderBy(m => m)
                .Where((m, i) => i <= verticesInFrontCount*0.75)
                .DefaultIfEmpty(0)
                .Max() * worldScale;

            if (length <= 0 || radius <= 0) return null;

            return Tuple.Create(length, radius);
        }

        private static Vector3? GetMaterialDpsForward(Renderer r) {
            return r.sharedMaterials.Select(GetMaterialDpsForward).FirstOrDefault(c => c != null);
        }

        private static Vector3? GetMaterialDpsForward(Material mat) {
            if (mat == null) return null;
            if (!mat.shader) return null;
            if (mat.shader.name == "Raliv/Penetrator") return Vector3.forward; // Raliv
            if (mat.shader.name.Contains("XSToon") && mat.shader.name.Contains("Penetrator")) return Vector3.forward; // XSToon w/ Raliv
            if (mat.HasProperty("_PenetratorEnabled") && mat.GetFloat("_PenetratorEnabled") > 0) return Vector3.forward; // Poiyomi 7 w/ Raliv
            if (mat.shader.name.Contains("DPS") && mat.HasProperty("_ReCurvature")) return Vector3.forward; // UnityChanToonShader w/ Raliv
            if (mat.HasProperty("_TPSPenetratorEnabled") && mat.GetFloat("_TPSPenetratorEnabled") > 0) {
                // Poiyomi 8 w/ TPS
                if (mat.HasProperty("_TPS_PenetratorForward")) {
                    var c = mat.GetVector("_TPS_PenetratorForward");
                    return new Vector3(c.x, c.y, c.z).normalized;
                }
                return Vector3.forward;
            }
            return null;
        }
    }
}