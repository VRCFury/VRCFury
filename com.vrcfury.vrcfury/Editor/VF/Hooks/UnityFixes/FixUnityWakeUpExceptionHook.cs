using System;
using System.Reflection;
using UnityEditor;
using VF.Utils;

namespace VF.Hooks.UnityFixes {
    /**
     * Fixes internal unity error when reloading scripts when an empty animator has been opened recently
     * https://discussions.unity.com/t/what-is-this-big-error-im-getting/579827
     */
    internal static class FixUnityWakeUpExceptionHook {
        private abstract class Reflection : ReflectionHelper {
            public static readonly Type Edge = ReflectionUtils.GetTypeFromAnyAssembly("UnityEditor.Graphs.Edge");
            public static readonly FieldInfo m_FromNode = Edge?.VFField("m_FromNode");
            public static readonly PropertyInfo m_Graph = m_FromNode?.FieldType?.VFProperty("graph");
        }

        [InitializeOnLoadMethod]
        private static void Init() {
            if (!ReflectionHelper.IsReady<Reflection>()) return;
            HarmonyUtils.Patch(
                typeof(FixUnityWakeUpExceptionHook),
                nameof(Prefix),
                Reflection.Edge,
                "WakeUp"
            );
        }

        private static bool Prefix(object __instance, ref bool __result) {
            try {
                var fromNode = Reflection.m_FromNode.GetValue(__instance);
                if (fromNode == null) {
                    __result = false;
                    return false;
                }

                var g = Reflection.m_Graph.GetValue(fromNode);
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

