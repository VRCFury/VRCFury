using UnityEditor;
using UnityEngine;
using VF.Utils;

namespace VF.Hooks.VrcsdkFixes {
    /**
     * The vrcsdk updates collider transforms whenever you focus the avatar descriptor, but this makes it look like
     * global collider tricks aren't working. We can disable this updater while in play mode so that it shows the tricks
     * correctly.
     */
    internal static class DisableColliderUpdateInPlayModeHook {
        private abstract class Reflection : ReflectionHelper {
            public static readonly HarmonyUtils.PatchObj Patch = HarmonyUtils.Patch(
                typeof(DisableColliderUpdateInPlayModeHook),
                nameof(Prefix),
                "AvatarDescriptorEditor3",
                "UpdateAutoColliders"
            );
        }

        [InitializeOnLoadMethod]
        private static void Init() {
            if (!ReflectionHelper.IsReady<Reflection>()) return;
            Reflection.Patch.apply();
        }

        private static bool Prefix() {
            if (Application.isPlaying) {
                return false;
            }
            return true;
        }
    }
}
