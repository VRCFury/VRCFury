using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
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
            var worldToPlug = Matrix4x4.TRS(
                position,
                plug.owner().worldRotation * size.localRotation,
                Vector3.one
            ).inverse;
            const float boundaryTolerance = 0.00001f;
            var vertices = new List<Vector3>();
            var triangles = new List<int>();
            foreach (var renderer in size.renderers) {
                var baked = MeshBaker.BakeMesh(renderer);
                if (baked == null) continue;
                var mask = PlugMaskGenerator.GetMask(renderer, plug);
                var toPlug = worldToPlug * renderer.localToWorldMatrix;
                var remap = new int[baked.vertices.Length];
                for (var i = 0; i < remap.Length; i++) remap[i] = -1;
                void AddVertex(int index) {
                    if (remap[index] >= 0) {
                        triangles.Add(remap[index]);
                        return;
                    }
                    var v = baked.vertices[index];
                    var point = toPlug.MultiplyPoint3x4(v);
                    point.x /= scale;
                    point.y /= scale;
                    point.z /= size.worldLength;
                    // Roundoff in the world-to-plug transform must not drop one side of a
                    // cross-section that lies exactly on the root or tip plane.
                    if (Mathf.Abs(point.z) <= boundaryTolerance) point.z = 0;
                    else if (Mathf.Abs(point.z - 1) <= boundaryTolerance) point.z = 1;
                    remap[index] = vertices.Count;
                    triangles.Add(vertices.Count);
                    vertices.Add(point);
                }
                var mesh = renderer.GetMesh();
                for (var submesh = 0; submesh < mesh.subMeshCount; submesh++) {
                    if (mesh.GetTopology(submesh) != MeshTopology.Triangles) continue;
                    var indices = mesh.GetTriangles(submesh);
                    for (var i = 0; i < indices.Length; i += 3) {
                        // Don't let weakly influenced vertices inflate the plug envelope.
                        if (mask.GetOrDefault(indices[i], 1) < PlugMaskGenerator.MinimumRadiusWeight
                            || mask.GetOrDefault(indices[i + 1], 1) < PlugMaskGenerator.MinimumRadiusWeight
                            || mask.GetOrDefault(indices[i + 2], 1) < PlugMaskGenerator.MinimumRadiusWeight) continue;
                        for (var j = 0; j < 3; j++) AddVertex(indices[i + j]);
                    }
                }
            }
            return Fit(vertices.ToArray(), triangles.ToArray());
        }

        // Input X/Y are local distances from the plug axis; Z is normalized depth.
        // Slice triangle edges rather than binning vertices: even a sparsely tessellated cone has a profile.
        public static AnimationCurve Fit(Vector3[] vertices, int[] triangles) {
            var depths = GetSampleDepths(vertices, triangles);
            var radii = SampleRadii(vertices, triangles, depths);
            return FitSamples(depths, radii);
        }

        private static float[] GetSampleDepths(Vector3[] vertices, int[] triangles) {
            var referenced = new bool[vertices.Length];
            foreach (var index in triangles) referenced[index] = true;
            var depthSet = new HashSet<float>();
            for (var i = 0; i < vertices.Length; i++) {
                var z = vertices[i].z;
                if (referenced[i] && z >= 0 && z <= 1) depthSet.Add(z);
            }
            for (var i = 0; i <= 128; i++) depthSet.Add(i / 128f);
            var depths = depthSet.ToArray();
            Array.Sort(depths);
            return depths;
        }

        private static float[] SampleRadii(Vector3[] vertices, int[] triangles, float[] depths) {
            var extents = new Vector4[depths.Length];
            var segments = new (Vector3 a, Vector3 b, int first, int last)[triangles.Length];
            var workers = depths.Length >= 1024 && triangles.Length >= 1024
                ? Math.Min(Environment.ProcessorCount, 8) : 1;
            // Each directional extreme of a triangle's slice occurs at a slice endpoint (mesh edge).
            // Repeating a shared edge cannot change a maximum; avoid hashing every triangle edge.
            void Prepare(int worker) {
                var firstTriangle = triangles.Length / 3 * worker / workers;
                var lastTriangle = triangles.Length / 3 * (worker + 1) / workers;
                for (var i = firstTriangle * 3; i < lastTriangle * 3; i += 3) {
                    for (var edge = 0; edge < 3; edge++) {
                        var start = vertices[triangles[i + edge]];
                        var end = vertices[triangles[i + (edge + 1) % 3]];
                        if (start.z > end.z) (start, end) = (end, start);
                        var first = FindDepth(depths, start.z, false);
                        var last = FindDepth(depths, end.z, true) - 1;
                        segments[i + edge] = (start, end, first, last);
                    }
                }
            }

            // Slice ranges are independent. Workers only read mesh snapshots and write
            // their own extents, preserving edge order and avoiding locks or Unity object access.
            void Sample(int worker) {
                var first = depths.Length * worker / workers;
                var last = depths.Length * (worker + 1) / workers - 1;
                foreach (var segment in segments) {
                    if (segment.first > last || segment.last < first) continue;
                    var a = segment.a;
                    var b = segment.b;
                    var end = Math.Min(last, segment.last);
                    for (var i = Math.Max(first, segment.first); i <= end; i++) {
                        if (a.z == b.z) {
                            Include(ref extents[i], a.x, a.y);
                            Include(ref extents[i], b.x, b.y);
                        } else {
                            var t = (depths[i] - a.z) / (b.z - a.z);
                            Include(ref extents[i], a.x + (b.x - a.x) * t, a.y + (b.y - a.y) * t);
                        }
                    }
                }
            }
            if (workers == 1) {
                Prepare(0);
                Sample(0);
            } else {
                Parallel.For(0, workers, Prepare);
                Parallel.For(0, workers, Sample);
            }

            return extents.Select(e => (e.x + e.y + e.z + e.w) * 0.25f).ToArray();
        }

        // Right, left, up, down distances from the plug axis. A missing side contributes 0.
        private static void Include(ref Vector4 extent, float x, float y) {
            if (x > extent.x) extent.x = x;
            if (-x > extent.y) extent.y = -x;
            if (y > extent.z) extent.z = y;
            if (-y > extent.w) extent.w = -y;
        }

        // Lower/upper bounds without the generic comparer's per-comparison overhead.
        private static int FindDepth(float[] depths, float value, bool after) {
            var lo = 0;
            var hi = depths.Length;
            while (lo < hi) {
                var mid = lo + (hi - lo) / 2;
                if (depths[mid] < value || (after && depths[mid] == value)) lo = mid + 1;
                else hi = mid;
            }
            return lo;
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
            var errors = new Dictionary<(int first, int last), (int index, float error)>();
            while (kept.Count < MaxSamples) {
                var worst = -1;
                var error = tolerance;
                var first = kept.Min;
                foreach (var last in kept.Skip(1)) {
                    // Adding a key only changes its two neighboring intervals.
                    // Reuse the worst error for every interval whose endpoints stayed the same.
                    if (!errors.TryGetValue((first, last), out var interval)) {
                        interval = (-1, tolerance);
                        for (var i = first + 1; i < last; i++) {
                            var estimate = Mathf.Lerp(
                                radii[first],
                                radii[last],
                                (depths[i] - depths[first]) / (depths[last] - depths[first])
                            );
                            var delta = Mathf.Abs(radii[i] - estimate);
                            if (delta <= interval.error) continue;
                            interval = (i, delta);
                        }
                        errors[(first, last)] = interval;
                    }
                    if (interval.error > error) {
                        error = interval.error;
                        worst = interval.index;
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
