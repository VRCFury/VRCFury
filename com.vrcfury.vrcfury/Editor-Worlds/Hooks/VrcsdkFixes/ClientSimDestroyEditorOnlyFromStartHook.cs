using UnityEditor;
using VF.Utils;

namespace VF.Hooks.VrcsdkFixes {
    /**
     * ClientSimRuntimeLoader.StartClientSim calls DestroyEditorOnly during BeforeSceneLoad.
     * With Reload Scene enabled this does nothing (scene roots are not loaded yet).
     * With Reload Scene disabled this can delete objects before network-id setup and break play mode.
     * Skip only the StartClientSim callsite; keep DestroyEditorOnly in OnAfterSceneLoad.
     * https://feedback.vrchat.com/sdk-bug-reports/p/editoronly-objects-are-deleted-too-early-by-clientsim-when-you-use-play-mode-opt
     */
    internal static class ClientSimDestroyEditorOnlyFromStartHook {
        [ReflectionHelperOptional]
        private abstract class Reflection : ReflectionHelper {
            public static readonly HarmonyUtils.PatchObj PatchStartPrefix = HarmonyUtils.Patch(
                typeof(ClientSimDestroyEditorOnlyFromStartHook),
                nameof(OnStartClientSimPrefix),
                "VRC.SDK3.ClientSim.ClientSimRuntimeLoader",
                "StartClientSim"
            );
            public static readonly HarmonyUtils.PatchObj PatchStartFinalizer = HarmonyUtils.Patch(
                typeof(ClientSimDestroyEditorOnlyFromStartHook),
                nameof(OnStartClientSimFinalizer),
                "VRC.SDK3.ClientSim.ClientSimRuntimeLoader",
                "StartClientSim",
                HarmonyUtils.PatchMode.Finalizer
            );
            public static readonly HarmonyUtils.PatchObj PatchDestroyPrefix = HarmonyUtils.Patch(
                typeof(ClientSimDestroyEditorOnlyFromStartHook),
                nameof(OnDestroyEditorOnlyPrefix),
                "VRC.SDK3.ClientSim.ClientSimRuntimeLoader",
                "DestroyEditorOnly"
            );
        }

        private static int startClientSimDepth;

        [VFInit]
        private static void Init() {
            if (!ReflectionHelper.IsReady<Reflection>()) return;
            Reflection.PatchStartPrefix.apply();
            Reflection.PatchStartFinalizer.apply();
            Reflection.PatchDestroyPrefix.apply();
        }

        private static void OnStartClientSimPrefix() {
            startClientSimDepth++;
        }

        private static void OnStartClientSimFinalizer() {
            if (startClientSimDepth > 0) startClientSimDepth--;
        }

        private static bool OnDestroyEditorOnlyPrefix() {
            return startClientSimDepth <= 0;
        }
    }
}
