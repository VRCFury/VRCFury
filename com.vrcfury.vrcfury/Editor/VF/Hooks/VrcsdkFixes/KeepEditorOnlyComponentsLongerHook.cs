using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor.Callbacks;
using UnityEngine;
using VF.Utils;
using VRC.SDKBase.Editor.BuildPipeline;

namespace VF.Hooks.VrcsdkFixes {
    /**
     * The VRCSDK removes all EditorOnly objects and components at -1024 by default.
     * This defers the component removal until the very end of the build, so all systems have a chance to use them.
     * There's really no reason to remove them earlier.
     */
    internal static class KeepEditorOnlyComponentsLongerHook {
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
                }
            }

            callbacksField.SetValue(null, callbacks);
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
                if (IsActuallyUploadingHook.Get()) {
                    EditorOnlyUtils.RemoveEditorOnlyComponents(obj);
                }
                return true;
            }
        }
    }
}
