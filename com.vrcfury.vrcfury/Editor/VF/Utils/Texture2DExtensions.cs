﻿using System;
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
            var multiplier = ((double)maxSize) / Math.Max(original.width, original.height);
            if (multiplier > 1) multiplier = 1;

            int MakeSafeSize(double size) {
                return Math.Max(4, Mathf.ClosestPowerOfTwo((int)Math.Round(size)));
            }
            var targetWidth = MakeSafeSize(original.width * multiplier);
            var targetHeight = MakeSafeSize(original.height * multiplier);
            var needsResized = targetWidth != original.width || targetHeight != original.height;

#if UNITY_2022_1_OR_NEWER
            var isCompressed = GraphicsFormatUtility.IsCompressedFormat(original.format);
#else
            var isCompressed = GraphicsFormatUtility.IsCompressedFormat(GraphicsFormatUtility.GetGraphicsFormat(original.format, true));
#endif
            var needsCompressed = forceCompression && !isCompressed;

            if (!needsResized && !needsCompressed) return original;
            
            var texture = original.Clone();
            texture.ForceReadable();

            if (needsResized) {
                Debug.LogWarning($"VRCFury is resizing texture {AssetDatabase.GetAssetPath(original)} from {original.width}.{original.height} to {targetWidth}x{targetHeight}");
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
                Debug.LogWarning($"VRCFury is compressing texture {AssetDatabase.GetAssetPath(original)}");
                var compressedTypeTest = new Texture2D(256, 256, original.format, false);
                compressedTypeTest.Compress(false);
                var autoCompressFormat = compressedTypeTest.format;
                Debug.LogWarning($"Using type {autoCompressFormat}");
                EditorUtility.CompressTexture(texture, autoCompressFormat, TextureCompressionQuality.Best);
            }

            return texture;
        }
    }
}
