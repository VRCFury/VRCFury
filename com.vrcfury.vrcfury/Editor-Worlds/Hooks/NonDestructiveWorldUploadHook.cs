using System;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using VF.Builder;
using VF.Utils;
using VRC.SDK3.Editor;

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
            public static readonly FieldInfo _worldUploadCancellationTokenSource =
                WorldBuilderType?.VFStaticField("_worldUploadCancellationTokenSource");
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
                    currentBuilder.OnSdkBuildError -= OnSdkRestore;
                    currentBuilder.OnSdkUploadFinish -= OnSdkRestore;
                    currentBuilder.OnSdkUploadError -= OnSdkRestore;
                }

                currentBuilder = builder;
                currentBuilder.OnSdkBuildError += OnSdkRestore;
                currentBuilder.OnSdkUploadFinish += OnSdkRestore;
                currentBuilder.OnSdkUploadError += OnSdkRestore;
            };
        }

        protected override void Process(Scene scene) {
            if (Application.isPlaying) return;

            if (restorePending) {
                RestoreOriginalScene();
            }

            if (!ReflectionHelper.IsReady<Reflection>()) {
                throw new Exception("VRCFury does not support this VRCSDK version");
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
            var buildsDir = $"{TmpFilePackage.GetPath()}/Builds";
            clonedScenePath = $"{buildsDir}/VRCFury World Upload Temp.unity";

            if (originalScenePath == clonedScenePath) {
                throw new Exception("You are attempting to upload a VRCFury Temp Scene. This is wrong. Switch back to the original scene file in your project.");
            }

            TmpFilePackage.Cleanup();
            VRCFuryAssetDatabase.CreateFolder(buildsDir);

            if (!AssetDatabase.CopyAsset(originalScenePath, clonedScenePath)) {
                ClearState();
                throw new Exception("VRCFury failed to clone the world scene before upload.");
            }

            restorePending = true;

            try {
                // This prevents the vrcsdk from aborting when the scene closes, and tricks it into uploading
                // our modified clone instead.
                var token = Reflection._worldUploadCancellationTokenSource.GetValue(null);
                Reflection._worldUploadCancellationTokenSource.SetValue(null, null);
                EditorSceneManager.OpenScene(clonedScenePath, OpenSceneMode.Single);
                Reflection._worldUploadCancellationTokenSource.SetValue(null, token);
                Reflection.FindScenes.Invoke(currentBuilder, new object[] { });
            } catch {
                try {
                    RestoreOriginalScene();
                } catch {
                }
                throw;
            }
        }

        private static void OnSdkRestore(object sender, string _) {
            RestoreOriginalScene();
        }

        private static void RestoreOriginalScene() {
            if (!restorePending) return;

            var reopenPath = originalScenePath;

            ClearState();

            if (!string.IsNullOrWhiteSpace(reopenPath)
                && AssetDatabase.LoadAssetAtPath<SceneAsset>(reopenPath) != null) {
                EditorSceneManager.OpenScene(reopenPath, OpenSceneMode.Single);
            } else {
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            }
        }

        private static void ClearState() {
            restorePending = false;
            originalScenePath = null;
            clonedScenePath = null;
        }
    }
}
