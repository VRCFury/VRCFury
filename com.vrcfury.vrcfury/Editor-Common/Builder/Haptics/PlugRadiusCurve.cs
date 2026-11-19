using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using VF.Component;
using VF.Utils;

namespace VF.Builder.Haptics {
    internal static class PlugRadiusCurve {
        // Number of equal intervals sampled from root to tip, in addition to sampling vertex depths.
        private const int SliceIntervals = 128;
        // Snap vertices this close to the root or tip plane (in plug lengths) to absorb transform roundoff.
        private const float BoundaryTolerance = 0.00001f;
        // Stop adding keys when the largest radius error is within this fraction of the peak radius.
        private const float RelativeRadiusTolerance = 0.02f;
        // Maximum number of keys in the fitted curve, including root and tip.
        private const int MaxSamples = 24;
        // Both the slice count and the triangle index count must reach this size to sample in parallel.
        private const int ParallelThreshold = 1024;
        // Limit parallel workers because each one allocates its own array of slice extents.
        private const int MaxWorkers = 8;

        public static AnimationCurve GetRadiusCurve(VRCFuryHapticPlug plug) {
            return GetRadiusCurve(plug, PlugSizeDetector.GetWorldSize(plug), plug.owner().worldScale.x);
        }

        // X is normalized plug length (0=root, 1=tip); Y is local radius.
        public static AnimationCurve GetRadiusCurve(VRCFuryHapticPlug plug, PlugSizeDetector.SizeResult size, float scale) {
            if (size.renderers.Count == 0) {
                var radius = size.worldRadius / scale;
                var lastSliceBeforeTip = 1f - 1f / SliceIntervals;
                return FitSamples(new[] { 0f, lastSliceBeforeTip, 1f }, new[] { radius, radius, 0f });
            }
            var position = plug.owner().TransformPoint(size.localPosition);
            var worldToPlug = Matrix4x4.TRS(
                position,
                plug.owner().worldRotation * size.localRotation,
                Vector3.one
            ).inverse;
            var vertices = new List<Vector3>();
            var triangles = new List<int>();
            var maskBoundaries = new List<(float min, float max)>();
            foreach (var renderer in size.renderers) {
                var baked = MeshBaker.BakeMesh(renderer);
                if (baked == null) continue;
                var mask = PlugMaskGenerator.GetMask(renderer, plug);
                var toPlug = worldToPlug * renderer.localToWorldMatrix;
                var remap = new int[baked.vertices.Length];
                for (var i = 0; i < remap.Length; i++) remap[i] = -1;
                int MapVertex(int index) {
                    if (remap[index] >= 0) return remap[index];
                    var v = baked.vertices[index];
                    var point = toPlug.MultiplyPoint3x4(v);
                    point.x /= scale;
                    point.y /= scale;
                    point.z /= size.worldLength;
                    if (Mathf.Abs(point.z) <= BoundaryTolerance) point.z = 0;
                    else if (Mathf.Abs(point.z - 1) <= BoundaryTolerance) point.z = 1;
                    remap[index] = vertices.Count;
                    vertices.Add(point);
                    return remap[index];
                }
                var mesh = renderer.GetMesh();
                bool Included(int index) => mask.GetOrDefault(index, 1) >= PlugMaskGenerator.MinimumRadiusWeight;
                for (var submesh = 0; submesh < mesh.subMeshCount; submesh++) {
                    if (mesh.GetTopology(submesh) != MeshTopology.Triangles) continue;
                    var indices = mesh.GetTriangles(submesh);
                    for (var i = 0; i < indices.Length; i += 3) {
                        var a = indices[i];
                        var b = indices[i + 1];
                        var c = indices[i + 2];
                        var included = Included(a) && Included(b) && Included(c);
                        a = MapVertex(a);
                        b = MapVertex(b);
                        c = MapVertex(c);
                        if (included) {
                            triangles.Add(a);
                            triangles.Add(b);
                            triangles.Add(c);
                        } else {
                            var z = new Vector3(vertices[a].z, vertices[b].z, vertices[c].z);
                            maskBoundaries.Add((Math.Min(z.x, Math.Min(z.y, z.z)), Math.Max(z.x, Math.Max(z.y, z.z))));
                        }
                    }
                }
            }
            return Fit(vertices.ToArray(), triangles.ToArray(), maskBoundaries);
        }

        // Input X/Y are local distances from the plug axis; Z is normalized depth.
        // Slice triangle edges rather than binning vertices: even a sparsely tessellated cone has a profile.
        public static AnimationCurve Fit(
            Vector3[] vertices,
            int[] triangles,
            List<(float min, float max)> maskBoundaries = null
        ) {
            // Include vertex depths to preserve narrow bulges between the regular slices.
            var depthSet = new HashSet<float>();
            for (var i = 0; i <= SliceIntervals; i++) depthSet.Add((float)i / SliceIntervals);
            foreach (var index in triangles) {
                var z = vertices[index].z;
                if (z >= 0 && z <= 1) depthSet.Add(z);
            }
            var depths = depthSet.OrderBy(z => z).ToArray();
            var radii = SampleRadii(vertices, triangles, depths);

            var keptDepths = new List<float> { 0 };
            var keptRadii = new List<float> { 0 };
            var next = 0;
            var maskedThrough = float.NegativeInfinity;
            maskBoundaries?.Sort();
            for (var i = 0; i < depths.Length - 1; i++) {
                var depth = depths[i];
                while (maskBoundaries != null && next < maskBoundaries.Count && maskBoundaries[next].min <= depth) {
                    maskedThrough = Math.Max(maskedThrough, maskBoundaries[next++].max);
                }
                if (depth <= maskedThrough || radii[i] == 0) continue;
                if (keptRadii[0] == 0) keptRadii[0] = radii[i];
                if (depth == 0) continue;
                keptDepths.Add(depth);
                keptRadii.Add(radii[i]);
            }
            keptDepths.Add(1);
            keptRadii.Add(0);
            return FitSamples(keptDepths.ToArray(), keptRadii.ToArray());
        }

        private static float[] SampleRadii(Vector3[] vertices, int[] triangles, float[] depths) {
            var last = depths.Length - 1;
            var slice = new Dictionary<float, int>(depths.Length);
            for (var i = 0; i <= last; i++) slice.Add(depths[i], i);

            var workers = depths.Length >= ParallelThreshold && triangles.Length >= ParallelThreshold
                ? Math.Min(Environment.ProcessorCount, MaxWorkers) : 1;
            var byWorker = new Vector4[workers][];
            void Sample(int worker) {
                var extents = byWorker[worker] = new Vector4[depths.Length];
                void AddEdge(Vector3 a, Vector3 b) {
                    if (a.z > b.z) (a, b) = (b, a);
                    if (b.z < 0 || a.z > 1) return;
                    var start = a.z <= 0 ? 0 : slice[a.z];
                    if (a.z == b.z) {
                        Include(ref extents[start], a.x, a.y);
                        Include(ref extents[start], b.x, b.y);
                        return;
                    }
                    var end = b.z >= 1 ? last : slice[b.z];
                    for (var i = start; i <= end; i++) {
                        var t = (depths[i] - a.z) / (b.z - a.z);
                        Include(ref extents[i], a.x + (b.x - a.x) * t, a.y + (b.y - a.y) * t);
                    }
                }

                // A triangle's widest points in each slice are on its edges.
                var count = triangles.Length / 3;
                for (var i = count * worker / workers * 3; i < count * (worker + 1) / workers * 3; i += 3) {
                    var a = vertices[triangles[i]];
                    var b = vertices[triangles[i + 1]];
                    var c = vertices[triangles[i + 2]];
                    AddEdge(a, b);
                    AddEdge(b, c);
                    AddEdge(c, a);
                }
            }
            if (workers == 1) Sample(0);
            else Parallel.For(0, workers, Sample);

            var radii = new float[depths.Length];
            for (var i = 0; i <= last; i++) {
                var e = byWorker[0][i];
                for (var w = 1; w < workers; w++) e = Vector4.Max(e, byWorker[w][i]);
                radii[i] = (e.x + e.y + e.z + e.w) * 0.25f;
            }
            return radii;
        }

        // Right, left, up, down distances from the plug axis.
        private static void Include(ref Vector4 extent, float x, float y) {
            if (x > extent.x) extent.x = x;
            if (-x > extent.y) extent.y = -x;
            if (y > extent.z) extent.z = y;
            if (-y > extent.w) extent.w = -y;
        }

        private static AnimationCurve FitSamples(float[] depths, float[] radii) {
            var last = radii.Length - 1;
            var tolerance = radii.Max() * RelativeRadiusTolerance;
            var kept = new SortedSet<int> { 0, last };
            var splits = new List<(int start, int end, int key, float error)>();

            // Each straight section needs at most one candidate: its worst-fitting sample.
            void CheckSection(int start, int end) {
                var key = -1;
                var error = tolerance;
                for (var i = start + 1; i < end; i++) {
                    var t = (depths[i] - depths[start]) / (depths[end] - depths[start]);
                    var difference = Mathf.Abs(radii[i] - Mathf.Lerp(radii[start], radii[end], t));
                    if (difference <= error) continue;
                    key = i;
                    error = difference;
                }
                if (key >= 0) splits.Add((start, end, key, error));
            }

            CheckSection(0, last);
            while (splits.Count > 0 && kept.Count < MaxSamples) {
                var worst = splits.OrderByDescending(s => s.error).ThenBy(s => s.key).First();
                splits.Remove(worst);
                kept.Add(worst.key);
                CheckSection(worst.start, worst.key);
                CheckSection(worst.key, worst.end);
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
