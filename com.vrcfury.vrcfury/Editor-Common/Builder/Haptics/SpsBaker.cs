using System.Collections.Generic;
using UnityEngine;
using VF.Exceptions;
using VF.Utils;

namespace VF.Builder.Haptics {
    internal static class SpsBaker {
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
                return activeFromMask.GetOrDefault(i, 1);
            }

            for (var i = 0; i < bakedMesh.vertices.Length; i++) {
                baked.WriteVector3(bakedMesh.vertices[i]);

                if (tpsCompatibility) {
                    if (GetActive(i) == 0) {
                        baked.WriteVector3(new Vector3(0,0,0));
                    } else {
                        baked.WriteVector3(bakedMesh.normals.GetOrDefault(i, Vector3.zero));
                    }
                } else {
                    baked.WriteVector3(bakedMesh.normals.GetOrDefault(i, Vector3.zero));
                    baked.WriteVector3(bakedMesh.tangents.GetOrDefault(i, Vector3.zero));
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
                    if (bsBakedMeshOff == null || bsBakedMeshOn == null) continue;
                    baked.WriteFloat(weight);
                    var vertexCount = Mathf.Min(bsBakedMeshOn.vertices.Length, bsBakedMeshOff.vertices.Length);
                    for (var v = 0; v < vertexCount; v++) {
                        baked.WriteVector3(bsBakedMeshOn.vertices[v] - bsBakedMeshOff.vertices[v]);
                        baked.WriteVector3(
                            bsBakedMeshOn.normals.GetOrDefault(v, Vector3.zero)
                            - bsBakedMeshOff.normals.GetOrDefault(v, Vector3.zero)
                        );
                        baked.WriteVector3(
                            bsBakedMeshOn.tangents.GetOrDefault(v, Vector3.zero)
                            - bsBakedMeshOff.tangents.GetOrDefault(v, Vector3.zero)
                        );
                    }
                }
            }

            var tex = baked.Save();
            return tex;
        }
    }
}
