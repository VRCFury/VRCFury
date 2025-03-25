using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor.Callbacks;
using UnityEngine;
using VF.Builder;
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

        public class VrcfRemoveEditorOnlyObjects : VrcfAvatarPreprocessor {
            protected override int order => -1024;
            protected override void Process(VFGameObject obj) {
                EditorOnlyUtils.RemoveEditorOnlyObjects(obj);
            }
        }
        
        public class VrcfRemoveEditorOnlyComponents : VrcfAvatarPreprocessor {
            protected override int order => Int32.MaxValue;
            protected override void Process(VFGameObject obj) {
                if (IsActuallyUploadingHook.Get()) {
                    EditorOnlyUtils.RemoveEditorOnlyComponents(obj);
                }
            }
        }
    }
}
