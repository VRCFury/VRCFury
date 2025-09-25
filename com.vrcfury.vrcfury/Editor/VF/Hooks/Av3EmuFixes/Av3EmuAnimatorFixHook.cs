using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using VF.Builder;
using VF.Utils;
using VRC.Dynamics;
using VRC.SDK3.Avatars.Components;
using VRC.SDKBase;
using VRC.SDKBase.Editor.BuildPipeline;
using Debug = UnityEngine.Debug;
using Object = UnityEngine.Object;

namespace VF.Hooks.Av3EmuFixes {
    /**
     * If preprocessor hooks are run after av3emu has initialized, it blows up because the animator has changed
     * and transforms its expecting to exist may no longer exist. To solve this, we totally blow away av3emu and tell it
     * to restart after an avatar is built in play mode.
     */
    internal class Av3EmuAnimatorFixHook : VrcfAvatarPreprocessor {
        protected override int order => int.MaxValue;
        private static bool restartPending = false;
        protected override void Process(VFGameObject obj) {
            if (Application.isPlaying && !restartPending) {
                restartPending = true;
                EditorApplication.delayCall += () => {
                    restartPending = false;
                    if (Application.isPlaying) {
                        RestartAv3Emulator();
                    }
                };
            }
        }
        
        private static void DestroyAllOfType(string typeStr) {
            var type = ReflectionUtils.GetTypeFromAnyAssembly(typeStr);
            if (type == null) return;
            foreach (var runtime in ObjectExtensions.FindObjectsByType(type)) {
                Object.DestroyImmediate(runtime);
            }
        }

        private static void ClearField(object obj, string fieldStr) {
            var field = obj.GetType().GetField(fieldStr);
            if (field == null) return;
            var value = field.GetValue(obj);
            if (value == null) return;
            var clear = value.GetType().GetMethod("Clear");
            if (clear == null) return;
            clear.Invoke(value, new object[]{});
        }

        private static void RestartAv3Emulator() {
            try {
                var av3EmulatorType = ReflectionUtils.GetTypeFromAnyAssembly("Lyuma.Av3Emulator.Runtime.LyumaAv3Emulator");
                if (av3EmulatorType == null) av3EmulatorType = ReflectionUtils.GetTypeFromAnyAssembly("LyumaAv3Emulator");
                if (av3EmulatorType == null) return;
                
                Debug.Log("VRCFury is forcing av3emu to reload");
                
                var runHooksField =
                    av3EmulatorType.GetField("RunPreprocessAvatarHook", BindingFlags.Instance | BindingFlags.Public);
                if (runHooksField == null) throw new Exception("Failed to find RunPreprocessAvatarHook field");
                
                DestroyAllOfType("Lyuma.Av3Emulator.Runtime.LyumaAv3Runtime");
                DestroyAllOfType("LyumaAv3Runtime");
                DestroyAllOfType("Lyuma.Av3Emulator.Runtime.LyumaAv3Menu");
                DestroyAllOfType("Lyuma.Av3Emulator.Runtime.GestureManagerAv3Menu");

                var restartField = av3EmulatorType.GetField("RestartEmulator");
                if (restartField == null) throw new Exception("Failed to find RestartEmulator field");
                var emulators = ObjectExtensions.FindObjectsByType(av3EmulatorType);
                foreach (var emulator in emulators) {
                    ClearField(emulator, "runtimes");
                    ClearField(emulator, "forceActiveRuntimes");
                    ClearField(emulator, "scannedAvatars");
                    // Tell av3emu to not run hooks so we don't loop forever
                    runHooksField.SetValue(emulator, false);
                    restartField.SetValue(emulator, true);
                }

                if (emulators.Length >= 1) {
                    var avatars = ObjectExtensions.FindObjectsByType<VRCAvatarDescriptor>();
                    foreach (var avatar in avatars) {
                        foreach (var component in avatar.GetComponentsInChildren<IParameterSetup>(true)) {
                            foreach (var fieldInfo in component.GetType().GetFields()) {
                                if (fieldInfo.FieldType == typeof(IAnimParameterAccess)) {
                                    fieldInfo.SetValue(component, null);
                                }
                            }
                        }
                    }
                }
                
                foreach (var root in VFGameObject.GetRoots()) {
                    if (PlayModeTrigger.IsAv3EmulatorClone(root)) {
                        root.Destroy();
                    }
                }
            } catch (Exception e) {
                Debug.LogException(e);
                DialogUtils.DisplayDialog(
                    "VRCFury",
                    "VRCFury detected Av3Emulator, but was unable to reload it after making changes to the avatar." +
                    " Because of this, testing with the emulator may not be correct." +
                    " Report this on https://vrcfury.com/discord\n\n" + e.Message,
                    "Ok"
                );
            }
        }
    }
}
