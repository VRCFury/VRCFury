using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using VF.Builder;
using VF.Builder.Exceptions;
using VF.Model;

namespace VF.Inspector {
    static class OGBPenetratorSizeDetector {
        public static Renderer GetAutoRenderer(GameObject obj) {
            return GetAutoRenderer(obj, true) ?? GetAutoRenderer(obj, false);
        }
        
        private static Renderer GetAutoRenderer(GameObject obj, bool dpsOnly) {
            bool IsDps(Renderer r) => !dpsOnly || HasDpsMaterial(r);
            Renderer Try(IEnumerable<Renderer> enumerable) {
                var arr = enumerable.ToArray();
                if (arr.Length > 1) {
                    throw new VRCFBuilderException(
                        "Penetrator found multiple possible meshes. Please specify mesh in component manually.");
                }
                return arr.Length == 1 ? arr[0] : null;
            }
            
            Renderer found;
                
            found = Try(obj.GetComponents<Renderer>().Where(IsDps));
            if (found) return found;

            found = Try(obj.GetComponentsInChildren<Renderer>(true).Where(IsDps));
            if (found) return found;
            
            var parent = obj.transform.parent;
            while (parent != null) {
                found = Try(Enumerable.Range(0, parent.childCount)
                    .Select(childNum => parent.GetChild(childNum))
                    .SelectMany(child => child.GetComponents<Renderer>().Where(IsDps)));
                if (found) return found;
                parent = parent.parent;
            }

            return null;
        }
        
        public static Quaternion GetAutoWorldRotation(Renderer renderer) {
            var localRotation = GetMaterialDpsRotation(renderer) ?? Quaternion.identity;
            return OGBUtils.GetMeshRoot(renderer).rotation * localRotation;
        }
        
        public static Vector3 GetAutoWorldPosition(Renderer renderer) {
            return OGBUtils.GetMeshRoot(renderer).transform.position;
        }

        public static Tuple<float, float> GetAutoWorldSize(Renderer renderer, Vector3? worldPosition_ = null, Quaternion? worldRotation_ = null) {
            Quaternion worldRotation = worldRotation_ ?? GetAutoWorldRotation(renderer);
            Vector3 worldPosition = worldPosition_ ?? GetAutoWorldPosition(renderer);
            
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

        public static bool HasDpsMaterial(Renderer r) {
            return GetMaterialDpsRotation(r) != null;
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
