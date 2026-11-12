using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using HarmonyLib;
using UnityEngine;
using VF.Utils;

namespace VF.Hooks.VrcsdkFixes {
    internal static class FixMaterialContactPlayerIdsHook {
        [ReflectionHelperOptional]
        private abstract class Reflection : ReflectionHelper {
            private const string Setup = "VRC.SDK3.Avatars.AvatarDynamicsSetup";

            public static readonly MethodInfo PlayerId = typeof(FixMaterialContactPlayerIdsHook).VFStaticMethod(nameof(GetPlayerId));
            public static readonly HarmonyUtils.PatchObj Contacts = HarmonyUtils.Patch(
                typeof(FixMaterialContactPlayerIdsHook),
                nameof(Transpiler),
                Setup,
                "Contact_OnInitialize",
                HarmonyUtils.PatchMode.Transpiler
            );
        }

        private static int nextPlayerId;
        private class PlayerId {
            public readonly int value = ++nextPlayerId;
        }
        private static readonly ConditionalWeakTable<Object, PlayerId> playerIds = new();

        private static int GetPlayerId(Object avatar) => playerIds.GetOrCreateValue(avatar).value;

        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions) {
            foreach (var instruction in instructions) {
                if ((instruction.opcode == OpCodes.Callvirt || instruction.opcode == OpCodes.Call)
                    && instruction.operand is MethodInfo method
                    && method.Name == nameof(Object.GetHashCode)
                    && (method.DeclaringType == typeof(Object) || method.DeclaringType == typeof(object))) {
                    // Material contacts clamp negative IDs to zero. Use unique positive IDs
                    // shared by contacts on the same avatar, reserving zero for detached contacts.
                    instruction.opcode = OpCodes.Call;
                    instruction.operand = Reflection.PlayerId;
                }
                yield return instruction;
            }
        }

        [VFInit]
        private static void Init() {
            if (!ReflectionHelper.IsReady<Reflection>()) return;
            Reflection.Contacts.apply();
        }
    }
}
