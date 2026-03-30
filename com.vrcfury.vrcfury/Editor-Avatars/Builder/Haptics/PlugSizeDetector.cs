using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using VF.Builder.Exceptions;
using VF.Component;
using VF.Inspector;
using VF.Utils;

namespace VF.Builder.Haptics {
    internal static class PlugSizeDetector {
        public class SizeResult {
            public ICollection<Renderer> renderers;
            public float worldLength;
            public float worldRadius;
            public Quaternion localRotation;
            public Vector3 localPosition;
            public VFMultimapSet<VFGameObject, int> matSlots;
        }
        
        public static SizeResult GetWorldSize(VRCFuryHapticPlug plug) {
            var transform = plug.owner();
            var renderers = VRCFuryHapticPlugEditor.GetRenderers(plug);

            Quaternion worldRotation = transform.worldRotation;
            Vector3 worldPosition = transform.worldPosition;
            if (!plug.configureTps && !plug.enableSps && plug.autoPosition && renderers.Count > 0) {
                var firstRenderer = renderers.First();
                worldRotation = GetAutoWorldRotation(firstRenderer);
                worldPosition = GetAutoWorldPosition(firstRenderer);
            }
            var testBase = transform.Find("OGBTestBase");
            if (testBase != null) {
                worldPosition = testBase.worldPosition;
                worldRotation = testBase.worldRotation;
            }

            float worldLength = 0;
            float worldRadius = 0;
            VFMultimapSet<VFGameObject, int> matSlots = new VFMultimapSet<VFGameObject, int>();
            if (plug.autoRadius || plug.autoLength || renderers.Count > 0) {
                if (renderers.Count == 0) {
                    throw new VRCFBuilderException("Failed to find plug renderer");
                }
                var autoSize = GetAutoWorldSize(renderers, worldPosition, worldRotation, plug);
                if (autoSize != null) {
                    if (plug.autoLength) worldLength = autoSize.Item1;
                    if (plug.autoRadius) worldRadius = autoSize.Item2;
                    matSlots = autoSize.Item3;
                }
            }

            if (!plug.autoLength) {
                worldLength = plug.length;
                if (!plug.unitsInMeters) worldLength *= transform.worldScale.x;
            }
            if (!plug.autoRadius) {
                worldRadius = plug.radius;
                if (!plug.unitsInMeters) worldRadius *= transform.worldScale.x;
            }

            if (worldLength <= 0) throw new VRCFBuilderException("Failed to detect plug length");
            if (worldRadius <= 0) throw new VRCFBuilderException("Failed to detect plug radius");
            if (worldRadius > worldLength / 2) worldRadius = worldLength / 2;
            var localRotation = Quaternion.Inverse(transform.worldRotation) * worldRotation;
            var localPosition = transform.InverseTransformPoint(worldPosition);
            return new SizeResult {
                renderers = renderers,
                worldLength = worldLength,
                worldRadius = worldRadius,
                localRotation = localRotation,
                localPosition = localPosition,
                matSlots = matSlots
            };
        }

        public static Quaternion GetAutoWorldRotation(Renderer renderer) {
            var localRotation = GetMaterialDpsRotation(renderer) ?? Quaternion.identity;
            return HapticUtils.GetMeshRoot(renderer).worldRotation * localRotation;
        }
        
        public static Vector3 GetAutoWorldPosition(Renderer renderer) {
            return HapticUtils.GetMeshRoot(renderer).worldPosition;
        }

        public static Tuple<float, float, VFMultimapSet<VFGameObject,int>> GetAutoWorldSize(Renderer renderer) {
            return GetAutoWorldSize(
                new[] { renderer },
                GetAutoWorldPosition(renderer),
                GetAutoWorldRotation(renderer)
            );
        }

        public static Tuple<float, float, VFMultimapSet<VFGameObject,int>> GetAutoWorldSize(
            ICollection<Renderer> renderers,
            Vector3 worldPosition,
            Quaternion worldRotation,
            VRCFuryHapticPlug plug = null
        ) {
            if (renderers.Count == 0) return null;
            var inverseWorldRotation = Quaternion.Inverse(worldRotation);

            var allLocalVerts = new List<Vector3>();
            var matsUsed = new VFMultimapSet<VFGameObject, int>();
            foreach (var renderer in renderers) {
                var bakedMesh = MeshBaker.BakeMesh(renderer);
                if (bakedMesh == null) continue;
                var mask = plug != null ? PlugMaskGenerator.GetMask(renderer, plug) : null;
                var matsUsedByVert = new VFMultimapSet<int, int>();
                var mesh = renderer.GetMesh();
                var matSlotsInMesh = 0;
                if (mesh != null) {
                    var matCount = matSlotsInMesh = mesh.subMeshCount;
                    for (var matI = 0; matI < matCount; matI++) {
                        foreach (var vert in mesh.GetTriangles(matI)) {
                            matsUsedByVert.Put(vert, matI);
                        }
                    }
                }

                var localVertsUsedInRenderer = bakedMesh.vertices
                    .Select(vert => renderer.owner().TransformPoint(vert))
                    .Select(v => inverseWorldRotation * (v - worldPosition))
                    .Select((v, i) => {
                        var isUsed = mask == null || mask[i] > 0;
                        isUsed &= v.z > 0;
                        return (v, i, isUsed);
                    });
                foreach (var (v, index, isUsed) in localVertsUsedInRenderer) {
                    if (!isUsed) continue;
                    foreach (var matI in matsUsedByVert.Get(index)) {
                        matsUsed.Put(renderer.owner(), matI);
                    }
                    allLocalVerts.Add(v);
                }
                // Mat slots in the renderer that are higher than the number of mats on the mesh reuse the same verts as the last mesh mat
                if (matsUsed.Get(renderer.owner()).Contains(matSlotsInMesh-1)) {
                    for (var i = matSlotsInMesh; i < renderer.sharedMaterials.Length; i++) {
                        matsUsed.Put(renderer.owner(), i);
                    }
                }
            }

            var length = allLocalVerts
                .Select(v => v.z)
                .DefaultIfEmpty(0)
                .Max();
            var radius = allLocalVerts
                .Select(v => Vector3.Cross(v, Vector3.forward).magnitude)
                .OrderBy(m => m)
                .Where((m, i) => i <= allLocalVerts.Count*0.75)
                .DefaultIfEmpty(0)
                .Max();

            if (length <= 0 || radius <= 0) return null;

            return Tuple.Create(length, radius, matsUsed);
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
