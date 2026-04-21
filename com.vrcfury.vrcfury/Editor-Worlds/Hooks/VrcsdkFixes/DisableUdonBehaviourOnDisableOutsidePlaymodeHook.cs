using UnityEditor;
using VF.Utils;

namespace VF.Hooks.VrcsdkFixes {
    /**
     * When UdonBehaviours run OnDisable during play mode teardown, lots of things break.
     * This seemingly doesn't happen in game, so it shouldn't happen in play mode either.
     * https://feedback.vrchat.com/sdk-bug-reports/p/udon-behaviours-shouldnt-run-in-editor-when-play-mode-is-exiting
     */
    internal static class DisableUdonBehaviourOnDisableOutsidePlaymodeHook {
        private abstract class Reflection : ReflectionHelper {
            public static readonly HarmonyUtils.PatchObj Patch = HarmonyUtils.Patch(
                typeof(DisableUdonBehaviourOnDisableOutsidePlaymodeHook),
                nameof(Prefix),
                "VRC.Udon.UdonBehaviour",
                "OnDisable"
            );
        }

        [InitializeOnLoadMethod]
        private static void Init() {
            if (!ReflectionHelper.IsReady<Reflection>()) return;
            Reflection.Patch.apply();
        }

        private static bool Prefix() {
            return EditorApplication.isPlayingOrWillChangePlaymode;
        }
    }
}
