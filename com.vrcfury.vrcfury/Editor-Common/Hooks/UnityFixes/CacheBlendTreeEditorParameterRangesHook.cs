using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using HarmonyLib;
using VF.Utils;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace VF.Hooks.UnityFixes {
    /**
     * Unity's blendtree graph editor recalculates the same ranges for every slider, and it's really expensive.
     * Caching them cuts the render time by a lot.
     * It also repeatedly measures and shortens identical slot labels across shared nodes and GUI events.
     */
    internal static class CacheBlendTreeEditorParameterRangesHook {
        private abstract class Reflection : ReflectionHelper {
            public static readonly Func<BlendTree, int, string> Name = (Func<BlendTree, int, string>)typeof(BlendTree)
                .VFMethod("GetRecursiveBlendParameter")?.CreateDelegate(typeof(Func<BlendTree, int, string>));
            public static readonly Func<BlendTree, int, float> Min = (Func<BlendTree, int, float>)typeof(BlendTree)
                .VFMethod("GetRecursiveBlendParameterMin")?.CreateDelegate(typeof(Func<BlendTree, int, float>));
            public static readonly Func<BlendTree, int, float> Max = (Func<BlendTree, int, float>)typeof(BlendTree)
                .VFMethod("GetRecursiveBlendParameterMax")?.CreateDelegate(typeof(Func<BlendTree, int, float>));
            public static readonly HarmonyUtils.PatchObj Draw = HarmonyUtils.Patch(
                typeof(CacheBlendTreeEditorParameterRangesHook), nameof(Transpile),
                "UnityEditor.Graphs.AnimationBlendTree.GraphGUI", "NodeGUI", HarmonyUtils.PatchMode.Transpiler);
            public static readonly HarmonyUtils.PatchObj Rebuild = HarmonyUtils.Patch(
                typeof(CacheBlendTreeEditorParameterRangesHook), nameof(Clear),
                "UnityEditor.Graphs.AnimationBlendTree.Graph", "BuildFromBlendTree");
            public static readonly HarmonyUtils.PatchObj LabelPrefix = HarmonyUtils.Patch(
                typeof(CacheBlendTreeEditorParameterRangesHook), nameof(LabelPrefix),
                "UnityEditor.Graphs.AnimationBlendTree.GraphGUI", "LimitStringWidth");
            public static readonly HarmonyUtils.PatchObj LabelPostfix = HarmonyUtils.Patch(
                typeof(CacheBlendTreeEditorParameterRangesHook), nameof(LabelPostfix),
                "UnityEditor.Graphs.AnimationBlendTree.GraphGUI", "LimitStringWidth", HarmonyUtils.PatchMode.Postfix);
        }

        private static ConditionalWeakTable<BlendTree, Dictionary<int, (string name, float min, float max)>> cache = new();
        private static ConditionalWeakTable<GUIStyle, Dictionary<(string text, float width, float dpi), string>> labels = new();

        [VFInit]
        private static void Init() {
            if (!ReflectionHelper.IsReady<Reflection>()) return;
            Reflection.Draw.apply();
            Reflection.Rebuild.apply();
            Reflection.LabelPrefix.apply();
            Reflection.LabelPostfix.apply();
            Undo.undoRedoPerformed += Clear;
            EditorApplication.projectChanged += Clear;
        }

        private static void Clear() {
            cache = new();
            labels = new();
        }

        private static bool LabelPrefix(string __0, float __1, GUIStyle __2, ref string __result) =>
            !labels.GetOrCreateValue(__2).TryGetValue((__0, __1, EditorGUIUtility.pixelsPerPoint), out __result);

        private static void LabelPostfix(string __0, float __1, GUIStyle __2, string __result) {
            // Slot titles repeat across shared nodes and GUI events. Keep Unity's original shortening algorithm.
            labels.GetOrCreateValue(__2)[(__0, __1, EditorGUIUtility.pixelsPerPoint)] = __result;
        }

        private static (string name, float min, float max) Get(BlendTree tree, int index) {
            // Shared subtrees repeat these expensive recursive native queries on every GUI event.
            // Unity rebuilds the graph when its preview controllers become dirty after a tree edit.
            var parameters = cache.GetOrCreateValue(tree);
            if (!parameters.TryGetValue(index, out var value)) {
                value = (Reflection.Name(tree, index), Reflection.Min(tree, index), Reflection.Max(tree, index));
                parameters[index] = value;
            }
            return value;
        }

        private static string Name(BlendTree tree, int index) => Get(tree, index).name;
        private static float Min(BlendTree tree, int index) => Get(tree, index).min;
        private static float Max(BlendTree tree, int index) => Get(tree, index).max;

        private static IEnumerable<CodeInstruction> Transpile(IEnumerable<CodeInstruction> instructions) {
            foreach (var instruction in instructions) {
                if (instruction.operand is MethodInfo method && method.DeclaringType == typeof(BlendTree)) {
                    var replacement = method.Name switch {
                        "GetRecursiveBlendParameter" => nameof(Name),
                        "GetRecursiveBlendParameterMin" => nameof(Min),
                        "GetRecursiveBlendParameterMax" => nameof(Max),
                        _ => null
                    };
                    if (replacement != null) {
                        instruction.opcode = OpCodes.Call;
                        instruction.operand = typeof(CacheBlendTreeEditorParameterRangesHook).VFStaticMethod(replacement);
                    }
                }
                yield return instruction;
            }
        }
    }
}
