using System.Linq;
using UnityEditor;
using VF.Builder;
using VF.Utils;

namespace VF.Hooks.VrcsdkFixes {
    internal static class HideXrSettingsHook {
        [InitializeOnLoadMethod]
        private static void Init() {
            // Delay by one frame to make sure the temp package is ready
            EditorApplication.delayCall += () => {
                Scheduler.Schedule(Check, 1000);
            };
        }

        private static void Check() {
            if (!AssetDatabase.IsValidFolder("Assets/XR")) return;
            var tmpFolder = TmpFilePackage.GetPathNullable();
            if (tmpFolder == null) return;
            var subPaths = AssetDatabase.FindAssets("", new[] { "Assets/XR" })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(p => !string.IsNullOrEmpty(p))
                .OrderBy(p => p);
            var hasUnknownFile = false;
            foreach (var p in subPaths) {
                var assetType = AssetDatabase.LoadMainAssetAtPath(p)?.GetType().Name;
                if (AssetDatabase.IsValidFolder(p) || assetType == "OculusSettings" || assetType == "OculusLoader" ||
                    assetType == "XRManagerSettings" || assetType == "XRGeneralSettingsPerBuildTarget") continue;
                hasUnknownFile = true;
            }

            if (!hasUnknownFile) {
                VRCFuryAssetDatabase.CreateFolder($"{tmpFolder}/XR");
                var uniq = VRCFuryAssetDatabase.GetUniquePath($"{tmpFolder}/XR", "XR");
                AssetDatabase.MoveAsset("Assets/XR", uniq);
            }
        }
    }
}
