using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor.Callbacks;
using VF.Utils;
using VRC.SDKBase.Editor.BuildPipeline;

namespace VF.Hooks.VrcsdkFixes {
    /**
     * The VRCSDK removes all EditorOnly objects and components at -1024 by default.
     * This defers the component removal until the very end of the build, so all systems have a chance to use them.
     * There's really no reason to remove them earlier.
     */
    internal static class KeepEditorOnlyComponentsLongerHook {

        private abstract class Reflection : ReflectionHelper {
            public static readonly Type VRCBuildPipelineCallbacks =
                ReflectionUtils.GetTypeFromAnyAssembly("VRC.SDKBase.Editor.BuildPipeline.VRCBuildPipelineCallbacks");
            public static readonly FieldInfo preprocessAvatarCallbacks = VRCBuildPipelineCallbacks?
                .VFStaticField("_preprocessAvatarCallbacks");
        }

        [DidReloadScripts]
        public static void Init() {
            if (!ReflectionHelper.IsReady<Reflection>()) return;

            var _callbacks = Reflection.preprocessAvatarCallbacks.GetValue(null);
            if (!(_callbacks is List<IVRCSDKPreprocessAvatarCallback> callbacks)) return;

            foreach (var callback in callbacks.ToArray()) {
                var typeName = callback.GetType().Name;
                if (typeName == "RemoveAvatarEditorOnly") {
                    callbacks.Remove(callback);
                }
            }

            Reflection.preprocessAvatarCallbacks.SetValue(null, callbacks);
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
                EditorOnlyUtils.RemoveEditorOnlyComponents(obj);
            }
        }
    }
}
