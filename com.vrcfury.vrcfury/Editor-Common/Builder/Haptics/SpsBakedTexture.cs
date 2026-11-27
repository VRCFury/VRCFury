using System;
using UnityEngine;
using VF.Utils;

namespace VF.Builder.Haptics {
    internal ref struct SpsBakedTexture {
        private const int Width = 8192;
        private Texture2D texture;
        private Span<float> pixels;
        private int count;

        public SpsBakedTexture(int expectedFloatCount) {
            texture = null;
            pixels = default;
            count = 0;
            Resize(expectedFloatCount / Width + 1);
        }

        public void WriteFloat(float f) {
            if (count == pixels.Length) Resize(texture.height * 2);
            pixels[count++] = f;
        }
        public void WriteVector3(Vector3 v) {
            if (pixels.Length - count < 3) Resize(texture.height * 2);
            pixels[count++] = v.x;
            pixels[count++] = v.y;
            pixels[count++] = v.z;
        }

        private void Resize(int height) {
            var replacement = VrcfObjectFactory.CreateTexture2D(Width, height, TextureFormat.RFloat, false, true);
            try {
                var replacementPixels = replacement.GetPixelData<float>(0).AsSpan();
                pixels.Slice(0, count).CopyTo(replacementPixels);
                Dispose();
                texture = replacement;
                pixels = replacementPixels;
            } catch {
                UnityEngine.Object.DestroyImmediate(replacement);
                throw;
            }
        }

        public Texture2D Save() {
            var height = count / Width + 1;
            if (texture.height != height) Resize(height);
            pixels.Slice(count).Clear();
            texture.name = "SPS Data";
            texture.Apply(false);
            var baked = texture;
            texture = null;
            pixels = default;
            return baked;
        }

        public void Dispose() {
            if (texture != null) UnityEngine.Object.DestroyImmediate(texture);
            texture = null;
            pixels = default;
        }
    }
}
