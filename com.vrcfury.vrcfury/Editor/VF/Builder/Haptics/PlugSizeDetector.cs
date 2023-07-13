using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using UnityEngine;
using VF.Builder.Exceptions;
using VF.Model;

namespace VF.Builder.Haptics {
    internal static class PlugSizeDetector {
        public static Quaternion GetAutoWorldRotation(Renderer renderer) {
            var localRotation = GetMaterialDpsRotation(renderer) ?? Quaternion.identity;
            return HapticUtils.GetMeshRoot(renderer).rotation * localRotation;
        }
        
        public static Vector3 GetAutoWorldPosition(Renderer renderer) {
            return HapticUtils.GetMeshRoot(renderer).position;
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

        private static Quaternion? GetMaterialDpsRotation(Renderer r) {
            return r.sharedMaterials.Select(GetMaterialDpsRotation).FirstOrDefault(c => c != null);
        }

        private static Quaternion? GetMaterialDpsRotation(Material mat) {
            if (DpsConfigurer.IsDps(mat)) {
                return Quaternion.identity;
            }
            if (TpsConfigurer.IsTps(mat)) {
                return TpsConfigurer.GetTpsRotation(mat);
            }
            return null;
        }
    }
}
