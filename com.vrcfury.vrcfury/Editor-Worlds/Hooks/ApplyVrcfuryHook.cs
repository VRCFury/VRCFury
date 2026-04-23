using System;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.SceneManagement;
using VF.Exceptions;
using VF.Features;
using VF.Menu;

namespace VF.Hooks {
    internal static class ApplyVrcfuryHook {
        public class UploadHook : IProcessSceneWithReport {
            public int callbackOrder => -10000;

            public void OnProcessScene(Scene scene, BuildReport report) {
                if (Application.isPlaying) return;
                Process(scene);
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSplashScreen)]
        private static void PlayHook() {
            Process(SceneManager.GetActiveScene());
        }

        public static void Process(Scene scene) {
            if (Application.isPlaying && !PlayModeMenuItem.Get()) return;
            if (IsActuallyUploadingWorldHook.Get() && !UseInUploadMenuItem.Get()) return;

            var success = VRCFExceptionUtils.ErrorDialogBoundary(() => {
                TmpFilePackage.Cleanup();
                BuildInjectUnityActions.Process(scene);
                BuildSps.Process(scene);
                BuildMarker.Process(scene);
                ComponentInjects.Wire(scene);
            });
            if (!success) {
                if (Application.isPlaying) {
                    EditorApplication.isPlaying = false;
                } else {
                    throw new Exception("VRCFury build callback failed");
                }
            }
        }
    }
}
