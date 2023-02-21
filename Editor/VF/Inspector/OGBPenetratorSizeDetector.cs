using System;
using System.Linq;
using UnityEngine;
using VF.Builder;
using VF.Model;

namespace VF.Inspector {
    static class OGBPenetratorSizeDetector {
        public static Quaternion? GetAutoWorldRotation(GameObject obj, bool directOnly = false) {
            var t = ForEachMesh(obj, directOnly, renderer => {
                var localRotation = GetMaterialDpsRotation(renderer);
                return localRotation != null ? Tuple.Create(OGBUtils.GetMeshRoot(renderer).rotation * localRotation) : null;
            });
            return t?.Item1;
        }
        
        public static Vector3? GetAutoWorldPosition(GameObject obj, bool directOnly = false) {
            var t = ForEachMesh(obj, directOnly, renderer => {
                return Tuple.Create(OGBUtils.GetMeshRoot(renderer).transform.position);
            });
            return t?.Item1;
        }

        public static Tuple<float, float> GetAutoWorldSize(GameObject obj, bool directOnly, Vector3? worldPosition_ = null, Quaternion? worldRotation_ = null) {
            Quaternion worldRotation = worldRotation_ ?? GetAutoWorldRotation(obj, directOnly) ?? obj.transform.rotation;
            Vector3 worldPosition = worldPosition_ ?? GetAutoWorldPosition(obj, directOnly) ?? obj.transform.position;
            return ForEachMesh(obj, directOnly, renderer => {
                return GetAutoWorldSize(renderer, worldPosition, worldRotation);
            });
        }

        private static T ForEachMesh<T>(GameObject obj, bool directOnly, Func<Renderer, T> each) where T : class {
            foreach (var r in obj.GetComponents<Renderer>()) {
                if (GetMaterialDpsRotation(r) == null) continue;
                var auto = each(r);
                if (auto != null) return auto;
            }
            if (directOnly) return null;
            foreach (var r in obj.GetComponentsInChildren<Renderer>(true)) {
                if (GetMaterialDpsRotation(r) == null) continue;
                var auto = each(r);
                if (auto != null) return auto;
            }
            foreach (var r in obj.GetComponentsInChildren<Renderer>(true)) {
                if (GetMaterialDpsRotation(r) != null) continue;
                var auto = each(r);
                if (auto != null) return auto;
            }
            return null;
        }

        private static Tuple<float, float> GetAutoWorldSize(Renderer renderer, Vector3 worldPosition, Quaternion worldRotation) {
            var bakedMesh = MeshBaker.BakeMesh(renderer);
            if (bakedMesh == null) return null;

            var localRotation = Quaternion.Inverse(renderer.transform.rotation) * worldRotation;
            var localForward = localRotation * Vector3.forward;
            var worldScale = renderer.transform.lossyScale.x;
            var localPosition = renderer.transform.InverseTransformPoint(worldPosition);
            var verts = bakedMesh.vertices
                .Select(v => v - localPosition)
                .ToArray();
            var length = verts
                .Select(v => Vector3.Dot(v, localForward))
                .DefaultIfEmpty(0)
                .Max() * worldScale;
            var vertsInFront = verts
                .Where(v => Vector3.Dot(v, localForward) > 0)
                .ToArray();
            var verticesInFrontCount = vertsInFront.Count();
            var radius = vertsInFront
                .Select(v => Vector3.Cross(v, localForward).magnitude)
                .OrderBy(m => m)
                .Where((m, i) => i <= verticesInFrontCount*0.75)
                .DefaultIfEmpty(0)
                .Max() * worldScale;

            if (length <= 0 || radius <= 0) return null;

            return Tuple.Create(length, radius);
        }

        private static Quaternion? GetMaterialDpsRotation(Renderer r) {
            return r.sharedMaterials.Select(GetMaterialDpsRotation).FirstOrDefault(c => c != null);
        }

        private static Quaternion? GetMaterialDpsRotation(Material mat) {
            if (mat == null) return null;
            if (!mat.shader) return null;
            if (mat.shader.name == "Raliv/Penetrator") return Quaternion.identity; // Raliv
            if (mat.shader.name.Contains("XSToon") && mat.shader.name.Contains("Penetrator")) return Quaternion.identity; // XSToon w/ Raliv
            if (mat.HasProperty("_PenetratorEnabled") && mat.GetFloat("_PenetratorEnabled") > 0) return Quaternion.identity; // Poiyomi 7 w/ Raliv
            if (mat.shader.name.Contains("DPS") && mat.HasProperty("_ReCurvature")) return Quaternion.identity; // UnityChanToonShader w/ Raliv
            if (mat.HasProperty("_TPSPenetratorEnabled") && mat.GetFloat("_TPSPenetratorEnabled") > 0) {
                // Poiyomi 8 w/ TPS
                if (mat.HasProperty("_TPS_PenetratorForward")) {
                    var c = mat.GetVector("_TPS_PenetratorForward");
                    return Quaternion.LookRotation(new Vector3(c.x, c.y, c.z));
                }
                return Quaternion.identity;
            }
            return null;
        }
    }
}
