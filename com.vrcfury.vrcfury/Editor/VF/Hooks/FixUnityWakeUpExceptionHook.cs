using System;
using System.Reflection;
using UnityEditor;
using UnityEditor.Graphs;
using VF.Utils;

namespace VF.Hooks {
    /**
     * Fixes internal unity error when reloading scripts when an empty animator has been opened recently
     * https://discussions.unity.com/t/what-is-this-big-error-im-getting/579827
     */
    internal static class FixUnityWakeUpExceptionHook {
        private static FieldInfo m_FromNode =
            typeof(Edge).GetField("m_FromNode", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

        private static PropertyInfo m_Graph = m_FromNode?.FieldType
            .GetProperty("graph", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

        [InitializeOnLoadMethod]
        private static void Init() {
            if (m_FromNode == null || m_Graph == null) return;
            var original = typeof(Edge).GetMethod("WakeUp", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance, null, new Type[] {}, null);
            var prefix = typeof(FixUnityWakeUpExceptionHook).GetMethod(nameof(Prefix), BindingFlags.NonPublic | BindingFlags.Static);
            HarmonyUtils.Patch(original, prefix);
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
