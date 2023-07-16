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
            float[] activeFromMask,
            bool tpsCompatibility
        ) {
            var bakedMesh = MeshBaker.BakeMesh(skin, skin.rootBone, !tpsCompatibility);
            if (bakedMesh == null)
                throw new VRCFBuilderException("Failed to bake mesh for SPS configuration");

            int bitsRequired;
            if (tpsCompatibility) {
                bitsRequired = bakedMesh.vertices.Length * 6;
            } else {
                bitsRequired =
                    1 // version
                    + bakedMesh.vertices.Length * 7 // positions + normals + active
                    ;
            }
            var width = tpsCompatibility ? 8190 : 8192;
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

            var vertices = bakedMesh.vertices;
            var normals = bakedMesh.normals;

            float GetActive(int i) {
                return activeFromMask == null ? 1 : activeFromMask[i];
            }

            for (var i = 0; i < vertices.Length; i++) {
                WriteVector3(vertices[i]);

                if (tpsCompatibility) {
                    if (GetActive(i) == 0) {
                        WriteVector3(new Vector3(0,0,0));
                    } else {
                        WriteVector3(normals[i]);
                    }
                } else {
                    WriteVector3(normals[i]);
                    WriteFloat(GetActive(i));
                }

            }

            bake.SetPixels32(bakeArray);
            bake.Apply(false);
            VRCFuryAssetDatabase.SaveAsset(bake, tmpDir, "sps_bake");
            return bake;
        }
    }
}
