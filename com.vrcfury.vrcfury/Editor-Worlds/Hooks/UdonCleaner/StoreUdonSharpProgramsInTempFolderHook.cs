using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using JetBrains.Annotations;
using UdonSharp;
using UdonSharpEditor;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.SceneManagement;
using VF.Menu;
using VF.Utils;
using VRC.Udon;
using VRC.Udon.Editor.ProgramSources;
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

        private static Dictionary<MonoScript, UdonSharpProgramAsset> _udonSharpMonoScriptToProgram;
        private static Dictionary<AbstractUdonProgramSource, SerializedUdonProgramAsset> _serializedCache;
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

        private static void Reorganize() {
            var storagePath = GetStoragePath();
            if (storagePath == null) return;
            isReorganizing = true;
            try {
                VRCFuryAssetDatabase.WithAssetEditing(() => {
                    var storageAsset = AssetDatabase.LoadMainAssetAtPath(storagePath);
                    if (storageAsset == null) {
                        storageAsset = new AnimatorController();
                        VRCFuryAssetDatabase.SaveAsset(storageAsset, storagePath);
                    }

                    var newPrograms = new List<UdonSharpProgramAsset>();
                    var usharpProgramsByMonoscriptId = new Dictionary<string, UdonSharpProgramAsset>();
                    _udonSharpMonoScriptToProgram = Reorganize<MonoScript,UdonSharpProgramAsset>(
                        storageAsset,
                        FindUdonSharpBehaviourMonoScripts(),
                        program => program.sourceCsScript,
                        (script,program,isNew) => {
                            var scriptGuid = GetGuid(script);
                            program.name = scriptGuid;
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
                        }
                    );

                    var programs = new HashSet<AbstractUdonProgramSource>();
                    programs.UnionWith(FindAll<AbstractUdonProgramSource>());
                    programs.UnionWith(newPrograms);
                    var newSerializedPrograms = new List<SerializedUdonProgramAsset>();
                    var programsWithNewSerialized = new List<AbstractUdonProgramSource>();
                    _serializedCache = Reorganize<AbstractUdonProgramSource, SerializedUdonProgramAsset>(
                        storageAsset,
                        programs,
                        serialized => {
                            if (usharpProgramsByMonoscriptId.TryGetValue(serialized.name, out var usharpProgram)) {
                                return usharpProgram;
                            }
                            var path = AssetDatabase.GUIDToAssetPath(serialized.name);
                            if (string.IsNullOrEmpty(path)) return null;
                            return AssetDatabase.LoadAssetAtPath<AbstractUdonProgramSource>(path);
                        },
                        (program, serialized, isNew) => {
                            if (program is UdonSharpProgramAsset up) {
                                serialized.name = GetGuid(up.sourceCsScript);
                            } else {
                                serialized.name = GetGuid(program);
                            }
                            if (isNew) {
                                programsWithNewSerialized.Add(program);
                                newSerializedPrograms.Add(serialized);
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

            var oldFolder = "Assets/SerializedProgramAsset";
            if (Directory.Exists(oldFolder) && Directory.GetFileSystemEntries(oldFolder).Length == 0) {
                AssetDatabase.DeleteAsset(oldFolder);
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

        private static IEnumerable<MonoScript> FindUdonSharpBehaviourMonoScripts() {
            foreach (var script in FindAll<MonoScript>()) {
                var scriptClass = script.GetClass();
                if (scriptClass == null) continue;
                if (scriptClass.IsAbstract) continue;
                if (!typeof(UdonSharpBehaviour).IsAssignableFrom(scriptClass)) continue;
                yield return script;
            }
        }

        private static IEnumerable<T> FindAll<T>() where T : UnityEngine.Object {
            return AssetDatabase.FindAssets($"t:{typeof(T).Name}")
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(path => !string.IsNullOrEmpty(path))
                .SelectMany(AssetDatabase.LoadAllAssetsAtPath)
                .OfType<T>();
        }

        private static Dictionary<SourceType,OutputType> Reorganize<SourceType,OutputType>(
            UnityEngine.Object storageAsset,
            IEnumerable<SourceType> sources,
            Func<OutputType,SourceType> reverseLookup,
            Action<SourceType,OutputType,bool> onFound
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

                if (AssetDatabase.IsMainAsset(output)) AssetDatabase.DeleteAsset(AssetDatabase.GetAssetPath(output));
                VRCFuryAssetDatabase.AttachAsset(output, storageAsset);
                outputs[source] = output;
                onFound?.Invoke(source, output, false);
            }

            foreach (var source in sources) {
                if (outputs.ContainsKey(source)) continue;
                var output = ScriptableObject.CreateInstance<OutputType>();
                onFound?.Invoke(source, output, true);
                VRCFuryAssetDatabase.AttachAsset(output, storageAsset);
                outputs[source] = output;
            }

            return outputs;
        }

    }
}
