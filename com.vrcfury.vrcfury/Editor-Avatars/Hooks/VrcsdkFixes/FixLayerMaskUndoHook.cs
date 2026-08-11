using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEditor;
using VF.Utils;

namespace VF.Hooks.VrcsdkFixes {
    /**
     * The VRCSDK synchronizes Gesture and FX masks while opening the descriptor inspector and after a real build.
     * These automatic updates should not create undo entries.
     */
    internal static class FixLayerMaskUndoHook {
        private abstract class Reflection : ReflectionHelper {
            public static readonly MethodInfo ApplyModifiedProperties = typeof(SerializedObject)
                .VFMethod(nameof(SerializedObject.ApplyModifiedProperties), new Type[] { });
            public static readonly MethodInfo ApplyModifiedPropertiesWithoutUndo = typeof(SerializedObject)
                .VFMethod(nameof(SerializedObject.ApplyModifiedPropertiesWithoutUndo), new Type[] { });

            public static readonly HarmonyUtils.PatchObj InspectorPatch = HarmonyUtils.Patch(
                typeof(FixLayerMaskUndoHook),
                nameof(Transpiler),
                "AvatarDescriptorEditor3",
                "OnEnable",
                HarmonyUtils.PatchMode.Transpiler
            );
            public static readonly HarmonyUtils.PatchObj BuildPatch = HarmonyUtils.Patch(
                typeof(FixLayerMaskUndoHook),
                nameof(Transpiler),
                "VRC.SDK3A.Editor.VRCSdkControlPanelAvatarBuilder",
                "SetLayerMaskFromControllerInternal",
                HarmonyUtils.PatchMode.Transpiler
            );
        }

        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions) {
            foreach (var instruction in instructions) {
                if (instruction.Calls(Reflection.ApplyModifiedProperties)) {
                    instruction.operand = Reflection.ApplyModifiedPropertiesWithoutUndo;
                }
                yield return instruction;
            }
        }

        [VFInit]
        private static void Init() {
            if (!ReflectionHelper.IsReady<Reflection>()) return;
            Reflection.InspectorPatch.apply();
            Reflection.BuildPatch.apply();
        }
    }
}
