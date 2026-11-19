using System;
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
     * Brings the Project window forward when an asset is pinged and its Project tab is hidden.
     */
    internal static class FocusProjectOnAssetPingHook {
        private abstract class Reflection : ReflectionHelper {
            public static readonly Type ProjectBrowser = typeof(Editor).Assembly.GetType("UnityEditor.ProjectBrowser");
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
        private static void BeforePing(PingId __0) {
            if (!AssetDatabase.Contains(__0)) return;
            var browsers = Resources.FindObjectsOfTypeAll(Reflection.ProjectBrowser).OfType<EditorWindow>();
            var isVisible = browsers.Any(window =>
                window.hasFocus || (window.docked && (bool)Reflection.IsSelectedTab.Invoke(window, null)));
            if (!isVisible) EditorUtility.FocusProjectWindow();
        }
    }
}
