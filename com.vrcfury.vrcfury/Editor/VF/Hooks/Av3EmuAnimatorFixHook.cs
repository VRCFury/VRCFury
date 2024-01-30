using System.Reflection;
using UnityEditor;
using UnityEngine;
using VF.Builder;
using VRC.SDKBase.Editor.BuildPipeline;

namespace VF.Hooks {
    /**
     * Av3emu blows up if the animator is removed or changed during a preprocessing hook. This fixes that.
     */
    internal class Av3EmuAnimatorFixHook : IVRCSDKPreprocessAvatarCallback {
        public int callbackOrder => int.MaxValue;
        public bool OnPreprocessAvatar(GameObject obj) {
            CheckAvatar(obj);
            return true;
        }

        private static void CheckAvatar(VFGameObject obj) {
            CheckAvatar(obj, "Lyuma.Av3Emulator.Runtime.LyumaAv3Runtime");
            CheckAvatar(obj, "LyumaAv3Runtime");
        }
        
        private static void CheckAvatar(VFGameObject obj, string className) {
            var classType = ReflectionUtils.GetTypeFromAnyAssembly(className);
            if (classType == null) return;
            var resetAnimator = classType.GetMethod("InitializeAnimator",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (resetAnimator == null) return;
            var emuRuntime = obj.GetComponent(classType);
            if (emuRuntime == null) return;
            Debug.Log($"VRCFury is reloading the animator in av3emu for {obj.name} ...");
            resetAnimator.Invoke(emuRuntime, new object[]{});
        }
    }
}
