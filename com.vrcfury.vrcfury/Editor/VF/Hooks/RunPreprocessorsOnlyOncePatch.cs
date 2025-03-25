using UnityEditor;
using UnityEngine;
using VF.Utils;
using VRC.SDKBase.Editor.BuildPipeline;

namespace VF.Hooks {
    internal static class RunPreprocessorsOnlyOncePatch {
        [InitializeOnLoadMethod]
        private static void Init() {
            HarmonyUtils.Patch(
                typeof(RunPreprocessorsOnlyOncePatch),
                nameof(Prefix),
                typeof(VRCBuildPipelineCallbacks),
                "OnPreprocessAvatar"
            );
        }

        private static bool Prefix(GameObject __0, ref bool __result) {
            if (VrcfAvatarPreprocessor.GetRuns(__0) > 0) {
                Debug.LogWarning($"VRCFury is preventing OnPreprocessAvatar from running on {__0.name} because it already ran on that object");
                __result = true;
                return false;
            }
            return true;
        }
    }
}
