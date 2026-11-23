using UnityEngine;
using VF.Builder;
using VF.Menu;
using VF.Utils;
using VRC.SDK3.Avatars.Components;

namespace VF.Hooks.VrcsdkFixes {
    /**
     * Lets an avatar reach preprocessing when VRCFury will add its missing Animator.
     */
    internal static class AllowMissingAnimatorHook {
        private abstract class Reflection : ReflectionHelper {
            public static readonly HarmonyUtils.PatchObj Patch = HarmonyUtils.Patch(
                typeof(AllowMissingAnimatorHook),
                nameof(FilterError),
                "VRCSdkControlPanel",
                "OnGUIError"
            );
        }

        [VFInit]
        private static void Init() {
            if (ReflectionHelper.IsReady<Reflection>()) Reflection.Patch.apply();
        }

        private static bool FilterError(Object __0, string __1) {
            if (__1 != "This avatar does not contain an Animator, you need to add an Animator component for the avatar to work"
                || __0 is not VRCAvatarDescriptor avatar || avatar == null) return true;
            if (!UseInUploadMenuItem.Get() || (Application.isPlaying && !PlayModeMenuItem.Get())) return true;
            return !VRCFuryBuilder.ShouldRun(avatar.owner());
        }
    }
}
