using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using VF.Component;
using VF.Utils;

namespace VF.Builder.Haptics {
    internal static class PlugRadiusCurve {
        private const float RelativeRadiusTolerance = 0.02f;
        private const int MaxSamples = 24;

        public static AnimationCurve GetRadiusCurve(VRCFuryHapticPlug plug) {
            return GetRadiusCurve(plug, PlugSizeDetector.GetWorldSize(plug), plug.owner().worldScale.x);
        }

        // X is normalized plug length (0=root, 1=tip); Y is local radius.
        public static AnimationCurve GetRadiusCurve(VRCFuryHapticPlug plug, PlugSizeDetector.SizeResult size, float scale) {
            if (size.renderers.Count == 0) {
                var radius = size.worldRadius / scale;
                return FitSamples(new[] { 0f, 127f / 128, 1f }, new[] { radius, radius, 0f });
            }
            var position = plug.owner().TransformPoint(size.localPosition);
            var inverseRotation = Quaternion.Inverse(plug.owner().worldRotation * size.localRotation);
            const float boundaryTolerance = 0.00001f;
            var vertices = new List<Vector3>();
            var triangles = new List<int>();
            foreach (var renderer in size.renderers) {
                var baked = MeshBaker.BakeMesh(renderer);
                if (baked == null) continue;
                var mask = PlugMaskGenerator.GetMask(renderer, plug);
                var offset = vertices.Count;
                vertices.AddRange(baked.vertices.Select(v => {
                    var point = inverseRotation * (renderer.owner().TransformPoint(v) - position);
                    point.x /= scale;
                    point.y /= scale;
                    point.z /= size.worldLength;
                    // Roundoff in the world-to-plug transform must not drop one side of a
                    // cross-section that lies exactly on the root or tip plane.
                    if (Mathf.Abs(point.z) <= boundaryTolerance) point.z = 0;
                    else if (Mathf.Abs(point.z - 1) <= boundaryTolerance) point.z = 1;
                    return point;
                }));
                var mesh = renderer.GetMesh();
                for (var submesh = 0; submesh < mesh.subMeshCount; submesh++) {
                    if (mesh.GetTopology(submesh) != MeshTopology.Triangles) continue;
                    var indices = mesh.GetTriangles(submesh);
                    for (var i = 0; i < indices.Length; i += 3) {
                        // Don't let weakly influenced vertices inflate the plug envelope.
                        if (Enumerable.Range(0, 3).Any(j => mask.GetOrDefault(indices[i + j], 1) < PlugMaskGenerator.MinimumRadiusWeight)) continue;
                        for (var j = 0; j < 3; j++) triangles.Add(offset + indices[i + j]);
                    }
                }
            }
            return Fit(vertices.ToArray(), triangles.ToArray());
        }

        // Input X/Y are local distances from the plug axis; Z is normalized depth.
        // Slice triangle edges rather than binning vertices: even a sparsely tessellated cone has a profile.
        public static AnimationCurve Fit(Vector3[] vertices, int[] triangles) {
            var depths = vertices.Select(v => v.z).Where(z => z >= 0 && z <= 1)
                .Concat(Enumerable.Range(0, 129).Select(i => i / 128f))
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
            return FitSamples(depths, radii);
        }

        private static AnimationCurve FitSamples(float[] depths, float[] radii) {
            // Extend the first measured cross-section back to the base instead of pinching it closed.
            var firstMeasured = Array.FindIndex(radii, radius => radius > 0);
            for (var i = 0; i < firstMeasured; i++) radii[i] = radii[firstMeasured];
            // Always close the tip, including flat-ended meshes and renderer-free plugs.
            radii[radii.Length - 1] = 0;

            // Keep the most significant changes until the tolerance or sample limit is reached.
            var tolerance = radii.Max() * RelativeRadiusTolerance;
            var kept = new SortedSet<int> { 0, depths.Length - 1 };
            while (kept.Count < MaxSamples) {
                var worst = -1;
                var error = tolerance;
                var first = kept.Min;
                foreach (var last in kept.Skip(1)) {
                    for (var i = first + 1; i < last; i++) {
                        var estimate = Mathf.Lerp(
                            radii[first],
                            radii[last],
                            (depths[i] - depths[first]) / (depths[last] - depths[first])
                        );
                        var delta = Mathf.Abs(radii[i] - estimate);
                        if (delta <= error) continue;
                        error = delta;
                        worst = i;
                    }
                    first = last;
                }
                if (worst < 0) break;
                kept.Add(worst);
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
