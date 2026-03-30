using System.Reflection;
using UnityEditor;
using UnityEngine;
using VF.Builder;
using VF.Menu;
using VF.Utils;
#if VRC_NEW_PUBLIC_SDK
using System;
using VRC.SDK3A.Editor;
#endif

namespace VF.Hooks {
    /**
     * This adds a hook before the VRCSDK creates its clone of the GameObject.
     * This allows us to do certain behaviours on the original object before all
     * prefab connections are lost.
     */
    internal static class PrefabFixHook {

#if VRC_NEW_PUBLIC_SDK
        [InitializeOnLoadMethod]
        private static void Init() {
            VRCSdkControlPanel.OnSdkPanelEnable += (sender, e) => {
                if (VRCSdkControlPanel.TryGetBuilder<IVRCSdkAvatarBuilderApi>(out var builder)) {
                    builder.OnSdkBuildStart += (sender2, target) => {
                        if (target is GameObject targetObj) {
                            PreInstantiate(targetObj);
                        }
                    };
                }
            };
        }
#else


        private abstract class Reflection : ReflectionHelper {
            public static readonly Type SdkBuilder = ReflectionUtils.GetTypeFromAnyAssembly("VRC.SDKBase.Editor.VRC_SdkBuilder");
            public static readonly FieldInfo RunExportAndTestAvatarBlueprint = SdkBuilder?.VFStaticField("RunExportAndTestAvatarBlueprint");
            public static readonly FieldInfo RunExportAndUploadAvatarBlueprint = SdkBuilder?.VFStaticField("RunExportAndUploadAvatarBlueprint");
        }

        [InitializeOnLoadMethod]
        private static void Init() {
            if (!ReflectionHelper.IsReady<Reflection>()) return;
            try {
                PatchPreuploadMethod(Reflection.RunExportAndTestAvatarBlueprint);
                PatchPreuploadMethod(Reflection.RunExportAndUploadAvatarBlueprint);
            } catch (Exception e) {
                Debug.LogError(new Exception("VRCFury prefab fix patch failed", e));
            }
        }

        private static void PatchPreuploadMethod(FieldInfo runField) {
            void Fix(GameObject obj) => PreInstantiate(obj);
            var runObj = runField.GetValue(null);
            if (runObj is Action<GameObject> run1) {
                runField.SetValue(null, Fix + run1);
            } else if (runObj is Func<GameObject, bool> run2) {
                runField.SetValue(null, (Func<GameObject, bool>)(obj => {
                    Fix(obj);
                    return run2(obj);
                }));
            } else {
                throw new Exception("Invalid");
            }
        }
#endif

        private static void PreInstantiate(GameObject obj) {
            VRCFPrefabFixer.Fix(new VFGameObject[] { obj });
        }
        
        [InitializeOnLoadMethod]
        private static void InitPlayMode() {
            EditorApplication.playModeStateChanged += state => {
                if (state == PlayModeStateChange.ExitingEditMode) {
                    if (PlayModeMenuItem.Get()) {
                        var rootObjects = VFGameObject.GetRoots();
                        VRCFPrefabFixer.Fix(rootObjects);
                    }
                }
            };
        }
    }
}
