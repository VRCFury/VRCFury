using System;
using System.Reflection;
using UnityEditor;
using UnityEditor.Graphs;
using VF.Utils;

namespace VF.Hooks.UnityFixes {
    /**
     * Fixes internal unity error when reloading scripts when an empty animator has been opened recently
     * https://discussions.unity.com/t/what-is-this-big-error-im-getting/579827
     */
    internal static class FixUnityWakeUpExceptionHook {
        private static readonly FieldInfo m_FromNode =
            typeof(Edge).GetField("m_FromNode", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

        private static readonly PropertyInfo m_Graph = m_FromNode?.FieldType
            .GetProperty("graph", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

        [InitializeOnLoadMethod]
        private static void Init() {
            if (m_FromNode == null || m_Graph == null) return;
            HarmonyUtils.Patch(
                typeof(FixUnityWakeUpExceptionHook),
                nameof(Prefix),
                typeof(Edge),
                "WakeUp"
            );
        }

        private static bool Prefix(Edge __instance, ref bool __result) {
            try {
                var fromNode = m_FromNode.GetValue(__instance);
                if (fromNode == null) {
                    __result = false;
                    return false;
                }

                var g = m_Graph.GetValue(fromNode);
                if (g == null) {
                    __result = false;
                    return false;
                }
            } catch (Exception) {
                /**/
            }

            return true;
        }
    }
}
