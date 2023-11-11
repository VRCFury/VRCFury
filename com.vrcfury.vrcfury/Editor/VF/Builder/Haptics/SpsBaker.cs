using System.Collections.Generic;
using UnityEngine;
using VF.Builder.Exceptions;
using VF.Utils;

namespace VF.Builder.Haptics {
    public class SpsBaker {
        public static Texture2D Bake(
            SkinnedMeshRenderer skin,
            string tmpDir,
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

            var vertices = bakedMesh.vertices;
            var normals = bakedMesh.normals;

            float GetActive(int i) {
                return activeFromMask == null ? 1 : activeFromMask[i];
            }

            for (var i = 0; i < vertices.Length; i++) {
                baked.WriteVector3(vertices[i]);

                if (tpsCompatibility) {
                    if (GetActive(i) == 0) {
                        baked.WriteVector3(new Vector3(0,0,0));
                    } else {
                        baked.WriteVector3(normals[i]);
                    }
                } else {
                    baked.WriteVector3(normals[i]);
                    baked.WriteFloat(GetActive(i));
                }
            }

            if (!tpsCompatibility && spsBlendshapes != null) {
                foreach (var bs in spsBlendshapes) {
                    var weight = skin.GetBlendShapeWeight(bs);
                    skin.SetBlendShapeWeight(bs, weight + 100);
                    var bsBakedMesh = MeshBaker.BakeMesh(skin, skin.rootBone, true);
                    skin.SetBlendShapeWeight(bs, weight);
                    baked.WriteFloat(weight);
                    for (var v = 0; v < bsBakedMesh.vertices.Length; v++) {
                        baked.WriteVector3(bsBakedMesh.vertices[v] - vertices[v]);
                        baked.WriteVector3(bsBakedMesh.normals[v].normalized - normals[v].normalized);
                    }
                }
            }

            var tex = baked.Save();
            VRCFuryAssetDatabase.SaveAsset(tex, tmpDir, $"SPS Info for {skin.owner().name}");
            return tex;
        }
    }
}
