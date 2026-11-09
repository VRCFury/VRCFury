using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using VF.Utils;

namespace VF.Hooks.UnityFixes {
    internal static class FixBlendTreeParameterSyncHook {
        private abstract class Reflection : ReflectionHelper {
            public static readonly Type Graph = ReflectionUtils.GetTypeFromAnyAssembly("UnityEditor.Graphs.AnimationBlendTree.Graph");
            public static readonly FieldInfo Values = Graph?.VFField("m_ParameterValues");
            public static readonly MethodInfo Set = Graph?.VFMethod("SetParameterValue");
            public static readonly HarmonyUtils.PatchObj Patch = HarmonyUtils.Patch(
                typeof(FixBlendTreeParameterSyncHook), nameof(Transpile), Graph, "PopulateParameterValues",
                HarmonyUtils.PatchMode.Transpiler);
        }

        [VFInit]
        private static void Init() {
            if (!ReflectionHelper.IsReady<Reflection>()) return;
            Reflection.Patch.apply();
        }

        private static IEnumerable<CodeInstruction> Transpile(IEnumerable<CodeInstruction> instructions) {
            foreach (var instruction in instructions) {
                if (instruction.Calls(Reflection.Set)) {
                    instruction.opcode = OpCodes.Call;
                    instruction.operand = typeof(FixBlendTreeParameterSyncHook).VFStaticMethod(nameof(Sync));
                }
                yield return instruction;
            }
        }

        private static void Sync(object graph, string name, float value) {
            // Unity otherwise traverses the entire tree for every parameter on every GUI event.
            // Only skip unchanged values during polling; explicit slider edits still propagate normally.
            // Unity clears this dictionary when rebuilding the graph, so new nodes are initialized too.
            var values = (Dictionary<string, float>)Reflection.Values.GetValue(graph);
            if (values.TryGetValue(name, out var previous) && previous == value) return;
            Reflection.Set.Invoke(graph, new object[] { name, value });
        }
    }
}
