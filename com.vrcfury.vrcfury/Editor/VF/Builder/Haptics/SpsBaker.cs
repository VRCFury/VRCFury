using System;
using UnityEngine;

namespace VF.Builder.Haptics {
    public class SpsBaker {
        public static Texture2D Bake(MeshBaker.BakedMesh mesh, string tmpDir) {
            var bitsRequired =
                    1 // version
                    + mesh.vertices.Length * 6 // positions + normals
                ;
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
            WriteColor(0, 0, 0, 0);

            for (var i = 0; i < mesh.vertices.Length; i++) {
                WriteVector3(mesh.vertices[i]);
                WriteVector3(mesh.normals[i]);
            }

            bake.SetPixels32(bakeArray);
            bake.Apply(false);
            VRCFuryAssetDatabase.SaveAsset(bake, tmpDir, "sps_bake");
            return bake;
        }
    }
}
