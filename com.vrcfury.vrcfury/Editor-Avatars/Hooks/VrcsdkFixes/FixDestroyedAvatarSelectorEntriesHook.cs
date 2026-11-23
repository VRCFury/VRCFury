using System.Collections.Generic;
using VF.Utils;
using VRC.SDKBase;

namespace VF.Hooks.VrcsdkFixes {
    /**
     * The SDK's avatar dropdown can still reference destroyed descriptors until it refreshes.
     * Ignore those descriptors when formatting names, without changing the popup's choice list.
     * https://feedback.vrchat.com/sdk-bug-reports/p/sdk-3110-beta1-removed-descriptors-can-cause-vrcsdk-panel-to-crash-until-reloade
     */
    internal static class FixDestroyedAvatarSelectorEntriesHook {
        private abstract class Reflection : ReflectionHelper {
            public static readonly HarmonyUtils.PatchObj Patch = HarmonyUtils.Patch(
                typeof(FixDestroyedAvatarSelectorEntriesHook),
                nameof(Prefix),
                "VRC.SDK3A.Editor.Elements.AvatarSelector",
                "FormatAvatarName"
            );
        }

        [VFInit]
        private static void Init() {
            if (ReflectionHelper.IsReady<Reflection>()) Reflection.Patch.apply();
        }

        private static bool Prefix(
            ref List<VRC_AvatarDescriptor> __0,
            VRC_AvatarDescriptor __1,
            ref string __result
        ) {
            if (__1 == null) {
                __result = "(Missing Avatar)";
                return false;
            }

            if (__0.Exists(avatar => avatar == null)) {
                __0 = __0.FindAll(avatar => avatar != null);
            }
            return true;
        }
    }
}
