using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using VF.Exceptions;
using VF.Utils;

namespace VF.Builder.Haptics {
    internal static class SpsBaker {
        public const int ResolverRadiusSampleCount = 16;
        private const float MinSampleRadius = 0.0001f;

        public class RendererBakeInput {
            public Renderer renderer;
            public float[] activeFromMask;
        }

        public static Texture2D Bake(
            Renderer renderer,
            Transform origin,
            float[] activeFromMask,
            bool tpsCompatibility,
            ICollection<string> spsBlendshapes = null
        ) {
            var bakedMesh = MeshBaker.BakeMesh(renderer, origin, !tpsCompatibility);
            if (bakedMesh == null)
                throw new VRCFBuilderException("Failed to bake mesh for SPS configuration");

            var baked = new SpsBakedTexture(tpsCompatibility);

            // version
            if (!tpsCompatibility) {
                baked.WriteColor(0, 0, 0, 0);
            }

            float GetActive(int i) {
                return activeFromMask == null ? 1 : activeFromMask[i];
            }

            for (var i = 0; i < bakedMesh.vertices.Length; i++) {
                baked.WriteVector3(bakedMesh.vertices[i]);

                if (tpsCompatibility) {
                    if (GetActive(i) == 0) {
                        baked.WriteVector3(new Vector3(0,0,0));
                    } else {
                        baked.WriteVector3(bakedMesh.normals[i]);
                    }
                } else {
                    baked.WriteVector3(i < bakedMesh.normals.Length ? bakedMesh.normals[i] : Vector3.zero);
                    baked.WriteVector3(i < bakedMesh.tangents.Length ? bakedMesh.tangents[i] : Vector3.zero);
                    baked.WriteFloat(GetActive(i));
                }
            }

            if (!tpsCompatibility && spsBlendshapes != null) {
                foreach (var bs in spsBlendshapes) {
                    var weight = renderer.GetBlendshapeWeight(bs);
                    renderer.SetBlendshapeWeight(bs, 0);
                    var bsBakedMeshOff = MeshBaker.BakeMesh(renderer, origin, true);
                    renderer.SetBlendshapeWeight(bs, 100);
                    var bsBakedMeshOn = MeshBaker.BakeMesh(renderer, origin, true);
                    renderer.SetBlendshapeWeight(bs, weight);
                    baked.WriteFloat(weight);
                    for (var v = 0; v < bsBakedMeshOn.vertices.Length; v++) {
                        baked.WriteVector3(bsBakedMeshOn.vertices[v] - bsBakedMeshOff.vertices[v]);
                        baked.WriteVector3(v < bsBakedMeshOn.normals.Length ? bsBakedMeshOn.normals[v] - bsBakedMeshOff.normals[v] : Vector3.zero);
                        baked.WriteVector3(v < bsBakedMeshOn.tangents.Length ? bsBakedMeshOn.tangents[v] - bsBakedMeshOff.tangents[v] : Vector3.zero);
                    }
                }
            }

            var tex = baked.Save();
            return tex;
        }

        public static Vector4[] GetPackedResolverRadiusSamples(
            IEnumerable<RendererBakeInput> renderers,
            Transform origin,
            float worldLength
        ) {
            var radii = GetResolverRadiusSamples(
                renderers,
                origin.position,
                origin.rotation,
                worldLength
            );

            var packed = new Vector4[ResolverRadiusSampleCount / 4];
            for (var i = 0; i < ResolverRadiusSampleCount; i++) {
                var packedIndex = i / 4;
                var component = i % 4;
                packed[packedIndex][component] = radii[i];
            }
            return packed;
        }

        public static float[] GetResolverRadiusSamples(
            IEnumerable<RendererBakeInput> renderers,
            Vector3 worldPosition,
            Quaternion worldRotation,
            float worldLength
        ) {
            var safeLength = Mathf.Max(worldLength, MinSampleRadius);
            var buckets = Enumerable.Range(0, ResolverRadiusSampleCount)
                .Select(_ => new List<float>())
                .ToArray();
            var inverseWorldRotation = Quaternion.Inverse(worldRotation);

            foreach (var input in renderers ?? Enumerable.Empty<RendererBakeInput>()) {
                if (input?.renderer == null) continue;
                var bakedMesh = MeshBaker.BakeMesh(input.renderer);
                if (bakedMesh == null) continue;

                for (var i = 0; i < bakedMesh.vertices.Length; i++) {
                    if (input.activeFromMask != null && i < input.activeFromMask.Length && input.activeFromMask[i] <= 0) continue;

                    var vertex = inverseWorldRotation * (input.renderer.owner().TransformPoint(bakedMesh.vertices[i]) - worldPosition);
                    if (vertex.z <= 0) continue;

                    var bucketIndex = Mathf.Clamp(
                        Mathf.FloorToInt(Mathf.Clamp01(vertex.z / safeLength) * ResolverRadiusSampleCount),
                        0,
                        ResolverRadiusSampleCount - 1
                    );
                    buckets[bucketIndex].Add(new Vector2(vertex.x, vertex.y).magnitude);
                }
            }

            var radii = new float[ResolverRadiusSampleCount];
            for (var i = 0; i < ResolverRadiusSampleCount; i++) {
                radii[i] = ReduceBucketRadius(buckets[i]);
            }
            BackfillMissingSamples(radii);
            return radii;
        }

        private static float ReduceBucketRadius(List<float> bucket) {
            if (bucket == null || bucket.Count == 0) return 0;

            bucket.Sort();
            var filteredCount = Mathf.Max(1, Mathf.CeilToInt(bucket.Count * 0.75f));
            return Mathf.Max(bucket.Take(filteredCount).DefaultIfEmpty(0).Max(), MinSampleRadius);
        }

        private static void BackfillMissingSamples(float[] radii) {
            var lastKnown = 0f;
            for (var i = 0; i < radii.Length; i++) {
                if (radii[i] > 0) {
                    lastKnown = radii[i];
                } else if (lastKnown > 0) {
                    radii[i] = lastKnown;
                }
            }

            lastKnown = 0f;
            for (var i = radii.Length - 1; i >= 0; i--) {
                if (radii[i] > 0) {
                    lastKnown = radii[i];
                } else if (lastKnown > 0) {
                    radii[i] = lastKnown;
                }
            }

            for (var i = 0; i < radii.Length; i++) {
                if (radii[i] <= 0) radii[i] = MinSampleRadius;
            }
        }
    }
}
