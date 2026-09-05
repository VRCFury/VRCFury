#if VRCSDK_3_10_5_OR_NEWER
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using HarmonyLib;
#endif
using UnityEditor;
using VF.Utils;

namespace VF.Hooks.VrcsdkFixes {
    /**
     * "Assets/Create/U# Script" fails if the destination file is in a package that is outside project root
     * This fixes it.
     * https://feedback.vrchat.com/sdk-bug-reports/p/assets-create-u-script-fails-if-destination-file-is-outside-project-root
     */
    internal static class UdonSharpCreateScriptSavePanelHook {
        [ReflectionHelperOptional]
        private abstract class Reflection : ReflectionHelper {
#if !VRCSDK_3_10_5_OR_NEWER
            public static readonly HarmonyUtils.PatchObj Patch = HarmonyUtils.Patch(
                typeof(UdonSharpCreateScriptSavePanelHook),
                nameof(Prefix),
                "UdonSharpEditor.UdonSharpSettings",
                "SanitizeScriptFilePath"
            );
#else
            public static readonly Type UdonSharpBehaviourEditor =
                ReflectionUtils.GetTypeFromAnyAssembly("UdonSharpEditor.UdonSharpBehaviourEditor");
            public static readonly MethodInfo CreateUSharpScript =
                UdonSharpBehaviourEditor?.VFStaticMethod("CreateUSharpScript", new[] { typeof(string), typeof(bool) });
            public static readonly MethodInfo StringStartsWith = typeof(string).VFMethod(
                nameof(string.StartsWith),
                new[] { typeof(string) }
            );
            public static readonly MethodInfo StringSubstring = typeof(string).VFMethod(
                nameof(string.Substring),
                new[] { typeof(int) }
            );
            public static readonly HarmonyUtils.PatchObj Patch = HarmonyUtils.Patch(
                CreateUSharpScript,
                (typeof(UdonSharpCreateScriptSavePanelHook), nameof(ModernTranspiler)),
                HarmonyUtils.PatchMode.Transpiler
            );
#endif
        }

        [VFInit]
        private static void Init() {
            if (!ReflectionHelper.IsReady<Reflection>()) return;
            Reflection.Patch.apply();
        }

#if !VRCSDK_3_10_5_OR_NEWER
        private static void Prefix(ref string __0) {
            if (string.IsNullOrWhiteSpace(__0)) return;
#if UNITY_2021_2_OR_NEWER
            __0 = FileUtil.GetLogicalPath(__0);
#endif
        }
#else
        private static IEnumerable<CodeInstruction> ModernTranspiler(IEnumerable<CodeInstruction> instructions) {
            foreach (var instruction in instructions) {
                if (instruction.Calls(Reflection.StringStartsWith)) {
                    instruction.operand = typeof(UdonSharpCreateScriptSavePanelHook).VFStaticMethod(
                        nameof(LogicalPathStartsWith),
                        new[] { typeof(string), typeof(string) }
                    );
                } else if (instruction.Calls(Reflection.StringSubstring)) {
                    instruction.operand = typeof(UdonSharpCreateScriptSavePanelHook).VFStaticMethod(
                        nameof(GetProjectAssetPath),
                        new[] { typeof(string), typeof(int) }
                    );
                }
                yield return instruction;
            }
        }

        private static bool LogicalPathStartsWith(string path, string rootPath) {
            var logicalPath = FileUtil.GetLogicalPath(path);
            if (Path.IsPathRooted(logicalPath)) return path.StartsWith(rootPath);

            var rootFolder = Path.GetFileName(rootPath.TrimEnd('/', '\\'));
            return logicalPath.StartsWith(rootFolder + "/", StringComparison.Ordinal);
        }

        private static string GetProjectAssetPath(string path, int _) {
            var logicalPath = FileUtil.GetLogicalPath(path);
            return Path.IsPathRooted(logicalPath)
                ? FileUtil.GetProjectRelativePath(path)
                : logicalPath;
        }
#endif
    }
}
