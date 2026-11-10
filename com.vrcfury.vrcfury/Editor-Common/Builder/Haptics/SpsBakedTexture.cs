using System.Collections.Generic;
using UnityEngine;
using VF.Utils;

namespace VF.Builder.Haptics {
    internal class SpsBakedTexture {
        private readonly List<float> bakeArray = new List<float>();

        public void WriteFloat(float f) {
            bakeArray.Add(f);
        }
        public void WriteVector3(Vector3 v) {
            WriteFloat(v.x);
            WriteFloat(v.y);
            WriteFloat(v.z);
        }

        public Texture2D Save() {
            const int width = 8192;
            var height = bakeArray.Count / width + 1;
            var tex = VrcfObjectFactory.CreateTexture2D(width, height, TextureFormat.RFloat, false, true);
            tex.name = "SPS Data";
            var texArray = new float[width * height];
            bakeArray.CopyTo(texArray);
            tex.SetPixelData(texArray, 0);
            tex.Apply(false);
            return tex;
        }
    }
}
