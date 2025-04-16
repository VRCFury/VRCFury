using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using VF.Component;
using VF.Utils;

namespace VF.Builder.Haptics {
    internal static class SpsConfigurer {
        private const string SpsEnabled = "_SPS_Enabled";
        public const string SpsLength = "_SPS_Length";
        public const string SpsPlusEnabled = "_SPS_Plus_Enabled";
        private const string SpsOverrun = "_SPS_Overrun";
        private const string SpsBakedLength = "_SPS_BakedLength";
        private const string SpsBake = "_SPS_Bake";

        private const string SpsVATEnabled = "_SPS_VAT_Enabled";
        private const string SpsVATInterpolate = "_SPS_VAT_Interpolate";
        private const string SpsVATPlaybackSpeed = "_SPS_VAT_PlaybackSpeed";
        private const string SpsVATPosTexture = "_SPS_VAT_PosTexture";
        private const string SpsVATRotTexture = "_SPS_VAT_RotTexture";
        private const string SpsVATFPS = "_SPS_VAT_FPS";
        private const string SpsVATFrameCount = "_SPS_VAT_FrameCount";
        private const string SpsVATAnimMin = "_SPS_VAT_AnimMin";
        private const string SpsVATAnimMax = "_SPS_VAT_AnimMax";

        public static void ConfigureSpsMaterial(
            SkinnedMeshRenderer skin,
            Material m,
            float worldLength,
            Texture2D spsBaked,
            VRCFuryHapticPlug plug,
            VFGameObject bakeRoot,
            IList<string> spsBlendshapes
        ) {
            if (DpsConfigurer.IsDps(m) || TpsConfigurer.IsTps(m)) {
                throw new Exception(
                    $"VRCFury SPS plug was asked to use SPS deformation on renderer," +
                    $" but it already has TPS or DPS. If you want to use SPS, use a regular shader" +
                    $" on the mesh instead.");
            }

            SpsPatcher.Patch(m, plug.spsKeepImports);
            {
                // Prevent poi from stripping our parameters
                var count = ShaderUtil.GetPropertyCount(m.shader);
                for (var i = 0; i < count; i++) {
                    var propertyName = ShaderUtil.GetPropertyName(m.shader, i);
                    if (propertyName.StartsWith("_SPS_")) {
                       m.SetOverrideTag(propertyName + "Animated", "1");
                    }
                }
            }
            m.SetFloat(SpsEnabled, plug.spsAnimatedEnabled);
            if (plug.spsAnimatedEnabled == 0) bakeRoot.active = false;
            m.SetFloat(SpsLength, worldLength);
            m.SetFloat(SpsBakedLength, worldLength);
            m.SetFloat(SpsOverrun, plug.spsOverrun ? 1 : 0);
            m.SetTexture(SpsBake, spsBaked);
            m.SetFloat("_SPS_BlendshapeCount", spsBlendshapes.Count);
            m.SetFloat("_SPS_BlendshapeVertCount", skin.GetVertexCount());
            for (var i = 0; i < spsBlendshapes.Count; i++) {
                var name = spsBlendshapes[i];
                if (skin.HasBlendshape(name)) {
                    m.SetFloat("_SPS_Blendshape" + i, skin.GetBlendShapeWeight(name));
                }
            }

            m.SetFloat(SpsVATEnabled, plug.enableVat ? 1 : 0);
            m.SetFloat(SpsVATInterpolate, plug.vatInterpolate ? 1 : 0);
            m.SetFloat(SpsVATPlaybackSpeed, plug.vatPlaybackSpeed);
            m.SetTexture(SpsVATPosTexture, MakeReadable(plug.vatPosTexture?.Get(), "Vat Pos Texture"));
            m.SetTexture(SpsVATRotTexture, MakeReadable(plug.vatRotTexture?.Get(), "Vat Rot Texture"));
            m.SetFloat(SpsVATFPS, plug.vatFPS);
            m.SetFloat(SpsVATFrameCount, plug.vatFrameCount);
            m.SetFloat(SpsVATAnimMin, plug.vatAnimMin);
            m.SetFloat(SpsVATAnimMax, plug.vatAnimMax);
        }

        public static bool IsSps(Material mat) {
            return mat != null && mat.HasProperty(SpsBake);
        }

        // This is from PlugMaskGenerator, need it to convert GuidTexture2d into texture2d
        // Made changes to read guidtextured as a signed ARGBHalf
        // https://support.unity.com/hc/en-us/articles/206486626-How-can-I-get-pixels-from-unreadable-textures-
        private static Texture2D MakeReadable(Texture2D texture, string texture_name) {
            if (texture == null) return null;
            if (texture.isReadable) return texture;
            var tmp = RenderTexture.GetTemporary(
                texture.width,
                texture.height,
                0,
                RenderTextureFormat.ARGBHalf, // Need to get a signed texture
                RenderTextureReadWrite.Linear);
            Graphics.Blit(texture, tmp);
            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = tmp;
            Texture2D myTexture2D = new Texture2D(texture.width, texture.height, TextureFormat.RGBAHalf, false, true);
            VrcfObjectFactory.Register(myTexture2D);
            myTexture2D.ReadPixels(new Rect(0, 0, tmp.width, tmp.height), 0, 0);

            myTexture2D.name += texture_name;

            myTexture2D.wrapMode = TextureWrapMode.Clamp;
            myTexture2D.filterMode = FilterMode.Point;

            myTexture2D.Apply();

            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(tmp);
            return myTexture2D;
        }
    }
}
