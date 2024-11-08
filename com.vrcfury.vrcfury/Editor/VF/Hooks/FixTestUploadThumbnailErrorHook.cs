using System.Reflection;
using UnityEditor;
using UnityEngine;
using VF.Utils;

namespace VF.Hooks {
    /**
     * If you do Build and Test without a thumbnail set, the VRCSDK throws an exception after the build completes
     */
    internal static class FixTestUploadThumbnailErrorHook {
        [InitializeOnLoadMethod]
        private static void Init() {
            var methodToPatch = ReflectionUtils.GetTypeFromAnyAssembly("VRC.SDKBase.Editor.Elements.Thumbnail")?.GetMethod(
                "SetImage",
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance,
                null,
                new [] { typeof(string) },
                null
            );

            var prefix = typeof(FixTestUploadThumbnailErrorHook).GetMethod(
                nameof(Prefix),
                BindingFlags.Static | BindingFlags.NonPublic
            );

            HarmonyUtils.Patch(methodToPatch, prefix);
        }

        private static bool Prefix(string __0) {
            if (string.IsNullOrEmpty(__0)) {
                return false;
            }
            return true;
        }
    }
}
