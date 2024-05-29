using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace VF.Builder.Haptics {
    internal class SpsBakedTexture {
        private readonly List<Color32> bakeArray = new List<Color32>();
        private readonly bool tpsCompatibility;

        public SpsBakedTexture(bool tpsCompatibility) {
            this.tpsCompatibility = tpsCompatibility;
        }

        public void WriteColor(byte r, byte g, byte b, byte a) {
            bakeArray.Add(new Color32(r, g, b, a));
        }
        public void WriteFloat(float f) {
            byte[] bytes = BitConverter.GetBytes(f);
            WriteColor(bytes[0], bytes[1], bytes[2], bytes[3]);
        }
        public void WriteVector3(Vector3 v) {
            WriteFloat(v.x);
            WriteFloat(v.y);
            WriteFloat(v.z);
        }

        public Texture2D Save() {
            var width = tpsCompatibility ? 8190 : 8192;
            var height = (int)(bakeArray.LongCount() / width) + 1;
            var tex = new Texture2D(width, height, TextureFormat.RGBA32, false, true);
            tex.name = "SPS Data";
            var texArray = tex.GetPixels32();
            for (var i = 0; i < bakeArray.Count; i++) {
                texArray[i] = bakeArray[i];
            }
            tex.SetPixels32(texArray);
            tex.Apply(false);
            return tex;
        }
    }
}
