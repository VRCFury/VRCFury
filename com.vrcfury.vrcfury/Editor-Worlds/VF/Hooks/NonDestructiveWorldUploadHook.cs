using System;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using VF.Builder;
using VF.Builder.Exceptions;
using VF.Utils;
using VRC.SDK3.Editor;
using VRC.SDKBase.Editor.BuildPipeline;

namespace VF.Hooks {
    /**
     * Adds non-destructive workflow to world uploads, just like avatars.
     * During a non-play mode upload, the scene will be cloned, the VRCSDK will be tricked into looking at the clone,
     * and all modifications afterward will be made to the temporary clone.
     */
    internal class NonDestructiveWorldUploadHook : VrcfWorldPreprocessor {
        private abstract class Reflection : ReflectionHelper {
            public static readonly Type WorldBuilderType =
                ReflectionUtils.GetTypeFromAnyAssembly("VRC.SDK3.Editor.VRCSdkControlPanelWorldBuilder");
            public static readonly MethodInfo FindScenes = WorldBuilderType?.VFMethod("FindScenes");
        }

        private static IVRCSdkWorldBuilderApi currentBuilder;
        private static string originalScenePath;
        private static string clonedScenePath;
        private static bool restorePending;

        protected override int order => int.MinValue + 100;

        [InitializeOnLoadMethod]
        private static void Init() {
            VRCSdkControlPanel.OnSdkPanelEnable += (_, _2) => {
                if (!VRCSdkControlPanel.TryGetBuilder<IVRCSdkWorldBuilderApi>(out var builder)) return;
                if (ReferenceEquals(currentBuilder, builder)) return;

                if (currentBuilder != null) {
                    currentBuilder.OnSdkBuildFinish -= OnSdkBuildFinish;
                    currentBuilder.OnSdkBuildError -= OnSdkBuildError;
                }

                currentBuilder = builder;
                currentBuilder.OnSdkBuildFinish += OnSdkBuildFinish;
                currentBuilder.OnSdkBuildError += OnSdkBuildError;
            };
        }

        protected override void Process(Scene scene) {
            if (Application.isPlaying) return;

            if (restorePending) {
                RestoreOriginalScene();
            }

            if (currentBuilder == null) {
                throw new Exception("VRCFury could not find the VRChat world builder.");
            }

            if (!EditorSceneManager.SaveOpenScenes()) {
                throw new Exception("Unity scenes failed to save.");
            }
            AssetDatabase.SaveAssets();

            var originalScene = SceneManager.GetActiveScene();
            if (!originalScene.IsValid() || string.IsNullOrWhiteSpace(originalScene.path)) {
                throw new Exception("VRCFury requires the active world scene to be saved before upload.");
            }

            originalScenePath = originalScene.path;
            TmpFilePackage.Cleanup();
            var buildsDir = $"{TmpFilePackage.GetPath()}/Builds";
            VRCFuryAssetDatabase.CreateFolder(buildsDir);
            clonedScenePath = $"{buildsDir}/VRCFury World Upload Temp.unity";

            if (!AssetDatabase.CopyAsset(originalScenePath, clonedScenePath)) {
                ClearState();
                throw new Exception("VRCFury failed to clone the world scene before upload.");
            }

            restorePending = true;

            try {
                EditorSceneManager.OpenScene(clonedScenePath, OpenSceneMode.Single);
                RefreshWorldBuilderScenes();
            } catch {
                try {
                    RestoreOriginalScene();
                } catch {
                }
                throw;
            }
        }

        private static void OnSdkBuildFinish(object sender, string _) {
            RestoreOriginalScene();
        }

        private static void OnSdkBuildError(object sender, string _) {
            RestoreOriginalScene();
        }

        private static void RefreshWorldBuilderScenes() {
            if (!ReflectionHelper.IsReady<Reflection>()) {
                throw new Exception(
                    "VRCFury does not support this VRCSDK version because VRCSdkControlPanelWorldBuilder.FindScenes could not be found."
                );
            }

            Reflection.FindScenes.Invoke(currentBuilder, new object[] { });
        }

        private static void RestoreOriginalScene() {
            if (!restorePending) return;

            var reopenPath = originalScenePath;

            ClearState();

            if (!string.IsNullOrWhiteSpace(reopenPath)
                && AssetDatabase.LoadAssetAtPath<SceneAsset>(reopenPath) != null) {
                EditorSceneManager.OpenScene(reopenPath, OpenSceneMode.Single);
                if (currentBuilder != null && ReflectionHelper.IsReady<Reflection>()) {
                    Reflection.FindScenes.Invoke(currentBuilder, new object[] { });
                }
            }
        }

        private static void ClearState() {
            restorePending = false;
            originalScenePath = null;
            clonedScenePath = null;
        }
    }
}
