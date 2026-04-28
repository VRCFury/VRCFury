using System;
using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;
using UdonSharp;
using UdonSharpEditor;
using UnityEditor;
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
        private static string GetTempRoot() {
            var tmpPackagePath = TmpFilePackage.GetPath();
            if (tmpPackagePath == null) return null;
            return $"{tmpPackagePath}/Udon";
        }

        private static void Reorganize() {
            var tmpRoot = GetTempRoot();
            if (tmpRoot == null) return;
            isReorganizing = true;
            try {
                VRCFuryAssetDatabase.WithAssetEditing(() => {
                    var newPrograms = new List<UdonSharpProgramAsset>();
                    _udonSharpMonoScriptToProgram = Reorganize<MonoScript,UdonSharpProgramAsset>(
                        $"{tmpRoot}/ProgramAssets",
                        FindUdonSharpBehaviourMonoScripts(),
                        program => program.sourceCsScript,
                        (script,program,isNew) => {
                            if (program.sourceCsScript != script) {
                                program.sourceCsScript = script;
                                EditorUtility.SetDirty(program);
                            }
                            if (program.ScriptVersion != UdonSharpProgramVersion.CurrentVersion) {
                                program.ScriptVersion = UdonSharpProgramVersion.CurrentVersion;
                                EditorUtility.SetDirty(program);
                            }
                            if (isNew) newPrograms.Add(program);
                        }
                    );


                    var programs = new HashSet<AbstractUdonProgramSource>();
                    programs.UnionWith(FindAll<AbstractUdonProgramSource>());
                    programs.UnionWith(newPrograms);
                    var newSerializedPrograms = new List<SerializedUdonProgramAsset>();
                    var programsWithNewSerialized = new List<AbstractUdonProgramSource>();
                    _serializedCache = Reorganize<AbstractUdonProgramSource, SerializedUdonProgramAsset>(
                        $"{tmpRoot}/SerializedPrograms",
                        programs,
                        serialized => {
                            var path = AssetDatabase.GUIDToAssetPath(serialized.name);
                            if (string.IsNullOrEmpty(path)) return null;
                            return AssetDatabase.LoadAssetAtPath<AbstractUdonProgramSource>(path);
                        },
                        (program, serialized, isNew) => {
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
        }


        public class PostProcessor : AssetPostprocessor {
            private static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets, string[] movedAssets, string[] movedFromAssetPaths) {
                if (!UdonCleanerMenuItem.Get()) return;
                if (!ReflectionHelper.IsReady<UdonCleanerReflection>()) return;
                if (isReorganizing) return;
                var tmpRoot = GetTempRoot();
                if (tmpRoot == null) return;
                if (deletedAssets.Any(d => d.StartsWith(tmpRoot))) {
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

        private static IEnumerable<T> FindAll<T>() where T : UnityEngine.Object{
            foreach (var guid in AssetDatabase.FindAssets($"t:{typeof(T).Name}")) {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (string.IsNullOrEmpty(path)) continue;
                var asset = AssetDatabase.LoadAssetAtPath<T>(path);
                if (asset == null) continue;
                yield return asset;
            }
        }

        private static Dictionary<SourceType,OutputType> Reorganize<SourceType,OutputType>(
            string outputRoot,
            IEnumerable<SourceType> sources,
            Func<OutputType,SourceType> reverseLookup,
            Action<SourceType,OutputType,bool> onFound
        )
            where SourceType : UnityEngine.Object
            where OutputType : ScriptableObject
        {

            var outputs = new Dictionary<SourceType, OutputType>();

            foreach (var output in FindAll<OutputType>()) {
                var path = AssetDatabase.GetAssetPath(output);
                var source = reverseLookup(output);
                if (source == null) {
                    Debug.Log(path + " does not belong to a source");
                    AssetDatabase.DeleteAsset(path);
                    UnityEngine.Object.DestroyImmediate(output, true);
                    continue;
                }
                if (!AssetDatabase.TryGetGUIDAndLocalFileIdentifier(source, out string sourceGuid, out long _)) continue;
                if (string.IsNullOrEmpty(sourceGuid)) continue;

                if (outputs.ContainsKey(source)) {
                    AssetDatabase.DeleteAsset(path);
                    UnityEngine.Object.DestroyImmediate(output, true);
                    continue;
                }

                output.name = sourceGuid;

                var desiredPath = outputRoot + "/" + sourceGuid + ".asset";
                if (string.Equals(path, desiredPath, StringComparison.OrdinalIgnoreCase)) {
                    outputs[source] = output;
                    onFound?.Invoke(source, output, false);
                    continue;
                }

                var existingAtDesired = AssetDatabase.LoadAssetAtPath<UdonSharpProgramAsset>(desiredPath);
                if (existingAtDesired != null) {
                    AssetDatabase.DeleteAsset(path);
                    UnityEngine.Object.DestroyImmediate(output, true);
                    continue;
                }

                var moveError = AssetDatabase.MoveAsset(path, desiredPath);
                if (!string.IsNullOrEmpty(moveError)) {
                    AssetDatabase.DeleteAsset(path);
                    UnityEngine.Object.DestroyImmediate(output, true);
                    continue;
                }

                outputs[source] = output;
                onFound?.Invoke(source, output, false);
            }

            foreach (var source in sources) {
                if (outputs.ContainsKey(source)) continue;

                if (!AssetDatabase.TryGetGUIDAndLocalFileIdentifier(source, out string sourceGuid, out long _))
                    continue;

                var path = outputRoot + "/" + sourceGuid + ".asset";
                var output = ScriptableObject.CreateInstance<OutputType>();
                output.name = sourceGuid;
                onFound?.Invoke(source, output, true);
                VRCFuryAssetDatabase.SaveAsset(output, path);
                outputs[source] = output;
            }

            return outputs;
        }

    }
}
