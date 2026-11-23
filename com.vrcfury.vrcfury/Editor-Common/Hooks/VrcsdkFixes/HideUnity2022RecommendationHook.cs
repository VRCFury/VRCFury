using System;
using VF.Utils;

namespace VF.Hooks.VrcsdkFixes {
    /**
     * Hides the SDK control panel's warning when its recommended Unity version is 2022.3.
     */
    internal static class HideUnity2022RecommendationHook {
        private const string Recommendation =
            "You are not using the recommended Unity version for the VRChat SDK. "
            + "Content built with this version may not work correctly. Please use Unity 2022.3";

        private abstract class Reflection : ReflectionHelper {
            public static readonly HarmonyUtils.PatchObj Patch = HarmonyUtils.Patch(
                typeof(HideUnity2022RecommendationHook),
                nameof(FilterWarning),
                "VRCSdkControlPanel",
                "OnGUIWarning"
            );
        }

        [VFInit]
        private static void Init() {
            if (ReflectionHelper.IsReady<Reflection>()) Reflection.Patch.apply();
        }

        private static bool FilterWarning(string __1) =>
            __1 != Recommendation && __1?.StartsWith(Recommendation + ".", StringComparison.Ordinal) != true;
    }
}
