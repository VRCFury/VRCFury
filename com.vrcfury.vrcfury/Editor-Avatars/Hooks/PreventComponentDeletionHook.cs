using UnityEditor;
using UnityEngine;
using VF.Component;
using VF.Menu;
using VF.Model;
using VF.Utils;
using VRC.SDKBase.Editor.BuildPipeline;

namespace VF.Hooks {
    /**
     * Prevents components from being deleted during preprocessors when they need to be kept for debug reasons.
     */
    internal static class PreventComponentDeletionHook {
        private abstract class Reflection : ReflectionHelper {
            public static readonly HarmonyUtils.PatchObj DestroyImmediatePatch = HarmonyUtils.Patch(
                typeof(PreventComponentDeletionHook),
                nameof(DestroyImmediatePrefix),
                typeof(Object),
                nameof(Object.DestroyImmediate)
            );
            public static readonly HarmonyUtils.PatchObj PreprocessorPatch = HarmonyUtils.Patch(
                typeof(PreventComponentDeletionHook),
                nameof(PreprocessorPrefix),
                typeof(VRCBuildPipelineCallbacks),
                nameof(VRCBuildPipelineCallbacks.OnPreprocessAvatar)
            );
            public static readonly HarmonyUtils.PatchObj PreprocessorFinalizerPatch = HarmonyUtils.Patch(
                typeof(PreventComponentDeletionHook),
                nameof(PreprocessorFinalizer),
                typeof(VRCBuildPipelineCallbacks),
                nameof(VRCBuildPipelineCallbacks.OnPreprocessAvatar),
                patchMode: HarmonyUtils.PatchMode.Finalizer
            );
        }

        [InitializeOnLoadMethod]
        private static void Init() {
            if (!ReflectionHelper.IsReady<Reflection>()) return;
            Reflection.DestroyImmediatePatch.apply();
            Reflection.PreprocessorPatch.apply();
            Reflection.PreprocessorFinalizerPatch.apply();
        }

        private static bool inPreprocessor;

        private static bool DestroyImmediatePrefix(Object __0) {
            if (!IsActuallyUploadingHook.Get() && inPreprocessor) {
                if (__0 is VRCFuryTest || __0 is VRCFuryDebugInfo || __0 is VRCFuryPlayComponent) {
                    // Keep it! Prevent the deletion!
                    return false;
                }
            }
            return true;
        }

        private static void PreprocessorPrefix() {
            inPreprocessor = true;
        }

        private static void PreprocessorFinalizer() {
            inPreprocessor = false;
        }
    }
}
