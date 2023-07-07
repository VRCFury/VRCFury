using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using UnityEngine;
using VF.Builder.Exceptions;
using VF.Model;

namespace VF.Builder.Haptics {
    internal static class PlugSizeDetector {
        private static readonly int Poi7PenetratorEnabled = Shader.PropertyToID("_PenetratorEnabled");

        public static IImmutableList<Renderer> GetAutoRenderer(GameObject obj) {
            var foundWithDps = GetAutoRenderer(obj, true);
            if (foundWithDps.Count > 0) return foundWithDps;
            return GetAutoRenderer(obj, false);
        }
        
        private static IImmutableList<Renderer> GetAutoRenderer(GameObject obj, bool dpsOnly) {
            bool IsDps(Renderer r) => !dpsOnly || HasDpsMaterial(r);

            var foundOnObject = obj.GetComponents<Renderer>().Where(IsDps).ToImmutableList();
            if (foundOnObject.Count > 0) return foundOnObject;

            var foundInChildren = obj.GetComponentsInChildren<Renderer>(true).Where(IsDps).ToImmutableList();
            if (foundInChildren.Count > 0) return foundInChildren;
            
            var parent = obj.transform.parent;
            while (parent != null) {
                var foundOnParent = Enumerable.Range(0, parent.childCount)
                    .Select(childNum => parent.GetChild(childNum))
                    .SelectMany(child => child.GetComponents<Renderer>().Where(IsDps))
                    .ToImmutableList();
                if (foundOnParent.Count > 0) return foundOnParent;
                parent = parent.parent;
            }

            return new ImmutableArray<Renderer>();
        }

        public static Quaternion GetAutoWorldRotation(Renderer renderer) {
            var localRotation = GetMaterialDpsRotation(renderer) ?? Quaternion.identity;
            return HapticUtils.GetMeshRoot(renderer).rotation * localRotation;
        }
        
        public static Vector3 GetAutoWorldPosition(Renderer renderer) {
            return HapticUtils.GetMeshRoot(renderer).transform.position;
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
            if (mat.HasProperty(Poi7PenetratorEnabled) && mat.GetFloat(Poi7PenetratorEnabled) > 0) return Quaternion.identity; // Poiyomi 7 w/ Raliv
            if (mat.shader.name.Contains("DPS") && mat.HasProperty("_ReCurvature")) return Quaternion.identity; // UnityChanToonShader w/ Raliv
            if (TpsConfigurer.IsTps(mat)) {
                // Poiyomi 8 w/ TPS
                if (mat.HasProperty(TpsConfigurer.TpsPenetratorForward)) {
                    var c = mat.GetVector(TpsConfigurer.TpsPenetratorForward);
                    return Quaternion.LookRotation(new Vector3(c.x, c.y, c.z));
                }
                return Quaternion.identity;
            }
            return null;
        }
    }
}
