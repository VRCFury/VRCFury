#if !VRCSDK_3_10_5_OR_NEWER
using UnityEditor.Animations;
using VF.Utils;

namespace VF.Hooks.VrcsdkFixes {
    internal static class FixDupAnimatorWindowHook {
        private abstract class Reflection : ReflectionHelper {
            public static readonly HarmonyUtils.PatchObj Patch = HarmonyUtils.Patch(
                typeof(FixDupAnimatorWindowHook),
                nameof(Prefix),
                "AvatarParameterDriverEditor",
                "GetCurrentController"
            );
        }

        [VFInit]
        private static void Init() {
            if (!ReflectionHelper.IsReady<Reflection>()) return;
            Reflection.Patch.apply();
        }

        private static bool Prefix(ref AnimatorController __result) {
            __result = AnimatorControllerToolHelper.GetPreviewedAnimatorController();
            return false;
        }
    }
}
#endif

