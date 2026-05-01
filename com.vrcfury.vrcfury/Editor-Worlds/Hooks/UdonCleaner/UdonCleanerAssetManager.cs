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
    /**
     * Handles ownership of Udon's temporary assets that shouldn't live in Assets.
     * > Moves them out of Assets
     * > Patches the VRCSDK to use our authoritative list rather than maintaining its own connections
     */
    internal static class UdonCleanerAssetManager {

        public static Dictionary<MonoScript, UdonSharpProgramAsset> _udonSharpMonoScriptToProgram =
            new Dictionary<MonoScript, UdonSharpProgramAsset>();
        public static Dictionary<AbstractUdonProgramSource, SerializedUdonProgramAsset> _serializedCache =
            new Dictionary<AbstractUdonProgramSource, SerializedUdonProgramAsset>();
        private static bool isReorganizing;

        private const Layout DefaultEnabledLayout = Layout.STORAGE_ASSET;

        public abstract class Reflection : ReflectionHelper {
            public static readonly HarmonyUtils.PatchObj PatchGetAllUdonSharpPrograms = HarmonyUtils.Patch(
                (typeof(UdonSharpProgramAsset), nameof(UdonSharpProgramAsset.GetAllUdonSharpPrograms)),
                (typeof(UdonCleanerAssetManager), nameof(OnGetAllUdonSharpPrograms))
            );
            public static readonly HarmonyUtils.PatchObj PatchGetSerializedProgramAssetWithoutRefresh = HarmonyUtils.Patch(
                (typeof(UdonSharpProgramAsset), "GetSerializedProgramAssetWithoutRefresh"),
                (typeof(UdonCleanerAssetManager), nameof(OnGetSerializedProgramAssetWithoutRefresh))
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
                typeof(UdonCleanerAssetManager),
                (UdonCleanerReflection.programSource, nameof(programSource_get), nameof(programSource_set)),
                (UdonCleanerReflection.serializedProgramAsset, nameof(serializedProgramAsset_get), nameof(serializedProgramAsset_set)),
                (UdonCleanerReflection.serializedUdonProgramAsset, nameof(serializedUdonProgramAsset_get), nameof(serializedUdonProgramAsset_set))
            );
        }

        [CanBeNull]
        private static string GetStoragePath() {
            var tmpPackagePath = TmpFilePackage.GetPath();
            if (tmpPackagePath == null) return null;
            return $"{tmpPackagePath}/Udon";
        }

        private static string GetGuid(UnityEngine.Object obj) {
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

        public static void Reorganize(Layout layout) {
            var storagePath = GetStoragePath();
            if (storagePath == null) return;
            var storageAssetPath = $"{storagePath}/TempStorage.asset";

            isReorganizing = true;
            List<AbstractUdonProgramSource> programsWithNewSerialized = null;
            try {
                var existingScripts = FindUdonSharpBehaviourMonoScripts();
                var existingPrograms = FindAll<AbstractUdonProgramSource>();
                var nonUsPrograms = existingPrograms
                    .Where(p => !(p is UdonSharpProgramAsset));
                var existingSerialized = FindAll<SerializedUdonProgramAsset>();

                VRCFuryAssetDatabase.WithAssetEditing(() => {
                    var storageAsset = new Lazy<UnityEngine.Object>(() => {
                        var a = AssetDatabase.LoadMainAssetAtPath(storageAssetPath);
                        if (a == null) {
                            a = new AnimatorController();
                            VRCFuryAssetDatabase.SaveAsset(a, storageAssetPath);
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
                                EditorUtility.SetDirty(program);
                            }
                            if (program.ScriptVersion != UdonSharpProgramVersion.CurrentVersion) {
                                program.ScriptVersion = UdonSharpProgramVersion.CurrentVersion;
                                EditorUtility.SetDirty(program);
                            }
                            if (isNew) newPrograms.Add(program);
                            usharpProgramsByMonoscriptId[scriptGuid] = program;

                            if (layout == Layout.VANILLA) {
                                var scriptPath = AssetDatabase.GetAssetPath(script);
                                if (string.IsNullOrEmpty(scriptPath)) return null;
                                return scriptPath.ReplaceLast(".cs", ".asset");
                            } else if (layout == Layout.STORAGE_ASSET) {
                                program.name = scriptGuid;
                                return storageAsset.Value;
                            } else {
                                return $"{storagePath}/UdonSharpPrograms/{scriptGuid}.asset";
                            }
                        }
                    );

                    var programs = new HashSet<AbstractUdonProgramSource>();
                    programs.UnionWith(nonUsPrograms);
                    programs.UnionWith(_udonSharpMonoScriptToProgram.Values);
                    var newSerializedPrograms = new List<SerializedUdonProgramAsset>();
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
                                newSerializedPrograms.Add(serialized);
                            }
                            var programGuid = GetGuid(program);
                            if (layout == Layout.VANILLA) {
                                return $"Assets/SerializedUdonPrograms/{programGuid}.asset";
                            } else if (layout == Layout.STORAGE_ASSET) {
                                if (program is UdonSharpProgramAsset up) {
                                    serialized.name = GetGuid(up.sourceCsScript);
                                } else {
                                    serialized.name = GetGuid(program);
                                }
                                return storageAsset.Value;
                            } else {
                                return $"{storagePath}/SerializedPrograms/{programGuid}.asset";
                            }
                        }
                    );

#if UNITY_2022_1_OR_NEWER
                    foreach (var s in _udonSharpMonoScriptToProgram.Values) {
                        AssetDatabase.SaveAssetIfDirty(s);
                    }
                    foreach (var s in _udonSharpMonoScriptToProgram.Values) {
                        AssetDatabase.SaveAssetIfDirty(s);
                    }
                    if (storageAsset.IsValueCreated) {
                        AssetDatabase.SaveAssetIfDirty(storageAsset.Value);
                    }
#endif
                });

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

        public class PostProcessor : AssetPostprocessor {
            private static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets, string[] movedAssets, string[] movedFromAssetPaths, bool didDomainReload) {
                if (!UdonCleanerMenuItem.Get()) return;
                if (!ReflectionHelper.IsReady<UdonCleanerReflection>()) return;
                if (isReorganizing) return;
                var tmpRoot = GetStoragePath();
                if (tmpRoot == null) return;
                if (didDomainReload || deletedAssets.Contains(tmpRoot) || movedFromAssetPaths.Contains(tmpRoot)) {
                    Reorganize(DefaultEnabledLayout);
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

        public static T[] FindAll<T>() where T : UnityEngine.Object {
            return AssetDatabase.FindAssets($"t:{typeof(T).Name}")
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(path => !string.IsNullOrEmpty(path))
                .SelectMany(AssetDatabase.LoadAllAssetsAtPath)
                .OfType<T>()
                .NotNull()
                .ToArray();
        }

        internal class AssetOrPath {
            public UnityEngine.Object asset;
            public string path;
            public static implicit operator AssetOrPath(string _path) => new AssetOrPath { path = _path };
            public static implicit operator AssetOrPath(UnityEngine.Object _asset) => new AssetOrPath { asset = _asset };
        }

        private static Dictionary<SourceType,OutputType> Reorganize<SourceType,OutputType>(
            IEnumerable<SourceType> sources,
            IEnumerable<OutputType> existingOutputs,
            Func<OutputType,SourceType> reverseLookup,
            Func<SourceType,OutputType,bool,AssetOrPath> onFound
        )
            where SourceType : UnityEngine.Object
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

        private static void Destroy(UnityEngine.Object obj) {
            if (obj == null) return;
            var outputPath = AssetDatabase.GetAssetPath(obj);
            Debug.Log($"Removing {obj.GetType().Name} {outputPath} {obj.name}");
            if (!string.IsNullOrEmpty(outputPath)) {
                if (AssetDatabase.IsMainAsset(obj)) AssetDatabase.DeleteAsset(outputPath);
                else AssetDatabase.RemoveObjectFromAsset(obj);
            }
        }

        private static void Move(UnityEngine.Object obj, AssetOrPath dest) {
            if (dest == null) return;

            var oldPath = AssetDatabase.GetAssetPath(obj);

            if (dest.asset != null) {
                var storageAsset = dest.asset;
                if (AssetDatabase.GetAssetPath(storageAsset) == oldPath) return;
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
