﻿using UnityEditor;
using UnityEngine;
using VF.Menu;
using VF.Model;
using VF.Utils;
using VRC.SDKBase.Editor.BuildPipeline;

namespace VF.Hooks {
    /**
     * Prevents components from being deleted during preprocessors when they need to be kept for debug reasons.
     */
    internal static class PreventComponentDeletionHook {
        [InitializeOnLoadMethod]
        private static void Init() {
            HarmonyUtils.Patch(
                typeof(PreventComponentDeletionHook),
                nameof(DestroyImmediatePrefix),
                typeof(Object),
                nameof(Object.DestroyImmediate)
            );
            
            HarmonyUtils.Patch(
                typeof(PreventComponentDeletionHook),
                nameof(PreprocessorPrefix),
                typeof(VRCBuildPipelineCallbacks),
                nameof(VRCBuildPipelineCallbacks.OnPreprocessAvatar)
            );
            
            HarmonyUtils.Patch(
                typeof(PreventComponentDeletionHook),
                nameof(PreprocessorPostfix),
                typeof(VRCBuildPipelineCallbacks),
                nameof(VRCBuildPipelineCallbacks.OnPreprocessAvatar),
                postfix: true
            );
        }

        private static bool inPreprocessor;

        private static bool DestroyImmediatePrefix(Object __0) {
            if (!IsActuallyUploadingHook.Get() && inPreprocessor) {
                if (__0 is VRCFuryTest || __0 is VRCFuryDebugInfo) {
                    // Keep it! Prevent the deletion!
                    return false;
                }
            }
            return true;
        }

        private static void PreprocessorPrefix() {
            inPreprocessor = true;
        }

        private static void PreprocessorPostfix() {
            inPreprocessor = false;
        }
    }
}
