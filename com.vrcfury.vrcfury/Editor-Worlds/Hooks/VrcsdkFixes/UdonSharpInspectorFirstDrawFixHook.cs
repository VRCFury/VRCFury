using System.Collections.Generic;
using UdonSharp;
using UdonSharpEditor;
using UnityEditor;
using UnityEngine;
using VF.Utils;

namespace VF.Hooks.VrcsdkFixes {
    /**
     * U# mutates backing UdonBehaviour hideFlags during inspector OnEnable.
     * That can cause the first inspector draw to clip the next component's UI.
     * Detect this mutation and force an inspector rebuild on the next editor tick.
     */
    internal static class UdonSharpInspectorFirstDrawFixHook {
        private abstract class Reflection : ReflectionHelper {
            public static readonly HarmonyUtils.PatchObj PatchOnEnablePrefix = HarmonyUtils.Patch(
                typeof(UdonSharpInspectorFirstDrawFixHook),
                nameof(OnEnablePrefix),
                "UdonSharpEditor.UdonSharpBehaviourOverrideEditor",
                "OnEnable"
            );
            public static readonly HarmonyUtils.PatchObj PatchOnEnablePostfix = HarmonyUtils.Patch(
                typeof(UdonSharpInspectorFirstDrawFixHook),
                nameof(OnEnablePostfix),
                "UdonSharpEditor.UdonSharpBehaviourOverrideEditor",
                "OnEnable",
                HarmonyUtils.PatchMode.Postfix
            );
        }

        private static readonly Dictionary<int, HideFlags> hideFlagsByBackingId = new Dictionary<int, HideFlags>();

        [VFInit]
        private static void Init() {
            if (!ReflectionHelper.IsReady<Reflection>()) return;
            Reflection.PatchOnEnablePrefix.apply();
            Reflection.PatchOnEnablePostfix.apply();
        }

        private static void OnEnablePrefix(Editor __instance) {
            if (__instance == null) return;
            hideFlagsByBackingId.Clear();
            foreach (var target in __instance.targets) {
                if (!(target is UdonSharpBehaviour proxy) || proxy == null) continue;
                var backing = UdonSharpEditorUtility.GetBackingUdonBehaviour(proxy);
                if (backing == null) continue;
                hideFlagsByBackingId[backing.GetInstanceID()] = backing.hideFlags;
            }
        }

        private static void OnEnablePostfix(Editor __instance) {
            if (__instance == null) return;
            if (hideFlagsByBackingId.Count == 0) return;

            foreach (var target in __instance.targets) {
                if (!(target is UdonSharpBehaviour proxy) || proxy == null) continue;
                var backing = UdonSharpEditorUtility.GetBackingUdonBehaviour(proxy);
                if (backing == null) continue;
                if (!hideFlagsByBackingId.TryGetValue(backing.GetInstanceID(), out var beforeHideFlags)) continue;
                var afterHideFlags = backing.hideFlags;
                if (beforeHideFlags != afterHideFlags) {
                    backing.hideFlags = beforeHideFlags;
                    EditorApplication.delayCall += () => {
                        backing.hideFlags = afterHideFlags;
                    };
                }
            }

            hideFlagsByBackingId.Clear();
        }
    }
}
