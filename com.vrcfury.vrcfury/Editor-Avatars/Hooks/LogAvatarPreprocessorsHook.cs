using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using UnityEditor;
using UnityEngine;
using VF.Menu;
using VF.Utils;
using VRC.SDKBase.Editor.BuildPipeline;
using Debug = UnityEngine.Debug;

namespace VF.Hooks {
    internal static class LogAvatarPreprocessorsHook {
        private const string EditorPref = "com.vrcfury.logAvatarPreprocessors";

        public static bool Get() => EditorPrefs.GetBool(EditorPref, false);

        [MenuItem(MenuItems.logAvatarPreprocessors, priority = MenuItems.logAvatarPreprocessorsPriority)]
        private static void Click() => EditorPrefs.SetBool(EditorPref, !Get());

        [MenuItem(MenuItems.logAvatarPreprocessors, true)]
        private static bool Validate() {
            UnityEditor.Menu.SetChecked(MenuItems.logAvatarPreprocessors, Get());
            return true;
        }

        private abstract class Reflection : ReflectionHelper {
            public static readonly MethodInfo Callback = typeof(IVRCSDKPreprocessAvatarCallback)
                .GetMethod(nameof(IVRCSDKPreprocessAvatarCallback.OnPreprocessAvatar));
            public static readonly MethodInfo Wrapper = typeof(LogAvatarPreprocessorsHook)
                .VFStaticMethod(nameof(Run));
            public static readonly HarmonyUtils.PatchObj Patch = HarmonyUtils.Patch(
                typeof(LogAvatarPreprocessorsHook),
                nameof(Transpiler),
                typeof(VRCBuildPipelineCallbacks),
                nameof(VRCBuildPipelineCallbacks.OnPreprocessAvatar),
                HarmonyUtils.PatchMode.Transpiler
            );
            public static readonly HarmonyUtils.PatchObj BeginPatch = HarmonyUtils.Patch(
                typeof(LogAvatarPreprocessorsHook),
                nameof(Begin),
                typeof(VRCBuildPipelineCallbacks),
                nameof(VRCBuildPipelineCallbacks.OnPreprocessAvatar)
            );
            public static readonly HarmonyUtils.PatchObj FinishPatch = HarmonyUtils.Patch(
                typeof(LogAvatarPreprocessorsHook),
                nameof(Finish),
                typeof(VRCBuildPipelineCallbacks),
                nameof(VRCBuildPipelineCallbacks.OnPreprocessAvatar),
                HarmonyUtils.PatchMode.Finalizer
            );
        }

        [VFInit]
        private static void Init() {
            if (!ReflectionHelper.IsReady<Reflection>()) return;
            Reflection.Patch.apply();
            Reflection.BeginPatch.apply();
            Reflection.FinishPatch.apply();
        }

        private class Report {
            public string avatarName;
            public readonly List<(string name, double milliseconds, string outcome)> timings =
                new List<(string name, double milliseconds, string outcome)>();
        }

        [System.ThreadStatic] private static Report current;

        private static void Begin(GameObject __0, out Report __state) {
            __state = current;
            current = Get() ? new Report { avatarName = __0 != null ? __0.name : "(null)" } : null;
        }

        private static void Finish(Report __state) {
            var report = current;
            current = __state;
            if (report == null) return;
            Debug.Log(
                $"[VRCFury] Avatar preprocessor timings for {report.avatarName}:\n" +
                string.Join("\n", report.timings
                    .OrderByDescending(t => t.milliseconds)
                    .Select(t => $"{t.milliseconds:F2} ms  {t.name}{t.outcome}"))
            );
        }

        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions) {
            foreach (var instruction in instructions) {
                if (instruction.Calls(Reflection.Callback)) {
                    instruction.opcode = OpCodes.Call;
                    instruction.operand = Reflection.Wrapper;
                }
                yield return instruction;
            }
        }

        private static bool Run(IVRCSDKPreprocessAvatarCallback callback, GameObject avatar) {
            var report = current;
            if (report == null) return callback.OnPreprocessAvatar(avatar);
            var started = Stopwatch.GetTimestamp();
            var finished = false;
            var result = false;
            try {
                result = callback.OnPreprocessAvatar(avatar);
                finished = true;
                return result;
            } finally {
                var milliseconds = (Stopwatch.GetTimestamp() - started) * 1000.0 / Stopwatch.Frequency;
                var outcome = !finished ? " (threw an exception)" : result ? "" : " (returned false)";
                report.timings.Add((callback.GetType().FullName, milliseconds, outcome));
            }
        }
    }
}
