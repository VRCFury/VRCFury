#if !GESTURE_MANAGER_USES_SDK_PLAYER_IDS
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using VF.Utils;

namespace VF.Hooks.GestureManagerFixes {
    /**
     * Stops older Gesture Manager builds from overriding SDK-assigned player IDs on only some dynamics components.
     * https://github.com/BlackStartx/VRC-Gesture-Manager/releases/tag/v3.10.0
     */
    internal static class DisableGestureManagerPlayerIdHook {
        [ReflectionHelperOptional]
        private abstract class Reflection : ReflectionHelper {
            private const string ModuleVrc3 = "BlackStartX.GestureManager.Editor.Modules.Vrc3.ModuleVrc3";

            public static readonly HarmonyUtils.PatchObj ReceiverBaseSetup = HarmonyUtils.Patch(
                typeof(DisableGestureManagerPlayerIdHook),
                nameof(Transpiler),
                ModuleVrc3,
                "ReceiverBaseSetup",
                HarmonyUtils.PatchMode.Transpiler
            );
            public static readonly HarmonyUtils.PatchObj SenderBaseSetup = HarmonyUtils.Patch(
                typeof(DisableGestureManagerPlayerIdHook),
                nameof(Transpiler),
                ModuleVrc3,
                "SenderBaseSetup",
                HarmonyUtils.PatchMode.Transpiler
            );
            public static readonly HarmonyUtils.PatchObj PhysBoneBaseSetup = HarmonyUtils.Patch(
                typeof(DisableGestureManagerPlayerIdHook),
                nameof(Transpiler),
                ModuleVrc3,
                "PhysBoneBaseSetup",
                HarmonyUtils.PatchMode.Transpiler
            );
        }

        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions) {
            foreach (var instruction in instructions) {
                if (instruction.opcode == OpCodes.Stfld
                    && instruction.operand is FieldInfo field
                    && field.Name == "playerId") {
                    // stfld consumes the target object and value. Consume both without performing the assignment.
                    instruction.opcode = OpCodes.Pop;
                    instruction.operand = null;
                    yield return instruction;
                    yield return new CodeInstruction(OpCodes.Pop);
                    continue;
                }
                yield return instruction;
            }
        }

        [VFInit]
        private static void Init() {
            if (!ReflectionHelper.IsReady<Reflection>()) return;
            Reflection.ReceiverBaseSetup.apply();
            Reflection.SenderBaseSetup.apply();
            Reflection.PhysBoneBaseSetup.apply();
        }
    }
}
#endif
