using System;
using System.Reflection;
using JetBrains.Annotations;
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
        [ReflectionHelperOptional]
        private abstract class Av3EmuReflection : ReflectionHelper {
            public static readonly Type Av3EmulatorType =
                ReflectionUtils.GetTypeFromAnyAssembly("Lyuma.Av3Emulator.Runtime.LyumaAv3Emulator")
                ?? ReflectionUtils.GetTypeFromAnyAssembly("LyumaAv3Emulator");
            public static readonly Type LyumaAv3Runtime =
                ReflectionUtils.GetTypeFromAnyAssembly("Lyuma.Av3Emulator.Runtime.LyumaAv3Runtime") ??
                ReflectionUtils.GetTypeFromAnyAssembly("LyumaAv3Runtime");
            public static readonly FieldInfo RunPreprocessAvatarHook = Av3EmulatorType?.VFField("RunPreprocessAvatarHook");
            public static readonly FieldInfo RestartEmulator = Av3EmulatorType?.VFField("RestartEmulator");
            public static readonly Type LyumaAv3Menu =
                ReflectionUtils.GetTypeFromAnyAssembly("Lyuma.Av3Emulator.Runtime.LyumaAv3Menu");
            public static readonly Type GestureManagerAv3Menu =
                ReflectionUtils.GetTypeFromAnyAssembly("Lyuma.Av3Emulator.Runtime.GestureManagerAv3Menu");
            public static readonly FieldInfo Runtimes = Av3EmulatorType?.VFField("runtimes");
            public static readonly FieldInfo ForceActiveRuntimes = Av3EmulatorType?.VFField("forceActiveRuntimes");
            public static readonly FieldInfo ScannedAvatars = Av3EmulatorType?.VFField("scannedAvatars");
        }

        [CanBeNull]
        public static Type LyumaAv3Runtime => Av3EmuReflection.LyumaAv3Runtime;

        protected override int order => int.MaxValue;
        private static bool restartPending = false;
        protected override void Process(VFGameObject obj) {
            if (!ReflectionHelper.IsReady<Av3EmuReflection>()) return;
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
        
        private static void DestroyAllOfType(Type type) {
            foreach (var runtime in ObjectExtensions.FindObjectsByType(type)) {
                Object.DestroyImmediate(runtime);
            }
        }

        private static void ClearField(object obj, FieldInfo field) {
            if (field == null) return;
            var value = field.GetValue(obj);
            if (value == null) return;
            var clear = value.GetType().VFMethod("Clear");
            if (clear == null) return;
            clear.Invoke(value, new object[]{});
        }

        private static void RestartAv3Emulator() {
            try {
                Debug.Log("VRCFury is forcing av3emu to reload");

                DestroyAllOfType(Av3EmuReflection.LyumaAv3Runtime);
                DestroyAllOfType(Av3EmuReflection.LyumaAv3Menu);
                DestroyAllOfType(Av3EmuReflection.GestureManagerAv3Menu);

                var emulators = ObjectExtensions.FindObjectsByType(Av3EmuReflection.Av3EmulatorType);
                foreach (var emulator in emulators) {
                    ClearField(emulator, Av3EmuReflection.Runtimes);
                    ClearField(emulator, Av3EmuReflection.ForceActiveRuntimes);
                    ClearField(emulator, Av3EmuReflection.ScannedAvatars);
                    // Tell av3emu to not run hooks so we don't loop forever
                    Av3EmuReflection.RunPreprocessAvatarHook.SetValue(emulator, false);
                    Av3EmuReflection.RestartEmulator.SetValue(emulator, true);
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
