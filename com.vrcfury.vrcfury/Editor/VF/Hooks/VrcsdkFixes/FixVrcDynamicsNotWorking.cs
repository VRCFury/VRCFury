using System.Reflection;
using UnityEditor;
using VF.Utils;

namespace VF.Hooks.VrcsdkFixes {
    /**
     * https://feedback.vrchat.com/sdk-bug-reports/p/376-dynamics-only-work-the-first-time-you-enter-play-mode
     */
    internal static class FixVrcDynamicsNotWorking {
        [InitializeOnLoadMethod]
        public static void Init() {
            EditorApplication.playModeStateChanged += state => {
                if (state != PlayModeStateChange.ExitingEditMode) return;
                var frameNumberField = ReflectionUtils.GetTypeFromAnyAssembly("VRC.Dynamics.VRCAvatarDynamicsScheduler")?
                    .GetField("_latestCompletedFrameNumber", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                if (frameNumberField == null) return;
                frameNumberField.SetValue(null, -1);
            };
        }
    }
}
