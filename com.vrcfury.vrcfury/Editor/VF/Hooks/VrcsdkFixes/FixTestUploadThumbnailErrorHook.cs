using System.Reflection;
using UnityEditor;
using VF.Utils;

namespace VF.Hooks.VrcsdkFixes {
    /**
     * If you do Build and Test without a thumbnail set, the VRCSDK throws an exception after the build completes
     */
    internal static class FixTestUploadThumbnailErrorHook {
        [InitializeOnLoadMethod]
        private static void Init() {
            HarmonyUtils.Patch(
                typeof(FixTestUploadThumbnailErrorHook),
                nameof(Prefix),
                "VRC.SDKBase.Editor.Elements.Thumbnail",
                "SetImage"
            );
        }

        private static bool Prefix(string __0) {
            if (string.IsNullOrEmpty(__0)) {
                return false;
            }
            return true;
        }
    }
}
