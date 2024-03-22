using System;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using VF.Builder;
using VRC.SDKBase.Editor.BuildPipeline;
using Debug = UnityEngine.Debug;
using Object = UnityEngine.Object;

namespace VF.Hooks {
    /**
     * If preprocessor hooks are run after av3emu has initialized, it blows up because the animator has changed
     * and transforms its expecting to exist may no longer exist. To solve this, we totally blow away av3emu and tell it
     * to restart after an avatar is built in play mode.
     */
    internal class Av3EmuAnimatorFixHook : IVRCSDKPreprocessAvatarCallback {
        public int callbackOrder => int.MaxValue;
        public bool OnPreprocessAvatar(GameObject obj) {
            if (Application.isPlaying) {
                EditorApplication.delayCall += () => {
                    if (Application.isPlaying) {
                        RestartAv3Emulator();
                    }
                };
            }
            return true;
        }
        
        private static void DestroyAllOfType(string typeStr) {
            var type = ReflectionUtils.GetTypeFromAnyAssembly(typeStr);
            if (type == null) return;
            foreach (var runtime in Object.FindObjectsOfType(type)) {
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
            return;
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
                var emulators = Object.FindObjectsOfType(av3EmulatorType);
                foreach (var emulator in emulators) {
                    ClearField(emulator, "runtimes");
                    ClearField(emulator, "forceActiveRuntimes");
                    ClearField(emulator, "scannedAvatars");
                    // Tell av3emu to not run hooks so we don't loop forever
                    runHooksField.SetValue(emulator, false);
                    restartField.SetValue(emulator, true);
                }

                foreach (var root in VFGameObject.GetRoots()) {
                    if (PlayModeTrigger.IsAv3EmulatorClone(root)) {
                        root.Destroy();
                    }
                }
            } catch (Exception e) {
                Debug.LogException(e);
                EditorUtility.DisplayDialog(
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
