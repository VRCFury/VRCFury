using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEditor.Experimental;
using UnityEngine;
using VF.Menu;
using VF.Utils;
using Assembly = System.Reflection.Assembly;

namespace VF.Hooks.UnityFixes {
    /**
     * Unity can show VRCFury components as missing scripts even after successful compilation.
     * A stale MonoScript import or a cached DLL missing Unity's generated script type table can
     * leave the compiled type present but prevent Unity from resolving it when loading an asset.
     *
     * A script is "corrupt" if its MonoScript doesn't exist or doesn't point back to the script
     *
     * First, we try reimporting the script.
     * If that doesn't work, we remove the script's import cache artifacts and reimport it again.
     * If that doesn't work, we request a clean compilation (throw away the bee cache) and then retry.
     */
    internal static class FixMissingScriptsHook {
        [VFInit]
        private static void Init() {
            EditorApplication.update += Check;
        }

        private static void Check() {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating || BuildPipeline.isBuildingPlayer) return;
            EditorApplication.update -= Check;
            if (EditorUtility.scriptCompilationFailed || EditorApplication.isPlayingOrWillChangePlaymode) return;

            VfInitTimingLoggerMenuItem.Run(
                RepairBrokenScripts,
                $"{typeof(FixMissingScriptsHook).FullName}.{nameof(RepairBrokenScripts)}"
            );
        }

        private static void RepairBrokenScripts() {
            var broken = GetBrokenScripts().ToArray();
            if (broken.Length == 0) return;
            var repaired = broken
                .Where(script => TryRepair(script.path, script.type))
                .Select(script => script.path)
                .ToArray();
            var recompile = false;
            foreach (var script in broken) {
                if (!IsBroken(script.path, script.type)) continue;
                if (TryMarkCleanCompilation(AssetDatabase.AssetPathToGUID(script.path))) recompile = true;
            }
            if (recompile) {
                VFLog.Warning("Script reimport did not restore the script types. Recompiling scripts without cached compiler output.");
                CompilationPipeline.RequestScriptCompilation(RequestScriptCompilationOptions.CleanBuildCache);
                return;
            }
            if (repaired.Length == 0) return;

            VFLog.Warning("Repaired Unity's cached script imports. Reloading scripts to restore missing script types:\n"
                          + string.Join("\n", repaired));
            // Reimport repairs the script asset, but existing native fallback components need a reload too.
            EditorUtility.RequestScriptReload();
        }

        internal static IEnumerable<(string path, Type type)> GetBrokenScripts() {
            foreach (var (assembly, asmdefPath) in VrcfuryAssemblyUtils.Assemblies) {
                foreach (var script in GetBrokenScripts(asmdefPath, assembly)) {
                    yield return script;
                }
            }
        }

        internal static IEnumerable<(string path, Type type)> GetBrokenScripts(string asmdefPath, Assembly assembly) {
            var types = assembly.GetTypes()
                // Only check types with a standalone script file matching their class name.
                .Where(type => !type.IsAbstract && !type.IsNested && !type.IsGenericType
                    && (typeof(MonoBehaviour).IsAssignableFrom(type) || typeof(ScriptableObject).IsAssignableFrom(type)))
                .ToDictionary(type => type.Name);
            if (types.Count == 0) yield break;
            var root = VRCFuryAssetDatabase.GetDirectoryName(asmdefPath);
            foreach (var guid in AssetDatabase.FindAssets("t:MonoScript", new[] { root })) {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (!types.TryGetValue(Path.GetFileNameWithoutExtension(path), out var type)) continue;
                if (CompilationPipeline.GetAssemblyDefinitionFilePathFromScriptPath(path) != asmdefPath) continue;
                if (!IsBroken(path, type)) {
                    SessionState.EraseBool(GetAttemptKey(guid));
                    SessionState.EraseBool(GetCleanCompilationKey(guid));
                } else {
                    yield return (path, type);
                }
            }
        }

        private static bool IsBroken(string path, Type type) =>
            AssetDatabase.LoadAssetAtPath<MonoScript>(path)?.GetClass() != type;

        internal static bool TryRepair(string path, Type type) {
            if (!IsBroken(path, type)) return false;
            var guid = AssetDatabase.AssetPathToGUID(path);
            if (string.IsNullOrEmpty(guid)) return false;

            // Keep the limit across the reload we request, including when the repair only works temporarily.
            var attempted = GetAttemptKey(guid);
            if (SessionState.GetBool(attempted, false)) return false;
            SessionState.SetBool(attempted, true);
            try {
                void Reimport() => AssetDatabase.ImportAsset(
                    path,
                    ImportAssetOptions.ForceUpdate
                    | ImportAssetOptions.ForceSynchronousImport
                    | ImportAssetOptions.DontDownloadFromCacheServer
                );
                Reimport();
                if (!IsBroken(path, type)) return true;

                // ForceUpdate can still reuse a corrupt content-addressed artifact. Evict only this script's outputs.
                var artifact = AssetDatabaseExperimental.LookupArtifact(new ArtifactKey(new GUID(guid)));
                if (AssetDatabaseExperimental.GetArtifactPaths(artifact, out var paths)) RemoveArtifacts(paths);
                Reimport();
                if (!IsBroken(path, type)) return true;
                throw new Exception("The reimported script still does not resolve to " + type.FullName + ".");
            } catch (Exception e) {
                VFLog.Warning("Could not repair Unity's script import for " + path + ": " + e.Message);
                return false;
            }
        }

        private static string GetAttemptKey(string guid) => "com.vrcfury.scriptImportRepair." + guid;

        private static string GetCleanCompilationKey(string guid) => GetAttemptKey(guid) + ".cleanCompilation";

        internal static bool TryMarkCleanCompilation(string guid) {
            if (string.IsNullOrEmpty(guid)) return false;
            var key = GetCleanCompilationKey(guid);
            if (SessionState.GetBool(key, false)) return false;
            SessionState.SetBool(key, true);
            // Allow one final import repair using the freshly compiled type information.
            SessionState.EraseBool(GetAttemptKey(guid));
            return true;
        }

        internal static void RemoveArtifacts(IEnumerable<string> paths) {
            var root = Path.GetFullPath("Library/Artifacts") + Path.DirectorySeparatorChar;
            var comparison = Application.platform == RuntimePlatform.WindowsEditor
                ? StringComparison.OrdinalIgnoreCase
                : StringComparison.Ordinal;
            var files = paths.Select(path => Path.GetFullPath(FileUtil.GetPhysicalPath(path))).ToArray();
            // Validate every output before deleting any of them. Never delete sources or other Library data.
            foreach (var file in files) {
                if (!file.StartsWith(root, comparison)) throw new Exception("Unexpected script artifact location: " + file);
            }
            AssetDatabase.ReleaseCachedFileHandles();
            foreach (var file in files) File.Delete(file);
        }
    }
}
