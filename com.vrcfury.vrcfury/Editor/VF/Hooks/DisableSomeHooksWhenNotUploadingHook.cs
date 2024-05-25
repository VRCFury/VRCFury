using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEngine;
using VF.Feature;
using VF.Utils;
using VRC.SDKBase.Editor.BuildPipeline;
using Debug = UnityEngine.Debug;

namespace VF.Hooks {
    /**
     * Poiyomi tries to enforce mat lockdown when preprocessor hooks are run, even if they are run by the emulator.
     * Prevent that from happening if we're in play mode.
     */
    internal static class DisableSomeHooksWhenNotUploadingHook {
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
                var typeName = callback.GetType().Name;
                if (typeName == "RemoveAvatarEditorOnly") {
                    callbacks.Remove(callback);
                } else if (typeName == "LockMaterialsOnUpload") {
                    Debug.Log($"VRCFury found {typeName} and is patching it to only run during actual uploads");
                    var newCallback = new InhibitWhenNotUploadingWrapper(callback);
                    callbacks.Remove(callback);
                    callbacks.Add(newCallback);
                }
            }

            callbacksField.SetValue(null, callbacks);
        }

        private class InhibitWhenNotUploadingWrapper : IVRCSDKPreprocessAvatarCallback {
            private IVRCSDKPreprocessAvatarCallback wrapped;
            
            // The VRCSDK makes an instance of this hook using the default constructor and we can't stop it >:(
            public InhibitWhenNotUploadingWrapper() {
                this.wrapped = null;
            }

            public InhibitWhenNotUploadingWrapper(IVRCSDKPreprocessAvatarCallback wrapped) {
                this.wrapped = wrapped;
            }

            public int callbackOrder => wrapped?.callbackOrder ?? 0;
            public bool OnPreprocessAvatar(GameObject avatarGameObject) {
                if (wrapped == null) return true;
                if (!IsActuallyUploadingHook.Get()) {
                    Debug.Log($"VRCFury inhibited IVRCSDKPreprocessAvatarCallback {wrapped.GetType().Name} from running because an upload isn't actually happening");
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

        public class VrcfRemoveEditorOnlyObjects : IVRCSDKPreprocessAvatarCallback {
            public int callbackOrder => -1024;
            public bool OnPreprocessAvatar(GameObject obj) {
                EditorOnlyUtils.RemoveEditorOnlyObjects(obj);
                return true;
            }
        }
        
        public class VrcfRemoveEditorOnlyComponents : IVRCSDKPreprocessAvatarCallback {
            public int callbackOrder => Int32.MaxValue;
            public bool OnPreprocessAvatar(GameObject obj) {
                EditorOnlyUtils.RemoveEditorOnlyComponents(obj);
                return true;
            }
        }
    }
}
