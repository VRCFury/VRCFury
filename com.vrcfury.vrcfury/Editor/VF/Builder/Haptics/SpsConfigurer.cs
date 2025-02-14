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
        }

        public static bool IsSps(Material mat) {
            return mat != null && mat.HasProperty(SpsBake);
        }
    }
}
