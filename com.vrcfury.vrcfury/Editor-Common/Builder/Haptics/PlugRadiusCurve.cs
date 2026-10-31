using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using VF.Component;
using VF.Utils;

namespace VF.Builder.Haptics {
    internal static class PlugRadiusCurve {
        public static AnimationCurve GetRadiusCurve(VRCFuryHapticPlug plug) {
            return GetRadiusCurve(plug, PlugSizeDetector.GetWorldSize(plug));
        }

        public static AnimationCurve GetRadiusCurve(VRCFuryHapticPlug plug, PlugSizeDetector.SizeResult size) {
            var position = plug.owner().TransformPoint(size.localPosition);
            var inverseRotation = Quaternion.Inverse(plug.owner().worldRotation * size.localRotation);
            var boundaryTolerance = size.worldLength * 0.00001f;
            var vertices = new List<Vector3>();
            var triangles = new List<int>();
            foreach (var renderer in size.renderers) {
                var baked = MeshBaker.BakeMesh(renderer);
                if (baked == null) continue;
                var mask = PlugMaskGenerator.GetMask(renderer, plug);
                var offset = vertices.Count;
                vertices.AddRange(baked.vertices.Select(v => {
                    var point = inverseRotation * (renderer.owner().TransformPoint(v) - position);
                    // Roundoff in the world-to-plug transform must not drop one side of a
                    // cross-section that lies exactly on the root or tip plane.
                    if (Mathf.Abs(point.z) <= boundaryTolerance) point.z = 0;
                    else if (Mathf.Abs(point.z - size.worldLength) <= boundaryTolerance) point.z = size.worldLength;
                    return point;
                }));
                var mesh = renderer.GetMesh();
                for (var submesh = 0; submesh < mesh.subMeshCount; submesh++) {
                    if (mesh.GetTopology(submesh) != MeshTopology.Triangles) continue;
                    var indices = mesh.GetTriangles(submesh);
                    for (var i = 0; i < indices.Length; i += 3) {
                        // Don't let fully or partly excluded body triangles inflate the plug envelope.
                        if (Enumerable.Range(0, 3).Any(j => mask.GetOrDefault(indices[i + j], 1) <= 0)) continue;
                        for (var j = 0; j < 3; j++) triangles.Add(offset + indices[i + j]);
                    }
                }
            }
            return Fit(vertices.ToArray(), triangles.ToArray(), size.worldLength);
        }

        // Input is in the plug's world-unit frame (+Z along the plug). Both curve axes are meters.
        // Slice triangle edges rather than binning vertices: even a sparsely tessellated cone has a profile.
        public static AnimationCurve Fit(Vector3[] vertices, int[] triangles, float length) {
            if (!(length > 0) || float.IsInfinity(length)) throw new ArgumentOutOfRangeException(nameof(length));
            var depths = vertices.Select(v => v.z).Where(z => z >= 0 && z <= length)
                .Concat(Enumerable.Range(0, 129).Select(i => length * (i / 128f)))
                .Distinct().OrderBy(z => z).ToArray();
            var extents = new Vector4[depths.Length];
            void Include(int index, Vector3 point) {
                // Right, left, up, down distances from the plug axis. A missing side contributes 0.
                extents[index] = Vector4.Max(extents[index], new Vector4(point.x, -point.x, point.y, -point.y));
            }
            void Edge(Vector3 a, Vector3 b) {
                if (a.z > b.z) (a, b) = (b, a);
                var start = Array.BinarySearch(depths, a.z);
                if (start < 0) start = ~start;
                for (var i = start; i < depths.Length && depths[i] <= b.z; i++) {
                    if (a.z == b.z) {
                        Include(i, a);
                        Include(i, b);
                    } else {
                        Include(i, Vector3.LerpUnclamped(a, b, (depths[i] - a.z) / (b.z - a.z)));
                    }
                }
            }
            // Each directional extreme of a triangle's slice occurs at a slice endpoint (mesh edge).
            var edges = new HashSet<(int, int)>();
            for (var i = 0; i < triangles.Length; i += 3) {
                for (var j = 0; j < 3; j++) {
                    var a = triangles[i + j];
                    var b = triangles[i + (j + 1) % 3];
                    if (edges.Add((Math.Min(a, b), Math.Max(a, b)))) Edge(vertices[a], vertices[b]);
                }
            }

            var radii = extents.Select(e => (e.x + e.y + e.z + e.w) * 0.25f).ToArray();

            // Reduce keys with a maximum sampled radial error of 0.5% of the largest averaged radius.
            var tolerance = radii.Max() * 0.005f;
            var kept = new SortedSet<int> { 0, depths.Length - 1 };
            var pending = new Stack<(int, int)>();
            pending.Push((0, depths.Length - 1));
            while (pending.Count > 0) {
                var (first, last) = pending.Pop();
                var worst = -1;
                var error = tolerance;
                for (var i = first + 1; i < last; i++) {
                    var estimate = Mathf.Lerp(radii[first], radii[last],
                        (depths[i] - depths[first]) / (depths[last] - depths[first]));
                    var delta = Mathf.Abs(radii[i] - estimate);
                    if (delta <= error) continue;
                    error = delta;
                    worst = i;
                }
                if (worst < 0) continue;
                kept.Add(worst);
                pending.Push((first, worst));
                pending.Push((worst, last));
            }
            var keys = kept.Select(i => new Keyframe(depths[i], radii[i])).ToArray();
            for (var i = 1; i < keys.Length; i++) {
                var slope = (keys[i].value - keys[i - 1].value) / (keys[i].time - keys[i - 1].time);
                keys[i - 1].outTangent = slope;
                keys[i].inTangent = slope;
            }
            return new AnimationCurve(keys) { preWrapMode = WrapMode.ClampForever, postWrapMode = WrapMode.ClampForever };
        }
    }
}
