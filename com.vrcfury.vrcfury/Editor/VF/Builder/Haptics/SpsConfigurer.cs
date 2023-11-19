using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using VF.Component;
using VF.Utils;

namespace VF.Builder.Haptics {
    public static class SpsConfigurer {
        private static readonly int Enabled = Shader.PropertyToID(SpsEnabled);
        private static readonly int Length = Shader.PropertyToID(SpsLength);
        private static readonly int BakedLength = Shader.PropertyToID(SpsBakedLength);
        private static readonly int Overrun = Shader.PropertyToID(SpsOverrun);
        private static readonly int Bake = Shader.PropertyToID(SpsBake);
        private static readonly int SpsBlendshapeCount = Shader.PropertyToID("_SPS_BlendshapeCount");
        private static readonly int SpsBlendshapeVertCount = Shader.PropertyToID("_SPS_BlendshapeVertCount");
        private const string SpsEnabled = "_SPS_Enabled";
        private const string SpsLength = "_SPS_Length";
        private const string SpsOverrun = "_SPS_Overrun";
        private const string SpsBakedLength = "_SPS_BakedLength";
        private const string SpsBake = "_SPS_Bake";

        public static void ConfigureSpsMaterial(
            SkinnedMeshRenderer skin,
            Material original,
            float worldLength,
            Texture2D spsBaked,
            MutableManager mutableManager,
            VRCFuryHapticPlug plug,
            VFGameObject bakeRoot,
            IList<string> spsBlendshapes
        ) {
            if (DpsConfigurer.IsDps(original) || TpsConfigurer.IsTps(original)) {
                throw new Exception(
                    $"VRCFury haptic plug was asked to configure SPS on renderer," +
                    $" but it already has TPS or DPS. If you want to use SPS, use a regular shader" +
                    $" on the mesh instead.");
            }

            var m = MutableManager.MakeMutable(original);
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
            m.SetFloat(Enabled, plug.spsAnimatedEnabled);
            if (plug.spsAnimatedEnabled == 0) bakeRoot.active = false;
            m.SetFloat(Length, worldLength);
            m.SetFloat(nameID: BakedLength, worldLength);
            m.SetFloat(Overrun, plug.spsOverrun ? 1 : 0);
            m.SetTexture(Bake, spsBaked);
            m.SetFloat(SpsBlendshapeCount, spsBlendshapes.Count);
            m.SetFloat(SpsBlendshapeVertCount, skin.sharedMesh.vertexCount);
            for (var i = 0; i < spsBlendshapes.Count; i++) {
                var name = spsBlendshapes[i];
                if (skin.sharedMesh.HasBlendshape(name)) {
                    m.SetFloat("_SPS_Blendshape" + i, skin.GetBlendShapeWeight(name));
                }
            }
        }

        public static bool IsSps(Material mat) {
            return mat && mat.HasProperty(SpsBake);
        }
    }
}
