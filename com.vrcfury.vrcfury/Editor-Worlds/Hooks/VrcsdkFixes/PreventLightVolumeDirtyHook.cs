using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.SceneManagement;
using VF.Utils;

namespace VF.Hooks.VrcsdkFixes {
    /**
     * LightVolumes dirties the scene all the time for no reason. This hook inhibits those changes
     * and only applies them to the temp scene during upload.
     */
    internal static class PreventLightVolumeDirtyHook {
        private static bool allowEditModeCalls;
        private static readonly Dictionary<Type, MethodInfo> updateMethods
            = new Dictionary<Type, MethodInfo>();
        private static readonly Dictionary<Type, MethodInfo> onEnableMethods
            = new Dictionary<Type, MethodInfo>();

        [InitializeOnLoadMethod]
        private static void Init() {
            var rootType = ReflectionUtils.GetTypeFromAnyAssembly("VRCLightVolumes.LightVolume");
            if (rootType == null) return;
            foreach (var type in rootType.Assembly.GetTypes()) {
                var updateMethod = type.VFMethod("Update");
                if (updateMethod != null) {
                    updateMethods[type] = updateMethod;
                    HarmonyUtils.Patch(updateMethod, (typeof(PreventLightVolumeDirtyHook), nameof(GuardPrefix))).apply();
                    Debug.Log($"Patched {type.FullName}.Update");
                }
                var onEnableMethod = type.VFMethod("OnEnable");
                if (onEnableMethod != null) {
                    onEnableMethods[type] = onEnableMethod;
                    HarmonyUtils.Patch(onEnableMethod, (typeof(PreventLightVolumeDirtyHook), nameof(GuardPrefix))).apply();
                    Debug.Log($"Patched {type.FullName}.OnEnable");
                }
            }
        }

        public class SceneProcessor : IProcessSceneWithReport {
            public int callbackOrder => -1;

            public void OnProcessScene(Scene scene, BuildReport report) {
                allowEditModeCalls = true;
                try {
                    foreach (var component in scene.Roots().SelectMany(root => root.GetComponentsInSelfAndChildren<UnityEngine.Component>())) {
                        if (onEnableMethods.TryGetValue(component.GetType(), out var enable))
                            enable.Invoke(component, null);
                        if (updateMethods.TryGetValue(component.GetType(), out var update))
                            update.Invoke(component, null);
                    }
                } finally {
                    allowEditModeCalls = false;
                }
            }
        }

        private static bool GuardPrefix() {
            return Application.isPlaying || allowEditModeCalls;
        }
    }
}
