using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEngine;
using VRC.SDKBase.Editor.BuildPipeline;

namespace VF.Hooks {
    /**
     * Poiyomi tries to enforce mat lockdown when preprocessor hooks are run, even if they are run by the emulator.
     * Prevent that from happening if we're in play mode.
     */
    internal static class NoPoiLockdownInPlayModeHook {
        [DidReloadScripts]
        public static void Init() {
            var callbacksClass =
                ReflectionUtils.GetTypeFromAnyAssembly("VRC.SDKBase.Editor.BuildPipeline.VRCBuildPipelineCallbacks");
            if (callbacksClass == null) return;
            var callbacksField = callbacksClass.GetField("_preprocessAvatarCallbacks", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            if (callbacksField == null) return;

            var _callbacks = callbacksField.GetValue(null);
            if (!(_callbacks is List<IVRCSDKPreprocessAvatarCallback> callbacks)) return;

            foreach (var callback in callbacks.ToArray()) {
                if (callback.GetType().Name == "LockMaterialsOnUpload") {
                    Debug.Log("VRCFury found LockMaterialsOnUpload and is patching it to not run in play mode");
                    var newCallback = new PoiyomiLockdownDuringUploadWrapper(callback);
                    callbacks.Remove(callback);
                    callbacks.Add(newCallback);
                }
            }

            callbacksField.SetValue(null, callbacks);
        }

        private class PoiyomiLockdownDuringUploadWrapper : IVRCSDKPreprocessAvatarCallback {
            private IVRCSDKPreprocessAvatarCallback wrapped;
            
            // The VRCSDK makes an instance of this hook using the default constructor and we can't stop it >:(
            public PoiyomiLockdownDuringUploadWrapper() {
                this.wrapped = null;
            }

            public PoiyomiLockdownDuringUploadWrapper(IVRCSDKPreprocessAvatarCallback wrapped) {
                this.wrapped = wrapped;
            }

            public int callbackOrder => wrapped?.callbackOrder ?? 0;
            public bool OnPreprocessAvatar(GameObject avatarGameObject) {
                if (wrapped == null) return true;
                if (EditorApplication.isPlaying) {
                    Debug.Log("VRCFury inhibited poiyomi from locking down all mats on the avatar");
                    return true;
                }

                try {
                    return wrapped.OnPreprocessAvatar(avatarGameObject);
                } catch (Exception e) {
                    Debug.LogException(new Exception("Poiyomi failed to lockdown materials: " + e.Message, e));
                    throw e;
                }
            }
        }
    }
}
