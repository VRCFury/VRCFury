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

namespace VF.Hooks.VrcsdkFixes {
    /**
     * The SDK's contact scanner skips the EditorOnly filter used by the other component scanners.
     * Apply it before counting contacts, and exclude unsaved emulator senders in Play Mode.
     *
     * https://feedback.vrchat.com/sdk-bug-reports/p/sdk-3110-beta1-performance-rank-improperly-includes-editoronly-dontsaveinbuild-c
     */
    internal static class FixContactPerformanceStatsHook {
        private abstract class PerformanceScanner : ReflectionHelper {
            public static readonly MethodInfo Scan = typeof(ContactPerformanceScanner)
                .VFMethod(nameof(ContactPerformanceScanner.RunPerformanceScanEnumerator))
                ?.GetCustomAttribute<IteratorStateMachineAttribute>()?.StateMachineType.VFMethod("MoveNext");
            public static readonly MethodInfo CountContacts = typeof(List<ContactBase>).GetProperty(nameof(List<ContactBase>.Count))?.GetGetMethod();
            public static readonly MethodInfo CountComponents = typeof(List<UnityEngine.Component>)
                .GetProperty(nameof(List<UnityEngine.Component>.Count))?.GetGetMethod();
            public static readonly MethodInfo FilterAndCount = typeof(FixContactPerformanceStatsHook).VFStaticMethod(nameof(FilterAndCountContacts));
            public static readonly Func<UnityEngine.Component, bool> IsEditorOnly =
                ReflectionUtils.GetTypeFromAnyAssembly("VRC.SDKBase.Validation.ValidationUtils")
                    ?.GetMatchingDelegate<Func<UnityEngine.Component, bool>>("IsEditorOnly");
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
                if (instruction.Calls(PerformanceScanner.CountContacts) || instruction.Calls(PerformanceScanner.CountComponents)) {
                    // Older SDKs count senders and receivers separately in a Component list.
                    instruction.opcode = OpCodes.Call;
                    var listType = ((MethodInfo)instruction.operand).DeclaringType;
                    instruction.operand = PerformanceScanner.FilterAndCount.MakeGenericMethod(listType.GetGenericArguments());
                }
                yield return instruction;
            }
        }

        private static int FilterAndCountContacts<T>(List<T> contacts) where T : UnityEngine.Component {
            var playing = Application.isPlaying;
            contacts.RemoveAll(contact => contact == null
                || PerformanceScanner.IsEditorOnly(contact)
                || (playing && contact is VRCContactSender && (contact.hideFlags & HideFlags.DontSaveInBuild) != 0));
            return contacts.Count;
        }
    }
}
