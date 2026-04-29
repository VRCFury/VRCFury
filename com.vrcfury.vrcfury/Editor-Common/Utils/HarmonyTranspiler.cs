using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using UnityEngine;

namespace VF.Utils {
    internal static class HarmonyTranspiler {
        public static void TranspileVarAccess(
            IEnumerable<Assembly> assemblies,
            Type transpilerClass,
            params (FieldInfo, string, string)[] injects
        ) {
            varAccessInjects = injects.Select(inject => (
                inject.Item1,
                transpilerClass.VFStaticMethod(inject.Item2),
                transpilerClass.VFStaticMethod(inject.Item3)
            )).ToArray();

            var start = Time.realtimeSinceStartup;
            var methodsToPatch = assemblies
                .SelectMany(GetAllTypesAndNested)
                .SelectMany(type => type.GetMethods(
                    BindingFlags.Public | BindingFlags.NonPublic |
                    BindingFlags.Static | BindingFlags.Instance |
                    BindingFlags.DeclaredOnly
                ))
                .Where(MethodUsesTargetFields)
                .ToArray();
            var elapsed = Time.realtimeSinceStartup - start;
            //Debug.Log($"Searched for methods to patch in {elapsed} seconds");

            start = Time.realtimeSinceStartup;
            foreach (var method in methodsToPatch) {
                try {
                    madeChange = false;
                    HarmonyUtils.Transpile(typeof(HarmonyTranspiler), nameof(RunVarAccessTranspile), method);
                    if (madeChange) {
                        //Debug.Log($"Patched: {method.DeclaringType?.FullName} {method.Name}");
                    }
                } catch (Exception) {
                    Debug.LogWarning($"Failed to patch {method.DeclaringType?.FullName} {method.Name}");
                }
            }
            elapsed = Time.realtimeSinceStartup - start;
            //Debug.Log($"Transpiled in {elapsed} seconds");
        }

        private static bool madeChange = false;
        private static IList<(FieldInfo TargetField, MethodInfo Getter, MethodInfo Setter)> varAccessInjects;

        private static IEnumerable<CodeInstruction> RunVarAccessTranspile(IEnumerable<CodeInstruction> instructions) {
            foreach (var instruction in instructions) {
                bool isLd = instruction.opcode == OpCodes.Ldfld;
                bool isSt = instruction.opcode == OpCodes.Stfld;

                if (isLd || isSt) {
                    var operand = instruction.operand as FieldInfo;
                    var inject = varAccessInjects.FirstOrDefault(i => i.TargetField == operand);
                    if (inject != default) {
                        var replacementMethod = isLd ? inject.Getter : inject.Setter;
                        if (replacementMethod != null) {
                            var newInst = new CodeInstruction(OpCodes.Call, replacementMethod) {
                                labels = instruction.labels,
                                blocks = instruction.blocks
                            };
                            madeChange = true;
                            yield return newInst;
                            continue;
                        }
                    }
                }
                yield return instruction;
            }
        }

        private static bool MethodUsesTargetFields(MethodInfo method) {
            List<CodeInstruction> instructions;
            try {
                instructions = PatchProcessor.GetOriginalInstructions(method);
            } catch (Exception) {
                return false;
            }
            if (instructions == null) return false;

            foreach (var inst in instructions) {
                if (inst.opcode == OpCodes.Ldfld || inst.opcode == OpCodes.Stfld) {
                    var operandField = inst.operand as FieldInfo;
                    if (operandField != null && varAccessInjects.Any(i => i.TargetField == operandField)) {
                        return true;
                    }
                }
            }
            return false;
        }

        private static IEnumerable<Type> GetAllTypesAndNested(Assembly assembly) {
            foreach (var type in assembly.GetTypes()) {
                yield return type;
                foreach (var nested in type.GetNestedTypes()) {
                    yield return nested;
                }
            }
        }
    }
}
