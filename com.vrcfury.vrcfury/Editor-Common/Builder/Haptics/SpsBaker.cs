using System.Collections.Generic;
using UnityEngine;
using VF.Exceptions;
using VF.Utils;

namespace VF.Builder.Haptics {
    internal static class SpsBaker {
        internal const int RadiusProfileSamples = 256;

        public static Texture2D Bake(
            Renderer renderer,
            Transform origin,
            float[] activeFromMask,
            ICollection<string> spsBlendshapes,
            AnimationCurve radiusCurve,
            float localRadius
        ) {
            var bakedMesh = MeshBaker.BakeMesh(renderer, origin, true);
            if (bakedMesh == null)
                throw new VRCFBuilderException("Failed to bake mesh for SPS configuration");

            var vertexCount = bakedMesh.vertices.Length;
            var blendshapeCount = spsBlendshapes?.Count ?? 0;
            // The mesh stores position, normal, tangent, and mask. Each shape stores its weight and three deltas.
            var expectedFloatCount = 1 + vertexCount * 10 + blendshapeCount * (1 + vertexCount * 9) + RadiusProfileSamples;
            using var baked = new SpsBakedTexture(expectedFloatCount);

            // version
            baked.WriteFloat(0);

            float GetActive(int i) {
                return activeFromMask != null && i < activeFromMask.Length ? activeFromMask[i] : 1;
            }

            static Vector3 GetAttribute(Vector3[] values, int i) {
                return i < values.Length ? values[i] : Vector3.zero;
            }

            for (var i = 0; i < vertexCount; i++) {
                baked.WriteVector3(bakedMesh.vertices[i]);

                baked.WriteVector3(GetAttribute(bakedMesh.normals, i));
                baked.WriteVector3(GetAttribute(bakedMesh.tangents, i));
                baked.WriteFloat(GetActive(i));
            }

            if (spsBlendshapes != null) {
                foreach (var bs in spsBlendshapes) {
                    var weight = renderer.GetBlendshapeWeight(bs);
                    MeshBaker.BakedMesh BakeAt(float sampleWeight) {
                        if (weight == sampleWeight) return bakedMesh;
                        renderer.SetBlendshapeWeight(bs, sampleWeight);
                        return MeshBaker.BakeMesh(renderer, origin, true);
                    }

                    MeshBaker.BakedMesh bsBakedMeshOff;
                    MeshBaker.BakedMesh bsBakedMeshOn;
                    try {
                        bsBakedMeshOff = BakeAt(0);
                        bsBakedMeshOn = BakeAt(100);
                    } finally {
                        renderer.SetBlendshapeWeight(bs, weight);
                    }
                    if (bsBakedMeshOff == null || bsBakedMeshOn == null) continue;
                    baked.WriteFloat(weight);
                    var shapeVertexCount = Mathf.Min(bsBakedMeshOn.vertices.Length, bsBakedMeshOff.vertices.Length);
                    for (var v = 0; v < shapeVertexCount; v++) {
                        baked.WriteVector3(bsBakedMeshOn.vertices[v] - bsBakedMeshOff.vertices[v]);
                        baked.WriteVector3(
                            GetAttribute(bsBakedMeshOn.normals, v) - GetAttribute(bsBakedMeshOff.normals, v)
                        );
                        baked.WriteVector3(
                            GetAttribute(bsBakedMeshOn.tangents, v) - GetAttribute(bsBakedMeshOff.tangents, v)
                        );
                    }
                }
            }

            for (var i = 0; i < RadiusProfileSamples; i++) {
                var depth = i / (RadiusProfileSamples - 1f);
                baked.WriteFloat(Mathf.Max(radiusCurve.Evaluate(depth), 0) / localRadius);
            }

            var tex = baked.Save();
            return tex;
        }
    }
}
