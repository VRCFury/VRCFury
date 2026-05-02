using System;
using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;
using UdonSharp;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using VF.Menu;
using VF.Utils;
using VRC.Udon;
using VRC.Udon.Editor.ProgramSources.UdonGraphProgram.UI;
using VRC.Udon.ProgramSources;
using Object = UnityEngine.Object;

namespace VF.Hooks.UdonCleaner {
    /**
     * Handles ownership of Udon's temporary assets that shouldn't live in Assets.
     */
    internal static class UdonCleanerAssetManager {

        public static Dictionary<MonoScript, UdonSharpProgramAsset> _udonSharpMonoScriptToProgram =
            new Dictionary<MonoScript, UdonSharpProgramAsset>();
        public static Dictionary<AbstractUdonProgramSource, SerializedUdonProgramAsset> _serializedCache =
            new Dictionary<AbstractUdonProgramSource, SerializedUdonProgramAsset>();
        private static bool isReorganizing;

        private const Layout DefaultEnabledLayout = Layout.STORAGE_ASSET;

        public static UdonSharpProgramAsset GetProgramForUSharpScript(MonoScript script) {
            return _udonSharpMonoScriptToProgram.GetValueOrDefault(script);
        }
        public static UdonSharpProgramAsset[] GetAllUSharpPrograms() {
            return _udonSharpMonoScriptToProgram.Values.ToArray();
        }
        public static SerializedUdonProgramAsset GetSerializedForProgram(AbstractUdonProgramSource program) {
            return _serializedCache.GetValueOrDefault(program);
        }

        [CanBeNull]
        private static string GetStoragePath() {
            var tmpPackagePath = TmpFilePackage.GetPath();
            if (tmpPackagePath == null) return null;
            return $"{tmpPackagePath}/Udon";
        }

        private static string GetGuid(Object obj) {
            AssetDatabase.TryGetGUIDAndLocalFileIdentifier(obj, out var scriptGuid, out long _);
            return scriptGuid;
        }

        // [MenuItem("Tools/Reorganize to Vanilla")]
        // public static void MenuItem() {
        //     Reorganize(Layout.VANILLA);
        // }
        // [MenuItem("Tools/Reorganize to Asset")]
        // public static void MenuItem2() {
        //     Reorganize(Layout.STORAGE_ASSET);
        // }
        // [MenuItem("Tools/Reorganize to Dirs")]
        // public static void MenuItem3() {
        //     Reorganize(Layout.STORAGE_DIRS);
        // }

        public enum Layout {
            VANILLA,
            STORAGE_ASSET,
            STORAGE_DIRS
        }

        private static bool isAssetEditing = false;
        private static ISet<Object> dirtyObjects = new HashSet<Object>();

        public static void Reorganize(Layout layout) {
            var storagePath = GetStoragePath();
            if (storagePath == null) return;
            var storageAssetPath = $"{storagePath}/TempStorage.asset";

            isReorganizing = true;
            List<AbstractUdonProgramSource> programsWithNewSerialized = null;
            try {
                var existingScripts = FindUdonSharpBehaviourMonoScripts();
                var existingPrograms = FindAll<AbstractUdonProgramSource>(true);
                var nonUsPrograms = existingPrograms
                    .Where(p => !(p is UdonSharpProgramAsset));
                var existingSerialized = FindAll<SerializedUdonProgramAsset>(true);

                try {
                    var storageAsset = new Lazy<Object>(() => {
                        var a = AssetDatabase.LoadMainAssetAtPath(storageAssetPath);
                        if (a == null) {
                            a = new AnimatorController();
                            Move(a, storageAssetPath);
                        }
                        return a;
                    });

                    var programsByOriginalGuid = new Dictionary<string, AbstractUdonProgramSource>();
                    foreach (var program in existingPrograms) {
                        if (!AssetDatabase.IsMainAsset(program)) continue;
                        var guid = GetGuid(program);
                        if (guid == null) continue;
                        programsByOriginalGuid[guid] = program;
                    }

                    var newPrograms = new List<UdonSharpProgramAsset>();
                    var usharpProgramsByMonoscriptId = new Dictionary<string, UdonSharpProgramAsset>();
                    _udonSharpMonoScriptToProgram = Reorganize(
                        existingScripts,
                        existingPrograms.OfType<UdonSharpProgramAsset>(),
                        program => program.sourceCsScript,
                        (script,program,isNew) => {
                            var scriptGuid = GetGuid(script);
                            if (program.sourceCsScript != script) {
                                program.sourceCsScript = script;
                                Dirty(program);
                            }
                            if (program.ScriptVersion != UdonSharpProgramVersion.CurrentVersion) {
                                program.ScriptVersion = UdonSharpProgramVersion.CurrentVersion;
                                Dirty(program);
                            }
                            if (isNew) newPrograms.Add(program);
                            usharpProgramsByMonoscriptId[scriptGuid] = program;

                            if (layout == Layout.VANILLA) {
                                var scriptPath = AssetDatabase.GetAssetPath(script);
                                if (string.IsNullOrEmpty(scriptPath)) return null;
                                return scriptPath.ReplaceLast(".cs", ".asset");
                            } else if (layout == Layout.STORAGE_ASSET) {
                                if (program.name != scriptGuid) {
                                    program.name = scriptGuid;
                                    Dirty(program);
                                }
                                return storageAsset.Value;
                            } else {
                                return $"{storagePath}/UdonSharpPrograms/{scriptGuid}.asset";
                            }
                        }
                    );

                    var programs = new HashSet<AbstractUdonProgramSource>();
                    programs.UnionWith(nonUsPrograms);
                    programs.UnionWith(_udonSharpMonoScriptToProgram.Values);
                    programsWithNewSerialized = new List<AbstractUdonProgramSource>();
                    _serializedCache = Reorganize(
                        programs.NotNull(),
                        existingSerialized,
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
                            }
                            var programGuid = GetGuid(program);
                            if (layout == Layout.VANILLA) {
                                return $"Assets/SerializedUdonPrograms/{programGuid}.asset";
                            } else if (layout == Layout.STORAGE_ASSET) {
                                var name = (program is UdonSharpProgramAsset up)
                                    ? GetGuid(up.sourceCsScript)
                                    :GetGuid(program);
                                if (serialized.name != name) {
                                    serialized.name = name;
                                    Dirty(serialized);
                                }
                                return storageAsset.Value;
                            } else {
                                return $"{storagePath}/SerializedPrograms/{programGuid}.asset";
                            }
                        }
                    );

#if UNITY_2022_1_OR_NEWER
                    foreach (var obj in dirtyObjects) {
                        AssetDatabase.SaveAssetIfDirty(obj);
                    }
#endif
                } finally {
                    if (isAssetEditing) {
                        isAssetEditing = false;
                        AssetDatabase.StopAssetEditing();
                    }
                }

                // RefreshProgram performs importer work and must run outside StartAssetEditing.
                if (programsWithNewSerialized != null) {
                    foreach (var program in programsWithNewSerialized) {
                        if (program == null) continue;
                        program.RefreshProgram();
                    }
                }
                UdonCleanerReflection.ClearProgramAssetCache();

                if (layout != Layout.STORAGE_ASSET) {
                    AssetDatabase.DeleteAsset(storageAssetPath);
                }
                VRCFuryAssetDatabase.DeleteDirIfEmpty("Assets/SerializedUdonPrograms");
                VRCFuryAssetDatabase.DeleteDirIfEmpty($"{storagePath}/SerializedPrograms");
                VRCFuryAssetDatabase.DeleteDirIfEmpty($"{storagePath}/UdonSharpPrograms");
                VRCFuryAssetDatabase.DeleteDirIfEmpty(storagePath);
            } finally {
                isReorganizing = false;
            }
        }

        private static void Dirty(Object obj) {
            EditorUtility.SetDirty(obj);
            dirtyObjects.Add(obj);
            if (isAssetEditing) return;
            isAssetEditing = true;
            AssetDatabase.StartAssetEditing();
        }

        public class PostProcessor : AssetPostprocessor {
            private static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets, string[] movedAssets, string[] movedFromAssetPaths
#if UNITY_2022_1_OR_NEWER
                , bool didDomainReload
#endif
                ) {
                if (!UdonCleanerMenuItem.Get()) return;
                if (!ReflectionHelper.IsReady<UdonCleanerReflection>()) return;
                if (isReorganizing) return;
                var tmpRoot = GetStoragePath();
                if (tmpRoot == null) return;
                if (
#if UNITY_2022_1_OR_NEWER
                    didDomainReload ||
#endif
                    deletedAssets.Contains(tmpRoot) || movedFromAssetPaths.Contains(tmpRoot)) {
                    Reorganize(DefaultEnabledLayout);
                }
            }
        }

        public static IEnumerable<MonoScript> FindUdonSharpBehaviourMonoScripts() {
            foreach (var script in FindAll<MonoScript>(false)) {
                var scriptClass = script.GetClass();
                if (scriptClass == null) continue;
                if (scriptClass.IsAbstract) continue;
                if (!typeof(UdonSharpBehaviour).IsAssignableFrom(scriptClass)) continue;
                yield return script;
            }
        }

        public static T[] FindAll<T>(bool includeSubassets) where T : Object {
            return AssetDatabase.FindAssets($"t:{typeof(T).Name}")
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(path => !string.IsNullOrEmpty(path))
                .SelectMany(path => includeSubassets
                    ? AssetDatabase.LoadAllAssetsAtPath(path).OfType<T>()
                    : new [] { AssetDatabase.LoadAssetAtPath<T>(path) }
                )
                .NotNull()
                .ToArray();
        }

        internal class AssetOrPath {
            public Object asset;
            public string path;
            public static implicit operator AssetOrPath(string _path) => new AssetOrPath { path = _path };
            public static implicit operator AssetOrPath(Object _asset) => new AssetOrPath { asset = _asset };
        }

        private static Dictionary<SourceType,OutputType> Reorganize<SourceType,OutputType>(
            IEnumerable<SourceType> sources,
            IEnumerable<OutputType> existingOutputs,
            Func<OutputType,SourceType> reverseLookup,
            Func<SourceType,OutputType,bool,AssetOrPath> onFound
        )
            where SourceType : Object
            where OutputType : ScriptableObject
        {
            var outputs = new Dictionary<SourceType, OutputType>();

            foreach (var output in existingOutputs) {
                var source = reverseLookup(output);
                if (source == null || outputs.ContainsKey(source)) {
                    Destroy(output);
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

        private static void Destroy(Object obj) {
            if (obj == null) return;
            var outputPath = AssetDatabase.GetAssetPath(obj);
            Debug.Log($"Removing {obj.GetType().Name} {outputPath} {obj.name}");
            if (!string.IsNullOrEmpty(outputPath)) {
                if (AssetDatabase.IsMainAsset(obj)) AssetDatabase.DeleteAsset(outputPath);
                else AssetDatabase.RemoveObjectFromAsset(obj);
            }
        }

        private static void Move(Object obj, AssetOrPath dest) {
            if (dest == null) return;

            var oldPath = AssetDatabase.GetAssetPath(obj);

            if (dest.asset != null) {
                var storageAsset = dest.asset;
                if (AssetDatabase.GetAssetPath(storageAsset) == oldPath) return;
                Dirty(obj);
                var wasMainAsset = AssetDatabase.IsMainAsset(obj);
                VRCFuryAssetDatabase.AttachAsset(obj, storageAsset);
                if (wasMainAsset) {
                    AssetDatabase.DeleteAsset(oldPath);
                }
                return;
            }
            if (dest.path != null) {
                var desiredPath = dest.path;
                if (oldPath == desiredPath) return;
                Dirty(obj);
                AssetDatabase.DeleteAsset(desiredPath);
                if (AssetDatabase.IsMainAsset(obj)) {
                    VRCFuryAssetDatabase.MoveAsset(oldPath, desiredPath);
                } else {
                    VRCFuryAssetDatabase.SaveAsset(obj, desiredPath);
                }
                return;
            }
        }
    }
}
