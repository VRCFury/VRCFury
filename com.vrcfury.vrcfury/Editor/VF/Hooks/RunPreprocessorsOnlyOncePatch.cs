using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEditor;
using UnityEngine;
using VF.Builder;
using VF.Model;
using VF.Utils;
using VRC.SDKBase.Editor.BuildPipeline;

namespace VF.Hooks {
    internal static class RunPreprocessorsOnlyOncePatch {

        private enum PlayModeState {
            Fresh,
            HarmonyPatchCalled,
            FirstPass,
            Finished
        }
        private static readonly Dictionary<Transform, PlayModeState> playModeState
            = new Dictionary<Transform, PlayModeState>();

        private static PlayModeState GetPlayModeState(Transform t) =>
            playModeState.TryGetValue(t, out var s) ? s : PlayModeState.Fresh;

        [InitializeOnLoadMethod]
        private static void Init() {
            EditorApplication.playModeStateChanged += state => {
                if (state == PlayModeStateChange.ExitingPlayMode) {
                    playModeState.Clear();
                }
            };
            
            HarmonyUtils.Patch(
                typeof(RunPreprocessorsOnlyOncePatch),
                nameof(Prefix),
                typeof(VRCBuildPipelineCallbacks),
                nameof(VRCBuildPipelineCallbacks.OnPreprocessAvatar)
            );
        }

        private static bool Prefix(GameObject __0, ref bool __result) {
            var go = (VFGameObject)__0;
            if (Application.isPlaying) {
                if (go.GetComponent<VRCFuryTest>() != null || GetPlayModeState(go) != PlayModeState.Fresh) {
                    Debug.LogWarning($"VRCFury is preventing OnPreprocessAvatar from running on {__0.name} because it already ran on that object");
                    __result = true;
                    return false;
                }
                playModeState[go] = PlayModeState.HarmonyPatchCalled;
            } else {
                if (go.GetComponent<VRCFuryTest>() != null) {
                    ShowFailDialog();
                    __result = false;
                    return false;
                }
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

        public static bool ShouldRunPreprocessors(VFGameObject obj) {
            return !Application.isPlaying || GetPlayModeState(obj) != PlayModeState.Finished;
        }
        public static bool ShouldStartPreprocessors(VFGameObject obj) {
            return GetPlayModeState(obj) == PlayModeState.Fresh;
        }

        internal class StartCheck : IVRCSDKPreprocessAvatarCallback {
            public int callbackOrder => int.MinValue;
            public bool OnPreprocessAvatar(GameObject obj) {
                var go = (VFGameObject)obj;

                if (Application.isPlaying) {
                    var state = GetPlayModeState(go);
                    if (go.GetComponent<VRCFuryTest>() != null || state == PlayModeState.Finished) {
                        playModeState[go] = PlayModeState.Finished;
                    } else if (state == PlayModeState.HarmonyPatchCalled || state == PlayModeState.Fresh) {
                        playModeState[go] = PlayModeState.FirstPass;
                    } else if (state == PlayModeState.FirstPass) {
                        playModeState[go] = PlayModeState.Finished;
                    }
                } else {
                    if (go.GetComponent<VRCFuryTest>() != null) {
                        ShowFailDialog();
                        return false;
                    }
                }
                
                return true;
            }
        }
    }
}
