using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using UnityEngine;
using VF.Component;

namespace VF.Builder.Haptics {
    public static class PlugMaskGenerator {
        // Returns 1 if active, 0 if ignored
        public static float[] GetMask(Renderer renderer, VRCFuryHapticPlug plug) {
            Mesh mesh = null;
            if (renderer is SkinnedMeshRenderer s) {
                mesh = s.sharedMesh;
            } else if (renderer is MeshRenderer r) {
                var meshFilter = r.owner().GetComponent<MeshFilter>();
                if (meshFilter) {
                    mesh = meshFilter.sharedMesh;
                }
            }
            if (mesh == null) {
                return null;
            }
            
            var boneWeights = mesh.boneWeights;
            var vertices = mesh.vertices;
            var uvs = mesh.uv;
            var textureMask = MakeReadable(plug.textureMask?.Get());

            ISet<int> includedBoneIds = ImmutableHashSet<int>.Empty;
            if (plug.useBoneMask && renderer is SkinnedMeshRenderer skin) {
                VFGameObject firstBone = plug.owner();
                while (firstBone != null) {
                    if (skin.bones.Contains((Transform)firstBone)) {
                        break;
                    }
                    firstBone = firstBone.parent;
                }
                if (firstBone != null) {
                    includedBoneIds = firstBone.GetSelfAndAllChildren()
                        .Select(bone => Array.IndexOf(skin.bones, (Transform)bone))
                        .Where(id => id >= 0)
                        .ToImmutableHashSet();
                }
            }

            float[] output = new float[vertices.Length];
            for (var i = 0; i < vertices.Length; i++) {
                var activeByWeight = 1f;
                if (boneWeights.Length > 0) {
                    activeByWeight = GetWeight(boneWeights[i], includedBoneIds);
                }
                var activeByTexture = 1f;
                if (textureMask != null) {
                    var p = textureMask.GetPixelBilinear(uvs[i].x, uvs[i].y);
                    activeByTexture = 1 - Math.Min(p.maxColorComponent, p.a);
                }

                output[i] = Math.Min(activeByWeight, activeByTexture);
            }

            return output;
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
