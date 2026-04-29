using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using JetBrains.Annotations;
using UdonSharp;
using UdonSharpEditor;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using VF.Menu;
using VF.Utils;
using VRC.Udon;
using VRC.Udon.Editor.ProgramSources;
using VRC.Udon.Editor.ProgramSources.UdonGraphProgram.UI;
using VRC.Udon.ProgramSources;
using HarmonyTranspiler = VF.Utils.HarmonyTranspiler;

namespace VF.Hooks.UdonCleaner {
    internal static class StoreUdonSharpProgramsInTempFolderHook {
        public abstract class Reflection : ReflectionHelper {
            public static readonly HarmonyUtils.PatchObj PatchGetAllUdonSharpPrograms = HarmonyUtils.Patch(
                (typeof(UdonSharpProgramAsset), nameof(UdonSharpProgramAsset.GetAllUdonSharpPrograms)),
                (typeof(StoreUdonSharpProgramsInTempFolderHook), nameof(OnGetAllUdonSharpPrograms))
            );
            public static readonly HarmonyUtils.PatchObj PatchGetSerializedProgramAssetWithoutRefresh = HarmonyUtils.Patch(
                (typeof(UdonSharpProgramAsset), "GetSerializedProgramAssetWithoutRefresh"),
                (typeof(StoreUdonSharpProgramsInTempFolderHook), nameof(OnGetSerializedProgramAssetWithoutRefresh))
            );
        }

        private static bool OnGetAllUdonSharpPrograms(ref UdonSharpProgramAsset[] __result) {
            __result = _udonSharpMonoScriptToProgram.Values.ToArray();
            return false;
        }

        private static bool OnGetSerializedProgramAssetWithoutRefresh(UdonSharpProgramAsset __instance, ref AbstractSerializedUdonProgramAsset __result) {
            __result = _serializedCache.GetValueOrDefault(__instance);
            return false;
        }

        public static AbstractUdonProgramSource programSource_get(UdonBehaviour ub) {
            foreach (var usb in ub.GetComponents<UdonSharpBehaviour>()) {
                if (UdonSharpEditorUtility.GetBackingUdonBehaviour(usb) == ub) {
                    var script = MonoScript.FromMonoBehaviour(usb);
                    return _udonSharpMonoScriptToProgram?.GetValueOrDefault(script);
                }
            }
            return ub.programSource;
        }

        private static void programSource_set(UdonBehaviour ub, AbstractUdonProgramSource program) {
            // do nothing!
        }

        public static AbstractSerializedUdonProgramAsset serializedProgramAsset_get(UdonBehaviour ub) {
            var program = programSource_get(ub);
            if (program == null) return null;
            return _serializedCache?.GetValueOrDefault(program);
        }

        private static void serializedProgramAsset_set(UdonBehaviour ub, AbstractSerializedUdonProgramAsset program) {
            // do nothing!
        }

        private static AbstractSerializedUdonProgramAsset serializedUdonProgramAsset_get(UdonProgramAsset program) {
            return _serializedCache?.GetValueOrDefault(program);
        }

        private static void serializedUdonProgramAsset_set(UdonProgramAsset ub, AbstractSerializedUdonProgramAsset program) {
            // do nothing!
        }

        [InitializeOnLoadMethod]
        private static void Init() {
            if (!UdonCleanerMenuItem.Get()) return;
            if (!ReflectionHelper.IsReady<UdonCleanerReflection>()) return;
            if (!ReflectionHelper.IsReady<Reflection>()) return;

            Reflection.PatchGetAllUdonSharpPrograms.apply();
            Reflection.PatchGetSerializedProgramAssetWithoutRefresh.apply();

            HarmonyTranspiler.TranspileVarAccess(
                AppDomain.CurrentDomain.GetAssemblies().Where(a => {
                    var name = a.GetName().Name;
                    return name == "UdonSharp.Editor"
                           || name == "VRC.ClientSim.Editor"
                           || name == "VRC.Udon.Editor"
                           || name == "VRC.Udon";
                }),
                typeof(StoreUdonSharpProgramsInTempFolderHook),
                (UdonCleanerReflection.programSource, nameof(programSource_get), nameof(programSource_set)),
                (UdonCleanerReflection.serializedProgramAsset, nameof(serializedProgramAsset_get), nameof(serializedProgramAsset_set)),
                (UdonCleanerReflection.serializedUdonProgramAsset, nameof(serializedUdonProgramAsset_get), nameof(serializedUdonProgramAsset_set))
            );

            Reorganize();
        }

        public static Dictionary<MonoScript, UdonSharpProgramAsset> _udonSharpMonoScriptToProgram;
        public static Dictionary<AbstractUdonProgramSource, SerializedUdonProgramAsset> _serializedCache;
        private static bool isReorganizing;

        [CanBeNull]
        private static string GetStoragePath() {
            var tmpPackagePath = TmpFilePackage.GetPath();
            if (tmpPackagePath == null) return null;
            return $"{tmpPackagePath}/Udon/TempStorage.asset";
        }

        private static string GetGuid(UnityEngine.Object obj) {
            AssetDatabase.TryGetGUIDAndLocalFileIdentifier(obj, out var scriptGuid, out long _);
            return scriptGuid;
        }

        public static void Reorganize(bool legacyLayout = false) {
            isReorganizing = true;
            try {
                VRCFuryAssetDatabase.WithAssetEditing(() => {
                    var storagePath = GetStoragePath();
                    if (storagePath == null) return;
                    var storageAsset = new Lazy<UnityEngine.Object>(() => {
                        var a = AssetDatabase.LoadMainAssetAtPath(storagePath);
                        if (a == null) {
                            a = new AnimatorController();
                            VRCFuryAssetDatabase.SaveAsset(a, storagePath);
                        }
                        return a;
                    });

                    var programsByOriginalGuid = new Dictionary<string, AbstractUdonProgramSource>();
                    foreach (var program in FindAll<AbstractUdonProgramSource>()) {
                        if (!AssetDatabase.IsMainAsset(program)) continue;
                        var guid = GetGuid(program);
                        if (guid == null) continue;
                        programsByOriginalGuid[guid] = program;
                    }

                    var newPrograms = new List<UdonSharpProgramAsset>();
                    var usharpProgramsByMonoscriptId = new Dictionary<string, UdonSharpProgramAsset>();
                    _udonSharpMonoScriptToProgram = Reorganize<MonoScript,UdonSharpProgramAsset>(
                        FindUdonSharpBehaviourMonoScripts(),
                        program => program.sourceCsScript,
                        (script,program,isNew) => {
                            var scriptGuid = GetGuid(script);
                            if (program.sourceCsScript != script) {
                                program.sourceCsScript = script;
                                EditorUtility.SetDirty(program);
                            }
                            if (program.ScriptVersion != UdonSharpProgramVersion.CurrentVersion) {
                                program.ScriptVersion = UdonSharpProgramVersion.CurrentVersion;
                                EditorUtility.SetDirty(program);
                            }
                            if (isNew) newPrograms.Add(program);
                            usharpProgramsByMonoscriptId[scriptGuid] = program;

                            if (legacyLayout) {
                                var scriptPath = AssetDatabase.GetAssetPath(script);
                                if (string.IsNullOrEmpty(scriptPath)) return null;
                                var path = scriptPath.ReplaceLast(".cs", ".asset");
                                program.name = Path.GetFileNameWithoutExtension(path);
                                return path;
                            } else {
                                program.name = scriptGuid;
                                return storageAsset.Value;
                            }
                        }
                    );

                    var programs = new HashSet<AbstractUdonProgramSource>();
                    programs.UnionWith(FindAll<AbstractUdonProgramSource>());
                    programs.UnionWith(newPrograms);
                    var newSerializedPrograms = new List<SerializedUdonProgramAsset>();
                    var programsWithNewSerialized = new List<AbstractUdonProgramSource>();
                    _serializedCache = Reorganize<AbstractUdonProgramSource, SerializedUdonProgramAsset>(
                        programs,
                        serialized => {
                            if (usharpProgramsByMonoscriptId.TryGetValue(serialized.name, out var usharpProgram)) {
                                return usharpProgram;
                            }
                            if (programsByOriginalGuid.TryGetValue(serialized.name, out var program) && program != null) {
                                return program;
                            }
                            return null;
                        },
                        (program, serialized, isNew) => {
                            if (isNew) {
                                programsWithNewSerialized.Add(program);
                                newSerializedPrograms.Add(serialized);
                            }
                            if (legacyLayout) {
                                serialized.name = GetGuid(program);
                                return $"Assets/SerializedUdonPrograms/{serialized.name}.asset";
                            } else {
                                if (program is UdonSharpProgramAsset up) {
                                    serialized.name = GetGuid(up.sourceCsScript);
                                } else {
                                    serialized.name = GetGuid(program);
                                }
                                return storageAsset.Value;
                            }
                        }
                    );

                    foreach (var program in programsWithNewSerialized) {
                        program.RefreshProgram();
                    }

                    foreach (var s in newPrograms) {
                        AssetDatabase.SaveAssetIfDirty(s);
                    }
                    foreach (var s in newSerializedPrograms) {
                        AssetDatabase.SaveAssetIfDirty(s);
                    }
                });
            } finally {
                isReorganizing = false;
            }
            UdonCleanerReflection.ClearProgramAssetCache();

            if (!legacyLayout) {
                var oldFolder = "Assets/SerializedUdonPrograms";
                if (Directory.Exists(oldFolder) && Directory.GetFileSystemEntries(oldFolder).Length == 0) {
                    AssetDatabase.DeleteAsset(oldFolder);
                }
            }
        }

        public class PostProcessor : AssetPostprocessor {
            private static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets, string[] movedAssets, string[] movedFromAssetPaths) {
                if (!UdonCleanerMenuItem.Get()) return;
                if (!ReflectionHelper.IsReady<UdonCleanerReflection>()) return;
                if (isReorganizing) return;
                var tmpRoot = GetStoragePath();
                if (tmpRoot == null) return;
                if (deletedAssets.Contains(tmpRoot) || movedFromAssetPaths.Contains(tmpRoot)) {
                    Reorganize();
                }
            }
        }

        public static IEnumerable<MonoScript> FindUdonSharpBehaviourMonoScripts() {
            foreach (var script in FindAll<MonoScript>()) {
                var scriptClass = script.GetClass();
                if (scriptClass == null) continue;
                if (scriptClass.IsAbstract) continue;
                if (!typeof(UdonSharpBehaviour).IsAssignableFrom(scriptClass)) continue;
                yield return script;
            }
        }

        public static IEnumerable<T> FindAll<T>() where T : UnityEngine.Object {
            return AssetDatabase.FindAssets($"t:{typeof(T).Name}")
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(path => !string.IsNullOrEmpty(path))
                .SelectMany(AssetDatabase.LoadAllAssetsAtPath)
                .OfType<T>();
        }

        internal class AssetOrPath {
            public UnityEngine.Object asset;
            public string path;
            public static implicit operator AssetOrPath(string _path) => new AssetOrPath { path = _path };
            public static implicit operator AssetOrPath(UnityEngine.Object _asset) => new AssetOrPath { asset = _asset };
        }

        private static Dictionary<SourceType,OutputType> Reorganize<SourceType,OutputType>(
            IEnumerable<SourceType> sources,
            Func<OutputType,SourceType> reverseLookup,
            Func<SourceType,OutputType,bool,AssetOrPath> onFound
        )
            where SourceType : UnityEngine.Object
            where OutputType : ScriptableObject
        {
            var outputs = new Dictionary<SourceType, OutputType>();

            foreach (var output in FindAll<OutputType>()) {
                var source = reverseLookup(output);
                if (source == null || outputs.ContainsKey(source)) {
                    var outputPath = AssetDatabase.GetAssetPath(output);
                    Debug.Log($"Removing unattached {typeof(OutputType).Name} {outputPath} {output.name}");
                    if (AssetDatabase.IsMainAsset(output)) AssetDatabase.DeleteAsset(outputPath);
                    UnityEngine.Object.DestroyImmediate(output, true);
                    continue;
                }

                var dest = onFound?.Invoke(source, output, false);
                Move(output, dest);
                outputs[source] = output;
            }

            foreach (var source in sources) {
                if (outputs.ContainsKey(source)) continue;
                Debug.Log($"Creating missing {typeof(OutputType).Name} for source {source.name}");
                var output = ScriptableObject.CreateInstance<OutputType>();
                var dest = onFound?.Invoke(source, output, true);
                Move(output, dest);
                outputs[source] = output;
            }

            return outputs;
        }

        private static void Move(UnityEngine.Object obj, AssetOrPath dest) {
            if (dest == null) return;
            if (dest.asset != null) {
                var storageAsset = dest.asset;
                if (AssetDatabase.IsMainAsset(obj))
                    AssetDatabase.DeleteAsset(AssetDatabase.GetAssetPath(obj));
                VRCFuryAssetDatabase.AttachAsset(obj, storageAsset);
                return;
            }
            if (dest.path != null) {
                var desiredPath = dest.path;
                var outputPath = AssetDatabase.GetAssetPath(obj);
                if (!string.Equals(outputPath, desiredPath, StringComparison.OrdinalIgnoreCase)) {
                    var existingAtDesired = AssetDatabase.LoadMainAssetAtPath(desiredPath);
                    if (existingAtDesired != null) {
                        AssetDatabase.DeleteAsset(desiredPath);
                        UnityEngine.Object.DestroyImmediate(existingAtDesired, true);
                    }
                    VRCFuryAssetDatabase.SaveAsset(obj, desiredPath);
                }
                return;
            }
        }
    }
}
