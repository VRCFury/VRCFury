using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using HarmonyLib;
using UnityEditor;
using UnityEngine;
using VF.Utils;

namespace VF.Hooks.VrcsdkFixes {
    /**
     * Allows the VRChat SDK Control Panel to switch tabs and display their contents in Play Mode.
     * The SDK checks Play Mode in both the tab handler and callbacks generated from CreateGUI.
     */
    internal static class AllowSdkControlPanelInPlayModeHook {
        private abstract class Reflection : ReflectionHelper {
            public static readonly MethodInfo EditorIsPlaying = AccessTools.PropertyGetter(
                typeof(EditorApplication),
                nameof(EditorApplication.isPlaying)
            );
            public static readonly MethodInfo ApplicationIsPlaying = AccessTools.PropertyGetter(
                typeof(Application),
                nameof(Application.isPlaying)
            );
            public static readonly HarmonyUtils.PatchObj[] Patches = FindMethods()
                .Select(method => HarmonyUtils.Patch(
                    method,
                    (typeof(AllowSdkControlPanelInPlayModeHook), nameof(Transpile)),
                    HarmonyUtils.PatchMode.Transpiler
                ))
                .ToArray();

            private static IEnumerable<MethodInfo> FindMethods() {
                var panel = ReflectionUtils.GetTypeFromAnyAssembly("VRCSdkControlPanel");
                if (panel == null) yield break;

                const BindingFlags methods = BindingFlags.Public | BindingFlags.NonPublic
                    | BindingFlags.Static | BindingFlags.Instance | BindingFlags.DeclaredOnly;
                var generatedTypes = panel.GetNestedTypes(BindingFlags.NonPublic)
                    .Where(type => type.IsDefined(typeof(CompilerGeneratedAttribute), false));
                var types = new[] { panel }.Concat(generatedTypes);
                foreach (var type in types) {
                    foreach (var method in type.GetMethods(methods)) {
                        if (!method.ContainsGenericParameters && method.GetMethodBody() != null
                            && PatchProcessor.GetOriginalInstructions(method).Any(ChecksPlayMode)) {
                            yield return method;
                        }
                    }
                }
            }
        }

        [VFInit]
        private static void Init() {
            if (!ReflectionHelper.IsReady<Reflection>()) return;
            foreach (var patch in Reflection.Patches) patch.apply();
        }

        private static bool ChecksPlayMode(CodeInstruction instruction) =>
            instruction.Calls(Reflection.EditorIsPlaying) || instruction.Calls(Reflection.ApplicationIsPlaying);

        private static IEnumerable<CodeInstruction> Transpile(IEnumerable<CodeInstruction> instructions) {
            foreach (var instruction in instructions) {
                if (ChecksPlayMode(instruction)) {
                    instruction.opcode = OpCodes.Ldc_I4_0;
                    instruction.operand = null;
                }
                yield return instruction;
            }
        }
    }
}
