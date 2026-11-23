using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using VF.Utils;
using OpCodes = System.Reflection.Emit.OpCodes;

namespace VF.Hooks.VrcsdkFixes {
    /**
     * Allows the VRChat SDK Control Panel to switch tabs and display their contents in Play Mode.
     * The SDK checks Play Mode in both the tab handler and callbacks generated from CreateGUI.
     * The build panel uses the SDK's disabled-action message while Unity is playing.
     * https://feedback.vrchat.com/sdk-bug-reports/p/sdk-3110-beta1-the-vrcsdk-panel-doesnt-work-properly-in-play-mode
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
            public static readonly HarmonyUtils.PatchObj[] Patches = FindPlayModeCheckPatches()
                .Concat(FindBuildRefreshPatches())
                .ToArray();

            private static IEnumerable<HarmonyUtils.PatchObj> FindPlayModeCheckPatches() {
                return FindMethods(typeof(VRCSdkControlPanel))
                    .Where(method => PatchProcessor.GetOriginalInstructions(method).Any(ChecksPlayMode))
                    .Select(method => HarmonyUtils.Patch(
                        method,
                        (typeof(AllowSdkControlPanelInPlayModeHook), nameof(Transpile)),
                        HarmonyUtils.PatchMode.Transpiler
                    ));
            }

            private static IEnumerable<HarmonyUtils.PatchObj> FindBuildRefreshPatches() {
                foreach (var builder in TypeCache.GetTypesDerivedFrom<IVRCSdkControlPanelBuilder>()) {
                    if (builder.FullName != "VRC.SDK3A.Editor.VRCSdkControlPanelAvatarBuilder"
                        && builder.FullName != "VRC.SDK3.Editor.VRCSdkControlPanelWorldBuilder") continue;

                    // This message identifies the scheduled validation callback across SDK layouts.
                    var refresh = FindMethods(builder).SingleOrDefault(method =>
                        method.Name.StartsWith("<CreateBuildGUI>", StringComparison.Ordinal)
                        && PatchProcessor.GetOriginalInstructions(method).Any(instruction =>
                            instruction.opcode == OpCodes.Ldstr
                            && Equals(instruction.operand, "Please set a name and thumbnail before uploading")));
                    yield return HarmonyUtils.Patch(
                        refresh,
                        (typeof(AllowSdkControlPanelInPlayModeHook), nameof(UpdateBuildButtons)),
                        HarmonyUtils.PatchMode.Postfix
                    );
                }
            }

            private static IEnumerable<MethodInfo> FindMethods(Type owner) {
                const BindingFlags methods = BindingFlags.Public | BindingFlags.NonPublic
                    | BindingFlags.Static | BindingFlags.Instance | BindingFlags.DeclaredOnly;
                var generatedTypes = owner.GetNestedTypes(BindingFlags.NonPublic)
                    .Where(type => type.IsDefined(typeof(CompilerGeneratedAttribute), false));
                var types = new[] { owner }.Concat(generatedTypes);
                foreach (var type in types) {
                    foreach (var method in type.GetMethods(methods)) {
                        if (!method.ContainsGenericParameters && method.GetMethodBody() != null) {
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

        private static void UpdateBuildButtons() {
            if (!EditorApplication.isPlaying) return;
            var window = VRCSdkControlPanel.window;
            if (window == null) return;
            var root = window.rootVisualElement.Q("build-panel-gui");
            if (root == null) return;

            ShowBuildDisabledMessage(root.Q<Label>("main-action-disabled-text"));
            ShowBuildDisabledMessage(root.Q<Label>("local-test-disabled-text"));
            ShowBuildDisabledMessage(root.Q<Label>("build-and-upload-disabled-text"));
        }

        private static void ShowBuildDisabledMessage(Label label) {
            if (label?.parent?.ClassListContains("d-none") != true) return;
            label.text = "Exit Play Mode to Build";
            label.parent.RemoveFromClassList("d-none");
        }
    }
}
