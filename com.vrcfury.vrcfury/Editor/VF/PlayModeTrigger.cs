using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using VF.Builder;
using VF.Builder.Exceptions;
using VF.Component;
using VF.Inspector;
using VF.Menu;
using VRC.SDK3.Avatars.Components;
using VRC.SDKBase.Editor.BuildPipeline;
using Object = UnityEngine.Object;

namespace VF {
    [InitializeOnLoad]
    public class PlayModeTrigger : IVRCSDKPostprocessAvatarCallback {
        static PlayModeTrigger()
        {
            VRCFuryComponent.AwakeOrStart += AwakeOrStart;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        // We want to apply VRCFury on Awake to perform before Av3Emu or GestureManager
        // but when apply VRCFury for newly enabled avatars on Awake, editor may crashes.
        // As a workaround, apply on Awake until scene load is finished and apply on Start
        // for newly enabled avatars.
        private static bool _applyOnAwake = true;

        private static void AwakeOrStart(bool isAwake, VRCFuryComponent component)
        {
            if (!PlayModeMenuItem.Get()) return; // manually disabled
            if (isAwake != _applyOnAwake) return;

            var avatar = component.gameObject.GetComponentInParent<VRCAvatarDescriptor>();
            if (avatar != null)
            {
                VFGameObject avatarGameObject = avatar.gameObject;
                if (ContainsAnyPrefabs(avatarGameObject)) return;

                var builder = new VRCFuryBuilder();
                builder.SafeRun(avatarGameObject);
                VRCFuryBuilder.StripAllVrcfComponents(avatarGameObject);
            }
            else
            {
                // even if it's not in avatar, process haptic socket and plug
                if (ContainsAnyPrefabs(component.gameObject)) return;

                switch (component)
                {
                    case VRCFuryHapticSocket socket:
                        socket.Upgrade();
                        VRCFExceptionUtils.ErrorDialogBoundary(() =>
                        {
                            VRCFuryHapticSocketEditor.Bake(socket, onlySenders: true);
                        });
                        Object.DestroyImmediate(socket);
                        break;
                    case VRCFuryHapticPlug plug:
                        plug.Upgrade();
                        VRCFExceptionUtils.ErrorDialogBoundary(() =>
                        {
                            var mutableManager = new MutableManager(TempDir);
                            VRCFuryHapticPlugEditor.Bake(plug, onlySenders: true, mutableManager: mutableManager);
                        });
                        Object.DestroyImmediate(plug);
                        break;
                }
            }

            if (!isAwake)
            {
                // we may need to restart AudioLink because `Shader.SetGlobalTexture`, which is called in AudioLink
                // initialization, does not work well for newly created materials in VRCFury initialization.
                RestartAudiolink();
            }
        }

        private static void RestartAudiolink() {
            var alComponentType = ReflectionUtils.GetTypeFromAnyAssembly("VRCAudioLink.AudioLink");
            if (alComponentType == null) return;
            foreach (var gm in Object.FindObjectsOfType(alComponentType).OfType<UnityEngine.Component>()) {
                Debug.Log("Restarting AudioLink ...");
                if (gm.gameObject.activeSelf) {
                    gm.gameObject.SetActive(false);
                    gm.gameObject.SetActive(true);
                }
            }
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            switch (state)
            {
                case PlayModeStateChange.EnteredEditMode:
                    _applyOnAwake = true;
                    break;
                case PlayModeStateChange.ExitingEditMode:
                    if (PlayModeMenuItem.Get()) {
                        var rootObjects = VFGameObject.GetRoots();
                        VRCFPrefabFixer.Fix(rootObjects);
                    }

                    _tmpDir = null;
                    _probablyUploading = null;
                    break;
                case PlayModeStateChange.EnteredPlayMode:
                    _applyOnAwake = false;
                    // force compute ProbablyUploading before removing AboutToUploadKey
                    var _ = ProbablyUploading;
                    EditorPrefs.DeleteKey(AboutToUploadKey);
                    break;
                case PlayModeStateChange.ExitingPlayMode:
                    break;
            }
        }

        // This should absolutely always be false in play mode, but we check just in case
        private static bool ContainsAnyPrefabs(VFGameObject obj) {
            foreach (var t in obj.GetSelfAndAllChildren()) {
                if (PrefabUtility.IsPartOfAnyPrefab(t)) {
                    return true;
                }
            }
            return false;
        }
        

        #region probablyUploading
        private const string AboutToUploadKey = "vrcf_vrcAboutToUpload";
        private static bool? _probablyUploading = null;
        private static float Now() => (float)EditorApplication.timeSinceStartup;

        public int callbackOrder => int.MaxValue;

        public void OnPostprocessAvatar()
        {
            SessionState.SetFloat(AboutToUploadKey, Now());
        }

        private static bool ProbablyUploading
        {
            get
            {
                if (_probablyUploading is bool probablyUploading) return probablyUploading;
                var aboutToUploadTime = SessionState.GetFloat(AboutToUploadKey, 0);
                var now = Now();
                probablyUploading = aboutToUploadTime <= now && aboutToUploadTime > now - 10;
                _probablyUploading = probablyUploading;
                return probablyUploading;
            }
        }
        #endregion

        #region TempDir

        private static string _tmpDir;

        private static string TempDir
        {
            get
            {
                if (_tmpDir == null)
                {
                    var tmpDirParent = TmpFilePackage.GetPath() + "/PlayMode";
                    VRCFuryAssetDatabase.DeleteFolder(tmpDirParent);
                    _tmpDir = $"{tmpDirParent}/{DateTime.Now.ToString("yyyyMMdd-HHmmss")}";
                    Directory.CreateDirectory(_tmpDir);
                }

                return _tmpDir;
            }
        }

        #endregion
    }
}
