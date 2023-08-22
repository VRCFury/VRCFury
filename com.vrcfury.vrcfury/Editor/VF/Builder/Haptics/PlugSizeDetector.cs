using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using VF.Builder.Exceptions;
using VF.Component;
using VF.Inspector;

namespace VF.Builder.Haptics {
    public static class PlugSizeDetector {
        public class SizeResult {
            public ICollection<Renderer> renderers;
            public float worldLength;
            public float worldRadius;
            public Quaternion localRotation;
            public Vector3 localPosition;
        }
        
        public static SizeResult GetWorldSize(VRCFuryHapticPlug plug) {
            var transform = plug.transform;
            var renderers = VRCFuryHapticPlugEditor.GetRenderers(plug);

            Quaternion worldRotation = transform.rotation;
            Vector3 worldPosition = transform.position;
            if (!plug.configureTps && !plug.enableSps && plug.autoPosition && renderers.Count > 0) {
                var firstRenderer = renderers.First();
                worldRotation = GetAutoWorldRotation(firstRenderer);
                worldPosition = GetAutoWorldPosition(firstRenderer);
            }
            var testBase = transform.Find("OGBTestBase");
            if (testBase != null) {
                worldPosition = testBase.position;
                worldRotation = testBase.rotation;
            }

            float worldLength = 0;
            float worldRadius = 0;
            if (plug.autoRadius || plug.autoLength) {
                if (renderers.Count == 0) {
                    throw new VRCFBuilderException("Failed to find plug renderer");
                }
                var autoSize = GetAutoWorldSize(renderers, worldPosition, worldRotation, plug);
                if (autoSize != null) {
                    if (plug.autoLength) worldLength = autoSize.Item1;
                    if (plug.autoRadius) worldRadius = autoSize.Item2;
                }
            }

            if (!plug.autoLength) {
                worldLength = plug.length;
                if (!plug.unitsInMeters) worldLength *= transform.lossyScale.x;
            }
            if (!plug.autoRadius) {
                worldRadius = plug.radius;
                if (!plug.unitsInMeters) worldRadius *= transform.lossyScale.x;
            }

            if (worldLength <= 0) throw new VRCFBuilderException("Failed to detect plug length");
            if (worldRadius <= 0) throw new VRCFBuilderException("Failed to detect plug radius");
            if (worldRadius > worldLength / 2) worldRadius = worldLength / 2;
            var localRotation = Quaternion.Inverse(transform.rotation) * worldRotation;
            var localPosition = transform.InverseTransformPoint(worldPosition);
            return new SizeResult {
                renderers = renderers,
                worldLength = worldLength,
                worldRadius = worldRadius,
                localRotation = localRotation,
                localPosition = localPosition
            };
        }

        public static Quaternion GetAutoWorldRotation(Renderer renderer) {
            var localRotation = GetMaterialDpsRotation(renderer) ?? Quaternion.identity;
            return HapticUtils.GetMeshRoot(renderer).rotation * localRotation;
        }
        
        public static Vector3 GetAutoWorldPosition(Renderer renderer) {
            return HapticUtils.GetMeshRoot(renderer).position;
        }

        public static Tuple<float, float> GetAutoWorldSize(Renderer renderer) {
            return GetAutoWorldSize(
                new[] { renderer },
                GetAutoWorldPosition(renderer),
                GetAutoWorldRotation(renderer)
            );
        }

        public static Tuple<float, float> GetAutoWorldSize(
            ICollection<Renderer> renderers,
            Vector3 worldPosition,
            Quaternion worldRotation,
            VRCFuryHapticPlug plug = null
        ) {
            if (renderers.Count == 0) return null;
            var inverseWorldRotation = Quaternion.Inverse(worldRotation);

            var allWorldVerts = renderers.SelectMany(renderer => {
                var bakedMesh = MeshBaker.BakeMesh(renderer);
                if (bakedMesh == null) return new Vector3[]{};
                var mask = plug ? PlugMaskGenerator.GetMask(renderer, plug) : null;
                return bakedMesh.vertices
                    .Select(vert => renderer.transform.TransformPoint(vert))
                    .Where((vert, i) => mask == null || mask[i] > 0);
            }).ToArray();

            var verts = allWorldVerts
                .Select(v => inverseWorldRotation * (v - worldPosition))
                .Where(v => v.z > 0)
                .ToArray();
            var length = verts
                .Select(v => v.z)
                .DefaultIfEmpty(0)
                .Max();
            var radius = verts
                .Select(v => Vector3.Cross(v, Vector3.forward).magnitude)
                .OrderBy(m => m)
                .Where((m, i) => i <= verts.Length*0.75)
                .DefaultIfEmpty(0)
                .Max();

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
