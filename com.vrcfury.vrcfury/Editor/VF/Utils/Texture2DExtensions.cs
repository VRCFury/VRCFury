using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.Experimental.Rendering;

namespace VF.Utils {
    internal static class Texture2DExtensions {
        public static void ForceReadable(this Texture2D texture, bool on = true) {
            var so = new SerializedObject(texture);
            so.Update();
            var sp = so.FindProperty("m_IsReadable");
            sp.boolValue = on;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
        
        public static Texture2D Optimize(this Texture2D original, bool forceCompression = true, int maxSize = 256) {
            var needsResized = original.width > maxSize || original.height > maxSize;
            var needsCompressed = forceCompression && !GraphicsFormatUtility.IsCompressedFormat(original.format);

            if (!needsResized && !needsCompressed) return original;
            
            var texture = original.Clone();
            texture.ForceReadable();

            if (needsResized) {
                var multiplier = ((double)maxSize) / Math.Max(texture.width, texture.height);
                var targetWidth = (int)Math.Round(texture.width * multiplier);
                var targetHeight = (int)Math.Round(texture.height * multiplier);
                var renderTexture = RenderTexture.GetTemporary(targetWidth, targetHeight);
                Graphics.Blit(texture, renderTexture);

                var scaled = new Texture2D(targetWidth, targetHeight, TextureFormat.RGBA32, false);
                VrcfObjectFactory.Register(scaled);
                scaled.filterMode = FilterMode.Bilinear;
                scaled.wrapMode = TextureWrapMode.Clamp;
                RenderTexture.active = renderTexture;
                scaled.ReadPixels(new Rect(0, 0, targetWidth, targetHeight), 0, 0);
                RenderTexture.active = null;
                RenderTexture.ReleaseTemporary(renderTexture);
                texture = scaled;
                needsCompressed = true;
            }

            if (needsCompressed) {
                var compressedTypeTest = original.Clone();
                compressedTypeTest.Compress(false);
                var autoCompressFormat = compressedTypeTest.format;
                EditorUtility.CompressTexture(texture, autoCompressFormat, TextureCompressionQuality.Best);
            }

            return texture;
        }
    }
}
