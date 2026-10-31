using System.Collections.Generic;
using UnityEngine;
using VF.Utils;

namespace VF.Builder.Haptics {
    internal class SpsBakedTexture {
        private readonly List<float> bakeArray = new List<float>();
        private readonly bool tpsCompatibility;

        public SpsBakedTexture(bool tpsCompatibility) {
            this.tpsCompatibility = tpsCompatibility;
        }

        public void WriteFloat(float f) {
            bakeArray.Add(f);
        }
        public void WriteVector3(Vector3 v) {
            WriteFloat(v.x);
            WriteFloat(v.y);
            WriteFloat(v.z);
        }

        public Texture2D Save() {
            var width = tpsCompatibility ? 8190 : 8192;
            var height = bakeArray.Count / width + 1;
            // Both formats store four bytes per texel. TPS expects the float's raw bytes in RGBA8;
            // SPS reads RFloat directly, avoiding byte reconstruction in every vertex variant.
            var format = tpsCompatibility ? TextureFormat.RGBA32 : TextureFormat.RFloat;
            var tex = VrcfObjectFactory.CreateTexture2D(width, height, format, false, true);
            tex.name = "SPS Data";
            var texArray = new float[width * height];
            bakeArray.CopyTo(texArray);
            tex.SetPixelData(texArray, 0);
            tex.Apply(false);
            return tex;
        }
    }
}
