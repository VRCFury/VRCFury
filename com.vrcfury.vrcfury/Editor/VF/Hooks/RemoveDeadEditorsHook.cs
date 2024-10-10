﻿using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using VF.Utils;

namespace VF.Hooks {
    /**
     * VRC_AnimatorPlayAudioEditor reads props in OnEnable instead of OnInspectorGui (don't do this).
     * A unity bug causes behaviour editors to remain forever rather than getting destroyed in some cases.
     * This means if you delete the AnimatorPlayAudio, the editor will fail to find the props when OnEnable is next called (usually on script reload),
     * because the target doesn't exist so the serializedObject can't be created.
     */
    internal static class RemoveDeadEditorsHook {
        [InitializeOnLoadMethod]
        private static void Init() {
            var original = ReflectionUtils.GetTypeFromAnyAssembly("VRC_AnimatorPlayAudioEditor")?.GetMethod(
                "OnEnable",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                null,
                new Type[] {},
                null
            );

            var prefix = typeof(RemoveDeadEditorsHook).GetMethod(nameof(Prefix),
                BindingFlags.Static | BindingFlags.NonPublic);

            HarmonyUtils.Patch(original, prefix);
        }

        private static bool Prefix(Editor __instance) {
            if (__instance.target == null) return false;
            return true;
        }
    }
}
