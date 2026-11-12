using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using VF.Component;
using VF.Exceptions;
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
        
        public static SizeResult GetWorldSize(VRCFuryHapticPlug plug) =>
            GetWorldSize(plug, PlugRendererFinder.GetRenderers(plug));

        public static SizeResult GetWorldSize(VRCFuryHapticPlug plug, ICollection<Renderer> renderers) {
            var transform = plug.owner();

            var localRotation = Quaternion.Euler(plug.rotation);
            var localPosition = plug.position;
            var worldRotation = transform.worldRotation * localRotation;
            var worldPosition = transform.TransformPoint(localPosition);

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
            return new SizeResult {
                renderers = renderers,
                worldLength = worldLength,
                worldRadius = worldRadius,
                localRotation = localRotation,
                localPosition = localPosition,
                matSlots = matSlots
            };
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
            var radii = new List<float>();
            var matsUsed = new VFMultimapSet<VFGameObject, int>();
            foreach (var renderer in renderers) {
                var bakedMesh = MeshBaker.BakeMesh(renderer);
                if (bakedMesh == null) continue;
                var mask = plug != null ? PlugMaskGenerator.GetMask(renderer, plug) : null;
                var rendererObject = renderer.owner();
                var localToWorld = renderer.transform.localToWorldMatrix;
                var usedVertices = new bool[bakedMesh.vertices.Length];
                for (var i = 0; i < bakedMesh.vertices.Length; i++) {
                    var v = inverseWorldRotation * (localToWorld.MultiplyPoint3x4(bakedMesh.vertices[i]) - worldPosition);
                    if (!(mask.GetOrDefault(i, 1) > 0 && v.z > 0)) continue;
                    usedVertices[i] = true;
                    allLocalVerts.Add(v);
                    if (mask.GetOrDefault(i, 1) >= PlugMaskGenerator.MinimumRadiusWeight) {
                        radii.Add(Vector3.Cross(v, Vector3.forward).magnitude);
                    }
                }
                var mesh = renderer.GetMesh();
                var matSlotsInMesh = 0;
                if (mesh != null) {
                    var matCount = matSlotsInMesh = mesh.subMeshCount;
                    for (var matI = 0; matI < matCount; matI++) {
                        if (mesh.GetTriangles(matI).Any(vert => usedVertices[vert])) {
                            matsUsed.Put(rendererObject, matI);
                        }
                    }
                }

                // Mat slots in the renderer that are higher than the number of mats on the mesh reuse the same verts as the last mesh mat
                if (matsUsed.Get(rendererObject).Contains(matSlotsInMesh-1)) {
                    for (var i = matSlotsInMesh; i < renderer.sharedMaterials.Length; i++) {
                        matsUsed.Put(rendererObject, i);
                    }
                }
            }

            var length = allLocalVerts
                .Select(v => v.z)
                .DefaultIfEmpty(0)
                .Max();
            var radius = radii
                .OrderBy(m => m)
                .Where((m, i) => i <= radii.Count*0.75)
                .DefaultIfEmpty(0)
                .Max();

            if (length <= 0 || radius <= 0) return null;

            return Tuple.Create(length, radius, matsUsed);
        }


    }
}
