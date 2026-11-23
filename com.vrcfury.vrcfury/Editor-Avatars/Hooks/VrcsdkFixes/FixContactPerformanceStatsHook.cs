using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using HarmonyLib;
using UnityEngine;
using VF.Utils;
using VRC.Dynamics;
using VRC.SDK3.Dynamics.Contact;
using VRC.SDK3.Dynamics.Contact.Components;
using VRC.SDKBase.Validation;

namespace VF.Hooks.VrcsdkFixes {
    /**
     * The SDK's contact scanner skips the EditorOnly filter used by the other component scanners.
     * Apply it before counting contacts, and exclude unsaved emulator senders in Play Mode.
     */
    internal static class FixContactPerformanceStatsHook {
        private abstract class PerformanceScanner : ReflectionHelper {
            public static readonly MethodInfo Scan = typeof(ContactPerformanceScanner)
                .VFMethod(nameof(ContactPerformanceScanner.RunPerformanceScanEnumerator))
                ?.GetCustomAttribute<IteratorStateMachineAttribute>()?.StateMachineType.VFMethod("MoveNext");
            public static readonly MethodInfo Count = typeof(List<ContactBase>).GetProperty(nameof(List<ContactBase>.Count))?.GetGetMethod();
            public static readonly MethodInfo FilterAndCount = typeof(FixContactPerformanceStatsHook).VFStaticMethod(nameof(FilterAndCountContacts));
            public static readonly Func<UnityEngine.Component, bool> IsEditorOnly =
                typeof(ValidationUtils).GetMatchingDelegate<Func<UnityEngine.Component, bool>>("IsEditorOnly");
            public static readonly HarmonyUtils.PatchObj Patch = HarmonyUtils.Patch(
                Scan,
                (typeof(FixContactPerformanceStatsHook), nameof(FilterContactBuffer)),
                HarmonyUtils.PatchMode.Transpiler
            );
        }

        [VFInit]
        private static void Init() {
            if (!ReflectionHelper.IsReady<PerformanceScanner>()) return;
            PerformanceScanner.Patch.apply();
        }

        private static IEnumerable<CodeInstruction> FilterContactBuffer(IEnumerable<CodeInstruction> instructions) {
            foreach (var instruction in instructions) {
                if (instruction.Calls(PerformanceScanner.Count)) {
                    // The SDK saves the total, then reuses this list to count non-local contacts for the rank.
                    instruction.opcode = OpCodes.Call;
                    instruction.operand = PerformanceScanner.FilterAndCount;
                }
                yield return instruction;
            }
        }

        private static int FilterAndCountContacts(List<ContactBase> contacts) {
            var playing = Application.isPlaying;
            contacts.RemoveAll(contact => contact == null
                || PerformanceScanner.IsEditorOnly(contact)
                || (playing && contact is VRCContactSender && (contact.hideFlags & HideFlags.DontSaveInBuild) != 0));
            return contacts.Count;
        }
    }
}
