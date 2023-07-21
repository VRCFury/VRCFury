using System;
using UnityEngine;
using VF.Component;

namespace VF.Builder.Haptics {
    public static class SpsConfigurer {
        private const string SpsEnabled = "_SPS_Enabled";
        private const string SpsLength = "_SPS_Length";
        private const string SpsOverrun = "_SPS_Overrun";
        private const string SpsBakedLength = "_SPS_BakedLength";
        private const string SpsBake = "_SPS_Bake";
        //private const string SpsChannel = "_SPS_Channel";

        public static Material ConfigureSpsMaterial(
            SkinnedMeshRenderer skin,
            Material original,
            float worldLength,
            float[] activeFromMask,
            MutableManager mutableManager,
            VRCFuryHapticPlug plug
        ) {
            if (DpsConfigurer.IsDps(original) || TpsConfigurer.IsTps(original)) {
                throw new Exception(
                    $"VRCFury haptic plug was asked to configure SPS on renderer {skin}," +
                    $" but it already has TPS or DPS. If you want to use SPS, use a regular shader" +
                    $" on the mesh instead.");
            }
            if (TpsConfigurer.IsLocked(original)) {
                throw new Exception(
                    "VRCFury Haptic Plug was asked to configure SPS on renderer {skin}, but material is poiyomi locked." +
                    "Please unlock the material using TPS to use this feature. (Don't worry, it will be re-locked during the upload).");
            }

            var m = mutableManager.MakeMutable(original);
            SpsPatcher.patch(m, mutableManager, plug.spsKeepImports);
            m.SetOverrideTag(SpsEnabled + "Animated", "1");
            m.SetFloat(SpsEnabled, plug.spsAnimatedEnabled);
            m.SetFloat(SpsLength, worldLength);
            m.SetFloat(SpsBakedLength, worldLength);
            m.SetFloat(SpsOverrun, plug.spsOverrun ? 1 : 0);
            var bake = SpsBaker.Bake(skin, mutableManager.GetTmpDir(), activeFromMask, false);
            m.SetTexture(SpsBake, bake);
            //m.SetFloat(SpsChannel, (int)channel);
            return m;
        }

        public static bool IsSps(Material mat) {
            return mat && mat.HasProperty(SpsBake);
        }
    }
}
