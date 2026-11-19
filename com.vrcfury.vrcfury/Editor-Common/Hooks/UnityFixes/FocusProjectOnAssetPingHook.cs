using System;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using VF.Utils;
#if UNITY_6000_3_OR_NEWER
using PingId = UnityEngine.EntityId;
#else
using PingId = System.Int32;
#endif

namespace VF.Hooks.UnityFixes {
    /**
     * Reveals a hidden Project tab on asset pings, except when clicking a Console entry.
     */
    internal static class FocusProjectOnAssetPingHook {
        private abstract class Reflection : ReflectionHelper {
            public static readonly Type ProjectBrowser = typeof(Editor).Assembly.GetType("UnityEditor.ProjectBrowser");
            public static readonly Type ConsoleWindow = typeof(Editor).Assembly.GetType("UnityEditor.ConsoleWindow");
            public static readonly MethodInfo IsSelectedTab = typeof(EditorWindow).VFMethod("IsSelectedTab");
            public static readonly HarmonyUtils.PatchObj PingPatch = HarmonyUtils.Patch(
                typeof(FocusProjectOnAssetPingHook),
                nameof(BeforePing),
                typeof(EditorGUIUtility),
                nameof(EditorGUIUtility.PingObject)
            );
        }

        [VFInit]
        private static void Init() {
            if (ReflectionHelper.IsReady<Reflection>()) Reflection.PingPatch.apply();
        }

        // Unity routes the object overload and native pings through the ID overload.
        private static bool BeforePing(PingId __0) {
            if (!AssetDatabase.Contains(__0)) return true;
            var browsers = Resources.FindObjectsOfTypeAll(Reflection.ProjectBrowser).OfType<EditorWindow>();
            var isVisible = browsers.Any(window =>
                window.hasFocus || (window.docked && (bool)Reflection.IsSelectedTab.Invoke(window, null)));
            if (isVisible) return true;
            // Some Unity versions ping while updating the selected Console row, after the click was consumed.
            if (new StackTrace().GetFrames()?.Any(frame => frame.GetMethod()?.DeclaringType == Reflection.ConsoleWindow) == true) return true;
            // Changing tabs inside an IMGUI object field can clear its BeginProperty stack before EndProperty.
            if (Event.current != null) {
                EditorApplication.delayCall += () => EditorGUIUtility.PingObject(__0);
                return false;
            }
            EditorUtility.FocusProjectWindow();
            return true;
        }
    }
}
