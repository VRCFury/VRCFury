using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using UnityEngine;
using VF.Builder.Exceptions;

namespace VF.Builder.Haptics {
    public class SpsBaker {
        public static Texture2D Bake(
            SkinnedMeshRenderer skin,
            string tmpDir,
            Texture2D textureMask = null,
            bool tpsCompatibility = false
        ) {
            var bakedMesh = MeshBaker.BakeMesh(skin, skin.rootBone, !tpsCompatibility);
            if (bakedMesh == null)
                throw new VRCFBuilderException("Failed to bake mesh for SPS configuration");

            textureMask = MakeReadable(textureMask);

            var firstBone = skin.rootBone;
            while (firstBone != null) {
                if (skin.bones.Contains(firstBone)) {
                    break;
                }
                firstBone = firstBone.parent;
            }

            ISet<int> includedBoneIds;
            if (firstBone != null) {
                includedBoneIds = firstBone.GetComponentsInChildren<Transform>(true)
                    .Select(bone => Array.IndexOf(skin.bones, bone))
                    .Where(id => id >= 0)
                    .ToImmutableHashSet();
            } else {
                includedBoneIds = ImmutableHashSet<int>.Empty;
            }

            int bitsRequired;
            if (tpsCompatibility) {
                bitsRequired = bakedMesh.vertices.Length * 6;
            } else {
                bitsRequired =
                    1 // version
                    + bakedMesh.vertices.Length * 7 // positions + normals + active
                    ;
            }
            var width = 8192;
            var height = (bitsRequired / width) + 1;

            Texture2D bake = new Texture2D(width, height, TextureFormat.RGBA32, false, true);
            var bakeArray = bake.GetPixels32();

            var offset = 0;

            void WriteColor(byte r, byte g, byte b, byte a) {
                bakeArray[offset] = new Color32(r, g, b, a);
                offset++;
            }
            void WriteFloat(float f) {
                byte[] bytes = BitConverter.GetBytes(f);
                WriteColor(bytes[0], bytes[1], bytes[2], bytes[3]);
            }
            void WriteVector3(Vector3 v) {
                WriteFloat(v.x);
                WriteFloat(v.y);
                WriteFloat(v.z);
            }

            // version
            if (!tpsCompatibility) {
                WriteColor(0, 0, 0, 0);
            }

            var uv = skin.sharedMesh.uv;
            var vertices = bakedMesh.vertices;
            var normals = bakedMesh.normals;
            var boneWeights = skin.sharedMesh.boneWeights;

            for (var i = 0; i < vertices.Length; i++) {
                var activeByTexture = 1f;
                if (textureMask != null) {
                    var p = textureMask.GetPixelBilinear(uv[i].x, uv[i].y);
                    activeByTexture = 1 - Math.Min(p.maxColorComponent, p.a);
                }

                WriteVector3(vertices[i]);

                if (tpsCompatibility) {
                    if (activeByTexture == 0) {
                        WriteVector3(new Vector3(0,0,0));
                    } else {
                        WriteVector3(normals[i]);
                    }
                } else {
                    var activeByWeight = GetWeight(boneWeights[i], includedBoneIds);
                    WriteVector3(normals[i]);
                    WriteFloat(Math.Min(activeByWeight, activeByTexture));
                }

            }

            bake.SetPixels32(bakeArray);
            bake.Apply(false);
            VRCFuryAssetDatabase.SaveAsset(bake, tmpDir, "sps_bake");
            return bake;
        }

        private static float GetWeight(BoneWeight boneWeight, ICollection<int> boneIds) {
            if (boneIds.Count == 0) return 1;
            var weightedToBone = 0f;
            if (boneIds.Contains(boneWeight.boneIndex0)) weightedToBone += boneWeight.weight0;
            if (boneIds.Contains(boneWeight.boneIndex1)) weightedToBone += boneWeight.weight1;
            if (boneIds.Contains(boneWeight.boneIndex2)) weightedToBone += boneWeight.weight2;
            if (boneIds.Contains(boneWeight.boneIndex3)) weightedToBone += boneWeight.weight3;
            var totalWeight = boneWeight.weight0 + boneWeight.weight1 + boneWeight.weight2 + boneWeight.weight3;
            if (totalWeight > 0) {
                weightedToBone /= totalWeight;
            }
            return weightedToBone;
        }

        // https://support.unity.com/hc/en-us/articles/206486626-How-can-I-get-pixels-from-unreadable-textures-
        private static Texture2D MakeReadable(Texture2D texture) {
            if (texture == null) return null;
            if (texture.isReadable) return texture;
            var tmp = RenderTexture.GetTemporary( 
                texture.width,
                texture.height,
                0,
                RenderTextureFormat.Default,
                RenderTextureReadWrite.Linear);
            Graphics.Blit(texture, tmp);
            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = tmp;
            Texture2D myTexture2D = new Texture2D(texture.width, texture.height);
            myTexture2D.ReadPixels(new Rect(0, 0, tmp.width, tmp.height), 0, 0);
            myTexture2D.Apply();
            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(tmp);
            return myTexture2D;
        }
    }
}
