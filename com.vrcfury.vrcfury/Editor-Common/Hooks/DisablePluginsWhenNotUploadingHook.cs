using System;
using UnityEngine;
using VF.Utils;

/*
 * VRCFury sometimes makes upload callbacks run when an upload isn't actually happening,
 * such as play mode and when building test copies.
 * Many plugins have callbacks that are way too expensive for these tests, and can safely be skipped.
 * This hook makes them skip unless an upload is actually happening.
 */
namespace VF.Hooks {
    internal static class DisablePluginsWhenNotUploadingHook {
        public static Func<bool> getIsActuallyUploading;

        [ReflectionHelperOptional]
        private abstract class Reflection : ReflectionHelper {
            private static readonly Type LockMaterialsOnUpload =
                PoiyomiUtils.ShaderOptimizer?.VFNestedType("LockMaterialsOnUpload");
            private static readonly Type LockMaterialsOnWorldUpload =
                PoiyomiUtils.ShaderOptimizer?.VFNestedType("LockMaterialsOnWorldUpload");

            public static readonly HarmonyUtils.PatchObj PoiPatchPreprocessAvatar = HarmonyUtils.Patch(
                typeof(DisablePluginsWhenNotUploadingHook),
                nameof(Prefix),
                LockMaterialsOnUpload,
                "OnPreprocessAvatar"
            );
            public static readonly HarmonyUtils.PatchObj PoiPatchBuildRequested = HarmonyUtils.Patch(
                typeof(DisablePluginsWhenNotUploadingHook),
                nameof(Prefix),
                LockMaterialsOnWorldUpload,
                "VRC.SDKBase.Editor.BuildPipeline.IVRCSDKBuildRequestedCallback.OnBuildRequested"
            );

            public static readonly HarmonyUtils.PatchObj LilPatchPreprocessAvatar = HarmonyUtils.Patch(
                typeof(DisablePluginsWhenNotUploadingHook),
                nameof(Prefix),
                "lilToon.External.VRChatModule",
                "OnPreprocessAvatar"
            );
            public static readonly HarmonyUtils.PatchObj LilPatchBuildRequested = HarmonyUtils.Patch(
                typeof(DisablePluginsWhenNotUploadingHook),
                nameof(Prefix),
                "lilToon.External.VRChatModule",
                "OnBuildRequested"
            );
            public static readonly HarmonyUtils.PatchObj UdonSharpBuildCompilePatch = HarmonyUtils.Patch(
                typeof(DisablePluginsWhenNotUploadingHook),
                nameof(Prefix),
                "UdonSharpEditor.UdonSharpBuildCompile",
                "OnBuildRequested"
            );
        }

        [UnityEditor.InitializeOnLoadMethod]
        private static void Init() {
            ApplyIfReady(Reflection.PoiPatchPreprocessAvatar);
            ApplyIfReady(Reflection.PoiPatchBuildRequested);
            ApplyIfReady(Reflection.LilPatchPreprocessAvatar);
            ApplyIfReady(Reflection.LilPatchBuildRequested);
            ApplyIfReady(Reflection.UdonSharpBuildCompilePatch);
        }

        private static bool Prefix(ref bool __result, object __instance) {
            if (getIsActuallyUploading != null && !getIsActuallyUploading()) {
                Debug.Log($"VRCFury inhibited {__instance.GetType().FullName} from running because an upload isn't actually happening");
                __result = true;
                return false;
            }
            return true;
        }

        private static void ApplyIfReady(HarmonyUtils.PatchObj patch) {
            if (patch == null || patch.error != null) return;
            patch.apply();
        }
    }
}
