using System;
using UnityEditor;
using UnityEngine;
using VF.Utils;

namespace VF.Hooks {
    internal static class WholesomeSpsConfiguratorHook {
        private const float ExtraMinHeight = 30;

        [ReflectionHelperOptional]
        private abstract class Reflection : ReflectionHelper {
            public static readonly Type SpsConfigurator = ReflectionUtils.GetTypeFromAnyAssembly("Wholesome.SPSConfigurator");
            public static readonly HarmonyUtils.PatchObj OnGuiPatch = HarmonyUtils.Patch(
                typeof(WholesomeSpsConfiguratorHook),
                nameof(OnGuiPrefix),
                "Wholesome.SPSConfigurator",
                "OnGUI"
            );
            public static readonly HarmonyUtils.PatchObj OpenPatch = HarmonyUtils.Patch(
                typeof(WholesomeSpsConfiguratorHook),
                nameof(OpenPostfix),
                "Wholesome.SPSConfigurator",
                "Open",
                HarmonyUtils.PatchMode.Postfix
            );
        }

        [VFInit]
        private static void Init() {
            if (!ReflectionHelper.IsReady<Reflection>()) return;
            Reflection.OnGuiPatch.apply();
            Reflection.OpenPatch.apply();
        }

        private static void OnGuiPrefix(EditorWindow __instance) {
            if (__instance == null) return;

            EditorGUILayout.HelpBox(
                "Note: SPS is a feature of VRCFury." +
                " This configurator just adds VRCFury SPS sockets to your avatar." +
                " Wholesome is not affiliated with VRCFury." +
                " For SPS support unrelated to this configurator, visit vrcfury.com/sps",
                MessageType.Warning
            );
        }

        private static void OpenPostfix() {
            var window = EditorWindow.GetWindow(Reflection.SpsConfigurator);
            if (window == null) return;
            window.minSize += new Vector2(2, ExtraMinHeight);
        }
    }
}
