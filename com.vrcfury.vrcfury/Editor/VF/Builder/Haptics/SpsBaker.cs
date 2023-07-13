using System;
using UnityEngine;
using VF.Builder.Exceptions;

namespace VF.Builder.Haptics {
    public class SpsBaker {
        public static Texture2D Bake(SkinnedMeshRenderer skin, string tmpDir, Texture2D mask = null, bool tpsCompatibility = false) {
            var mesh = MeshBaker.BakeMesh(skin, skin.rootBone, !tpsCompatibility);
            if (mesh == null)
                throw new VRCFBuilderException("Failed to bake mesh for SPS configuration");

            mask = MakeReadable(mask);

            var bitsRequired =
                    1 // version
                    + mesh.vertices.Length * 6 // positions + normals
                ;
            if (tpsCompatibility) {
                bitsRequired = mesh.vertices.Length * 6;
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

            for (var i = 0; i < mesh.vertices.Length; i++) {
                var masked = 0f;
                if (mask != null) {
                    var p = mask.GetPixelBilinear(uv[i].x, uv[i].y);
                    masked = Math.Min(p.maxColorComponent, p.a);
                }
                WriteVector3(mesh.vertices[i]);
                if (masked > 0 && tpsCompatibility) {
                    WriteVector3(new Vector3(0,0,0));
                } else {
                    WriteVector3(mesh.normals[i]);
                }
            }

            bake.SetPixels32(bakeArray);
            bake.Apply(false);
            VRCFuryAssetDatabase.SaveAsset(bake, tmpDir, "sps_bake");
            return bake;
        }

        // https://support.unity.com/hc/en-us/articles/206486626-How-can-I-get-pixels-from-unreadable-textures-
        private static Texture2D MakeReadable(Texture2D texture) {
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
