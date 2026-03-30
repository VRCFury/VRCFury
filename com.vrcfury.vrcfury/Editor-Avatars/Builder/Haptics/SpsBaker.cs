using System.Collections.Generic;
using UnityEngine;
using VF.Builder.Exceptions;
using VF.Utils;

namespace VF.Builder.Haptics {
    internal static class SpsBaker {
        public static Texture2D Bake(
            SkinnedMeshRenderer skin,
            float[] activeFromMask,
            bool tpsCompatibility,
            ICollection<string> spsBlendshapes = null
        ) {
            var bakedMesh = MeshBaker.BakeMesh(skin, skin.rootBone, !tpsCompatibility);
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
                    var weight = skin.GetBlendShapeWeight(bs);
                    skin.SetBlendShapeWeight(bs, 0);
                    var bsBakedMeshOff = MeshBaker.BakeMesh(skin, skin.rootBone, true);
                    skin.SetBlendShapeWeight(bs, 100);
                    var bsBakedMeshOn = MeshBaker.BakeMesh(skin, skin.rootBone, true);
                    skin.SetBlendShapeWeight(bs, weight);
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
    }
}
