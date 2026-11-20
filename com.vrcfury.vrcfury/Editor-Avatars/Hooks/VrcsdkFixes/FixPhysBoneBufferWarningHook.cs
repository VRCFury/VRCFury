#if UNITY_6000_0_OR_NEWER
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using UnityEngine;
using UnityEngine.Jobs;
using VF.Utils;
using VRC.Dynamics;

namespace VF.Hooks.VrcsdkFixes {
    /**
     * Unity 6 warns for each Add(null) while the SDK grows its PhysBone transform array.
     * AddChains overwrites every reserved slot, including its two extra transforms, before
     * scheduling jobs. Borrow the manager's transform only for those initial appends.
     * https://feedback.vrchat.com/sdk-bug-reports/p/sdk-3110-beta1-physbones-flood-play-mode-console-with-adding-null-transform-to-t
     */
    internal static class FixPhysBoneBufferWarningHook {
        [ReflectionHelperOptional]
        private abstract class Reflection : ReflectionHelper {
            public static readonly MethodInfo Add = typeof(TransformAccessArray).GetMethod(nameof(TransformAccessArray.Add), new[] { typeof(Transform) });
            public static readonly MethodInfo Placeholder = typeof(FixPhysBoneBufferWarningHook).VFStaticMethod(nameof(GetPlaceholder));
            public static readonly HarmonyUtils.PatchObj Patch = HarmonyUtils.Patch(
                typeof(FixPhysBoneBufferWarningHook),
                nameof(Transpiler),
                "VRC.Dynamics.BoneBuffer",
                "set_Length",
                HarmonyUtils.PatchMode.Transpiler
            );
        }

        [VFInit]
        private static void Init() {
            if (ReflectionHelper.IsReady<Reflection>()) Reflection.Patch.apply();
        }

        private static Transform GetPlaceholder() {
            var manager = PhysBoneManager.Inst;
            return manager != null ? manager.transform : null;
        }

        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions) {
            var code = instructions.ToArray();
            for (var i = 0; i + 1 < code.Length; i++) {
                if (code[i].opcode != OpCodes.Ldnull || !code[i + 1].Calls(Reflection.Add)) continue;
                code[i].opcode = OpCodes.Call;
                code[i].operand = Reflection.Placeholder;
            }
            return code;
        }
    }
}
#endif
