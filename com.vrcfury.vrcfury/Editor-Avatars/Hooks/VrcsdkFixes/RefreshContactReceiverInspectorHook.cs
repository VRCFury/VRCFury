using UnityEditor;
using UnityEngine;
using VF.Utils;
using VRC.SDK3.Dynamics.Contact.Components;

namespace VF.Hooks {
    /**
     * The vrcsdk contact receiver inspector doesn't update the debug fields live while in play mode,
     * only when you happen to hover over it to trigger a repaint. This fixed it.
     */
    internal static class RefreshContactReceiverInspectorHook {
        [ReflectionHelperOptional]
        private abstract class Reflection : ReflectionHelper {
            public static readonly HarmonyUtils.PatchObj Patch = HarmonyUtils.Patch(
                typeof(RefreshContactReceiverInspectorHook), nameof(Postfix),
                "VRC.SDK3.Dynamics.Contact.VRCContactBaseEditor", "OnInspectorGUI",
                HarmonyUtils.PatchMode.Postfix);
        }

        [VFInit]
        private static void Init() {
            if (ReflectionHelper.IsReady<Reflection>()) Reflection.Patch.apply();
        }

        public static void Postfix(Editor __instance) {
            if (Application.isPlaying && __instance.target is VRCContactReceiver) __instance.Repaint();
        }
    }
}
