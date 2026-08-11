using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEditor;
using VF.Utils;
using VRC.Editor;

namespace VF.Hooks.VrcsdkFixes {
    /**
     * SetAudioSettings in the VRCSDK runs on every domain reload, and calls SaveAssets for no reason
     */
    internal static class FixVrcsdkSavingEverythingOnDomainReloadHook {
        private abstract class Reflection : ReflectionHelper {
            public static readonly MethodInfo SaveAssets = typeof(AssetDatabase).VFStaticMethod(nameof(AssetDatabase.SaveAssets), new Type[] { });
            public static readonly HarmonyUtils.PatchObj PatchRunBehaviourSetupPrefix = HarmonyUtils.Patch(
                typeof(FixVrcsdkSavingEverythingOnDomainReloadHook),
                nameof(Transpiler),
                typeof(EnvConfig),
                "SetAudioSettings",
                HarmonyUtils.PatchMode.Transpiler
            );
        }

        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions) {
            foreach (var instruction in instructions) {
                if (instruction.Calls(Reflection.SaveAssets)) {
                    continue;
                }
                yield return instruction;
            }
        }

        [VFInit]
        private static void Init() {
            if (!ReflectionHelper.IsReady<Reflection>()) return;
            Reflection.PatchRunBehaviourSetupPrefix.apply();
        }
    }
}
