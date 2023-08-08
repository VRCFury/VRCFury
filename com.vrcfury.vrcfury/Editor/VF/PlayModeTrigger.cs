using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.SceneManagement;
using VF.Builder;
using VF.Builder.Exceptions;
using VF.Component;
using VF.Inspector;
using VF.Menu;
using VF.Model;
using VRC.SDK3.Avatars.Components;
using VRC.SDKBase.Editor.BuildPipeline;
using Object = UnityEngine.Object;

namespace VF {
    [InitializeOnLoad]
    public class PlayModeTrigger : IProcessSceneWithReport, IVRCSDKPostprocessAvatarCallback {
        static PlayModeTrigger()
        {
            VRCFuryComponent.StartCallback += StartCallback;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        public void OnProcessScene(Scene scene, BuildReport report)
        {
            foreach (var root in scene.GetRootGameObjects())
            {
                foreach (var avatar in root.GetComponentsInChildren<VRCAvatarDescriptor>(true))
                {
                    if (avatar.gameObject.activeInHierarchy)
                    {
                        VFGameObject avatarGameObject = avatar.owner();
                        var builder = new VRCFuryBuilder();
                        builder.SafeRun(avatarGameObject);
                        VRCFuryBuilder.StripAllVrcfComponents(avatarGameObject);
                    }
                    else
                    {
                        // if disabled, add activator to ensure process when avatar is enabled
                        avatar.gameObject.AddComponent<VRCFuryActivator>().hideFlags = HideFlags.HideInHierarchy | HideFlags.HideInInspector;
                    }
                }

                foreach (var socket in root.GetComponentsInChildren<VRCFuryHapticSocket>(true))
                {
                    var obj = socket.owner();
                    if (!obj.activeInHierarchy) continue;
                    if (ContainsAnyPrefabs(obj)) continue;
                    socket.Upgrade();
                    VRCFExceptionUtils.ErrorDialogBoundary(() =>
                    {
                        VRCFuryHapticSocketEditor.Bake(socket, onlySenders: true);
                    });
                    Object.DestroyImmediate(socket);
                }

                foreach (var plug in root.GetComponentsInChildren<VRCFuryHapticPlug>(true))
                {
                    var obj = plug.owner();
                    if (!obj.activeInHierarchy) continue;
                    if (ContainsAnyPrefabs(obj)) continue;
                    plug.Upgrade();
                    VRCFExceptionUtils.ErrorDialogBoundary(() =>
                    {
                        var mutableManager = new MutableManager(TempDir);
                        VRCFuryHapticPlugEditor.Bake(plug, onlySenders: true, mutableManager: mutableManager);
                    });
                    Object.DestroyImmediate(plug);
                }
            }
        }

        private static void StartCallback(VRCFuryComponent component)
        {
            if (!PlayModeMenuItem.Get()) return; // manually disabled

            var avatar = component.gameObject.GetComponent<VRCAvatarDescriptor>();
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

            // we may need to restart AudioLink because `Shader.SetGlobalTexture`, which is called in AudioLink
            // initialization, does not work well for newly created materials in VRCFury initialization.
            RestartAudiolink();
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
