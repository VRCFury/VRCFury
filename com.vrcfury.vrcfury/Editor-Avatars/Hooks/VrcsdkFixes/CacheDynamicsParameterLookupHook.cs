using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using UnityEngine;
using VF.Utils;

namespace VF.Hooks.VrcsdkFixes {
    internal static class CacheDynamicsParameterLookupHook {
        [ReflectionHelperOptional]
        private abstract class Reflection : ReflectionHelper {
            public static readonly MethodInfo Getter = typeof(Animator).GetProperty(nameof(Animator.parameters))?.GetGetMethod();
            public static readonly MethodInfo Snapshot = typeof(CacheDynamicsParameterLookupHook).VFStaticMethod(nameof(GetParameters));
            public static readonly HarmonyUtils.PatchObj Patch = HarmonyUtils.Patch(
                typeof(CacheDynamicsParameterLookupHook),
                nameof(Transpiler),
                "VRC.Dynamics.AnimParameterAccessAvatarSDK",
                HarmonyUtils.CONSTRUCTOR,
                HarmonyUtils.PatchMode.Transpiler
            );
        }

        [VFInit]
        private static void Init() {
            if (!ReflectionHelper.IsReady<Reflection>()) return;
            Reflection.Patch.apply();
        }

        private static AnimatorControllerParameter[] GetParameters(Animator animator, ref AnimatorControllerParameter[] parameters) {
            return parameters ??= animator.parameters;
        }

        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator) {
            var original = instructions.ToArray();
            // Already-fixed SDKs only read the array once. Leave those constructors unchanged.
            if (original.Count(instruction => instruction.Calls(Reflection.Getter)) < 2) return original;
            return Rewrite(original, generator);
        }

        private static IEnumerable<CodeInstruction> Rewrite(IEnumerable<CodeInstruction> instructions, ILGenerator generator) {
            // The SDK indexes animator.parameters inside a loop. Each read copies the entire array.
            var parameters = generator.DeclareLocal(typeof(AnimatorControllerParameter[]));
            foreach (var instruction in instructions) {
                if (instruction.Calls(Reflection.Getter)) {
                    // Keep the original guards and lazily snapshot at the first getter call.
                    yield return new CodeInstruction(OpCodes.Ldloca, parameters).MoveLabelsFrom(instruction);
                    instruction.opcode = OpCodes.Call;
                    instruction.operand = Reflection.Snapshot;
                    yield return instruction;
                } else {
                    yield return instruction;
                }
            }
        }
    }
}
