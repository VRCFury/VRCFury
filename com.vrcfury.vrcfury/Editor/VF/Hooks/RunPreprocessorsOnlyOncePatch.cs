using System;
using UnityEditor;
using UnityEngine;
using VF.Builder;
using VF.Model;
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
            var go = (VFGameObject)__0;
            if (go.GetComponent<PreprocessorsRan>() != null) {
                if (Application.isPlaying) {
                    Debug.LogWarning($"VRCFury is preventing OnPreprocessAvatar from running on {__0.name} because it already ran on that object");
                    __result = true;
                    return false;
                } else {
                    ShowFailDialog();
                    __result = false;
                    return false;
                }
            } else {
                var c = go.AddComponent<PreprocessorsRan>();
                c.state = PreprocessorsRan.State.AddedByHarmonyPatch;
            }
            return true;
        }

        private static void ShowFailDialog() {
            EditorUtility.DisplayDialog(
                "Error",
                "Avatar preprocessors have already run on this object and can not be run again." +
                " You may be trying to upload a test copy of your avatar, which is not allowed." +
                " Running preprocessors multiple times can cause various bugs, such as as parameter desync on different platforms," +
                " worse performance due to optimizers running multiple times, and other unexpected outcomes.",
                "Ok"
            );
        }

        internal class StartCheck : IVRCSDKPreprocessAvatarCallback {
            public int callbackOrder => int.MinValue;
            public bool OnPreprocessAvatar(GameObject obj) {
                var go = (VFGameObject)obj;
                var c = go.GetComponent<PreprocessorsRan>();
                if (c != null) {
                    if (c.state == PreprocessorsRan.State.AddedByHarmonyPatch) {
                        c.state = PreprocessorsRan.State.FirstPass;
                    } else if (c.state == PreprocessorsRan.State.FirstPass) {
                        c.state = PreprocessorsRan.State.Finished;
                    } else if (c.state == PreprocessorsRan.State.Finished && !Application.isPlaying) {
                        ShowFailDialog();
                        return false;
                    }
                } else {
                    c = go.AddComponent<PreprocessorsRan>();
                    c.state = PreprocessorsRan.State.FirstPass;
                }
                return true;
            }
        }
    }
}
