using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using VF.Utils;

namespace VF.Hooks.GestureManagerFixes {
    /** Gesture Manager assigns its own player ID to avatar dynamics components (because the VRCSDK used to not do it itself)
     * This breaks things, since it DOESNT set playerid on some newer vrcsdk components like global colliders.
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
