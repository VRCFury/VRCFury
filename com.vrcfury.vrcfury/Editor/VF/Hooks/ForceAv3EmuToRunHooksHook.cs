using System.Linq;
using System.Reflection;
using UnityEditor;
using VF.Builder;
using VF.Menu;

namespace VF.Hooks {
    /**
     * If vrcfury in play mode is enabled, we turn on "run hooks" in av3emu as well, so if present, it will call us
     * when it loads the avatar, meaning that we won't have to figure out how to force av3emu to reload itself later.
     */
    public static class ForceAv3EmuToRunHooksHook {
        [InitializeOnLoadMethod]
        static void Init() {
            EditorApplication.playModeStateChanged += (newState) => {
                if (newState == PlayModeStateChange.ExitingEditMode) {
                    CheckAv3Emu("LyumaAv3Emulator");
                    CheckAv3Emu("Lyuma.Av3Emulator.Runtime.LyumaAv3Emulator");
                }
            };
        }
        
        private static void CheckAv3Emu(string className) {
            if (!PlayModeMenuItem.Get()) return;

            var classType = ReflectionUtils.GetTypeFromAnyAssembly(className);
            if (classType == null) return;
            var runHooksField =
                classType.GetField("RunPreprocessAvatarHook", BindingFlags.Default | BindingFlags.Public);
            if (runHooksField == null) return;
            var components = VFGameObject.GetRoots().SelectMany(
                root => root.GetComponentsInSelfAndChildren(classType));
            foreach (var c in components) {
                runHooksField.SetValue(c, true);
            }
        }
    }
}
