using System;
using System.Reflection;
using UnityEditor;
using VF.Utils;

namespace VF.Hooks.VrcsdkFixes {
    /**
     * ClientSimNetworkingUtilities crashes when play mode domain reload is disabled.
     * https://feedback.vrchat.com/sdk-bug-reports/p/clientsim-crashes-in-some-cases-when-play-mode-domain-reloading-is-disabled
     */
    internal static class ClientSimNetworkingUtilitiesResetHook {
        [ReflectionHelperOptional]
        private abstract class Reflection : ReflectionHelper {
            public static readonly Type ClientSimNetworkingUtilities =
                ReflectionUtils.GetTypeFromAnyAssembly("VRC.SDK3.ClientSim.ClientSimNetworkingUtilities");
            public static readonly FieldInfo PlayerObjectList =
                ClientSimNetworkingUtilities?.VFStaticField("_playerObjectList");
        }

        [InitializeOnLoadMethod]
        private static void Init() {
            if (!ReflectionHelper.IsReady<Reflection>()) return;
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange change) {
            if (change != PlayModeStateChange.ExitingEditMode) return;
            Reflection.PlayerObjectList.SetValue(null, null);
        }
    }
}
