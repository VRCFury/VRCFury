using System.Linq;
using UnityEngine;
using VF.Model;
using VF.Model.Feature;
using VF.Utils;

namespace VF.Hooks {
    internal static class ThryAutoAnchorHook {
        [ReflectionHelperOptional]
        private abstract class Reflection : ReflectionHelper {
            public static readonly HarmonyUtils.PatchObj Patch = HarmonyUtils.Patch(
                typeof(ThryAutoAnchorHook),
                nameof(OnPreprocessAvatarPrefix),
                "Thry.ThryEditor.UploadCallbacks.VRCAutoAnchor",
                "OnPreprocessAvatar"
            );
        }

        [VFInit]
        private static void Init() {
            if (!ReflectionHelper.IsReady<Reflection>()) return;
            Reflection.Patch.apply();
        }

        private static bool OnPreprocessAvatarPrefix(GameObject __0, ref bool __result) {
            if (!HasVrcfBoundingBoxFix(__0)) return true;
            Debug.Log($"VRCFury inhibited Thry VRCAutoAnchor from running because avatar uses VRCFury Anchor Override Fix");
            __result = true;
            return false;
        }

        private static bool HasVrcfBoundingBoxFix(GameObject avatar) {
            return avatar != null && avatar.asVf()
                .GetComponentsInSelfAndChildren<VRCFury>()
                .SelectMany(vrcf => vrcf.GetAllFeatures())
                .Any(feature => feature is AnchorOverrideFix || feature is AnchorOverrideFix2);
        }
    }
}
