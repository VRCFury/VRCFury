using System;
using System.Reflection;
using UnityEditor;
using VF.Utils;

namespace VF.Hooks.VrcsdkFixes {
    /**
     * https://feedback.vrchat.com/sdk-bug-reports/p/376-dynamics-only-work-the-first-time-you-enter-play-mode
     */
    internal static class FixVrcDynamicsNotWorking {

        private abstract class Reflection : ReflectionHelper {
            public static readonly Type VRCAvatarDynamicsScheduler = ReflectionUtils.GetTypeFromAnyAssembly("VRC.Dynamics.VRCAvatarDynamicsScheduler");
            public static readonly FieldInfo latestCompletedFrameNumber = VRCAvatarDynamicsScheduler?
                .VFStaticField("_latestCompletedFrameNumber");
        }

        [InitializeOnLoadMethod]
        public static void Init() {
            if (!ReflectionHelper.IsReady<Reflection>()) return;
            EditorApplication.playModeStateChanged += state => {
                if (state != PlayModeStateChange.ExitingEditMode) return;
                Reflection.latestCompletedFrameNumber.SetValue(null, -1);
            };
        }
    }
}
