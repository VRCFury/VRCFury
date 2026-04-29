using System.Collections.Generic;
using System.IO;
using UdonSharp;
using UnityEditor;
using VF.Menu;
using VF.Utils;
using VRC.Udon.Serialization.OdinSerializer;

namespace VF.Hooks.UdonCleaner {
    /**
     * UdonSharpBehaviour uses odin serializer, which internally stores a copy of the prefab object inside EACH INSTANCE.
     * This means basically EVERY single prefab instance of a u# behaviour will have a junk override set on it, making
     * the overrides dropdown in unity basically worthless.
     *
     * This hack changes that mechanism so that the prefab links are stored out-of-band (in our own txt file in Library).
     * We restore the field odin expects just before it needs it, then immediately afterward, we pull it back out to our text file and clear the field again.
     * This means, aside from exactly when odin needs it, the field will always appear empty and not overridden.
     */
    internal static class UdonSharpPrefabLinksStorageHook {
        private sealed class SaveCacheOnWillSaveAssets : AssetModificationProcessor {
            private static string[] OnWillSaveAssets(string[] paths) {
                if (!UdonCleanerMenuItem.Get()) return paths;
                SaveCacheToDisk();
                return paths;
            }
        }

        private abstract class Reflection : ReflectionHelper {
            public static readonly HarmonyUtils.PatchObj PatchBeforePrefix = HarmonyUtils.Patch(
                typeof(UdonSharpPrefabLinksStorageHook),
                nameof(BeforePrefix),
                typeof(UdonSharpBehaviour),
                "UnityEngine.ISerializationCallbackReceiver.OnBeforeSerialize"
            );
            public static readonly HarmonyUtils.PatchObj PatchBeforePostfix = HarmonyUtils.Patch(
                typeof(UdonSharpPrefabLinksStorageHook),
                nameof(BeforePostfix),
                typeof(UdonSharpBehaviour),
                "UnityEngine.ISerializationCallbackReceiver.OnBeforeSerialize",
                HarmonyUtils.PatchMode.Finalizer
            );
            public static readonly HarmonyUtils.PatchObj PatchAfterPrefix = HarmonyUtils.Patch(
                typeof(UdonSharpPrefabLinksStorageHook),
                nameof(AfterPrefix),
                typeof(UdonSharpBehaviour),
                "UnityEngine.ISerializationCallbackReceiver.OnAfterDeserialize"
            );
            public static readonly HarmonyUtils.PatchObj PatchAfterPostfix = HarmonyUtils.Patch(
                typeof(UdonSharpPrefabLinksStorageHook),
                nameof(AfterPostfix),
                typeof(UdonSharpBehaviour),
                "UnityEngine.ISerializationCallbackReceiver.OnAfterDeserialize",
                HarmonyUtils.PatchMode.Finalizer
            );
        }

        private static readonly Dictionary<UnityEngine.Object, UnityEngine.Object> prefabByInstance = new Dictionary<UnityEngine.Object, UnityEngine.Object>();
        private static readonly object mapLock = new object();
        private static string cachePath;

        [InitializeOnLoadMethod]
        private static void Init() {
            if (!UdonCleanerMenuItem.Get()) return;
            cachePath = Path.Combine(Directory.GetParent(UnityEngine.Application.dataPath)?.FullName ?? "", "Library", "VRCFury", "SerializationDataPrefabCache.txt");
            LoadCacheFromDisk();
            if (!ReflectionHelper.IsReady<Reflection>()) return;
            Reflection.PatchBeforePrefix.apply();
            Reflection.PatchBeforePostfix.apply();
            Reflection.PatchAfterPrefix.apply();
            Reflection.PatchAfterPostfix.apply();
            AssemblyReloadEvents.beforeAssemblyReload += SaveCacheToDisk;
            EditorApplication.quitting += SaveCacheToDisk;
        }

        private static void BeforePrefix(UdonSharpBehaviour __instance) {
            TryApplyCachedPrefab(__instance);
        }

        private static void BeforePostfix(UdonSharpBehaviour __instance) {
            TryCaptureAndClearPrefab(__instance);
        }

        private static void AfterPrefix(UdonSharpBehaviour __instance) {
            TryApplyCachedPrefab(__instance);
        }

        private static void AfterPostfix(UdonSharpBehaviour __instance) {
            TryCaptureAndClearPrefab(__instance);
        }

        private static void TryApplyCachedPrefab(UdonSharpBehaviour instance) {
            if (instance == null) return;
            lock (mapLock) {
                if (!prefabByInstance.TryGetValue(instance, out var cachedPrefab)) return;
                TrySetPrefab(instance, cachedPrefab);
            }
        }

        private static void TryCaptureAndClearPrefab(UdonSharpBehaviour instance) {
            if (instance == null) return;
            if (!TryGetPrefab(instance, out var prefab)) return;

            lock (mapLock) {
                if (prefab != null) {
                    prefabByInstance[instance] = prefab;
                } else {
                    prefabByInstance.Remove(instance);
                }
            }

            TrySetPrefab(instance, null);
        }

        private static bool TryGetPrefab(UdonSharpBehaviour instance, out UnityEngine.Object prefab) {
            prefab = null;
            if (!(instance is ISupportsPrefabSerialization supporter)) return false;
            var data = supporter.SerializationData;
            prefab = data.Prefab;
            return true;
        }

        private static bool TrySetPrefab(UdonSharpBehaviour instance, UnityEngine.Object prefab) {
            if (!(instance is ISupportsPrefabSerialization supporter)) return false;
            var data = supporter.SerializationData;
            data.Prefab = prefab;
            supporter.SerializationData = data;
            return true;
        }

        private static void PruneDeadEntries() {
            List<UnityEngine.Object> toRemove = null;
            foreach (var kvp in prefabByInstance) {
                if (kvp.Key != null && kvp.Value != null) continue;
                if (toRemove == null) toRemove = new List<UnityEngine.Object>();
                toRemove.Add(kvp.Key);
            }

            if (toRemove == null) return;
            foreach (var key in toRemove) {
                prefabByInstance.Remove(key);
            }
        }

        private static void SaveCacheToDisk() {
            if (!UdonCleanerMenuItem.Get()) return;
            try {
                var lines = new List<string>();

                lock (mapLock) {
                    PruneDeadEntries();
                    var instanceObjects = new List<UnityEngine.Object>(prefabByInstance.Count);
                    var prefabObjects = new List<UnityEngine.Object>(prefabByInstance.Count);
                    foreach (var kvp in prefabByInstance) {
                        if (kvp.Key == null || kvp.Value == null) continue;
                        instanceObjects.Add(kvp.Key);
                        prefabObjects.Add(kvp.Value);
                    }

                    if (instanceObjects.Count > 0) {
                        var instanceObjectArray = instanceObjects.ToArray();
                        var prefabObjectArray = prefabObjects.ToArray();
                        var instanceIds = new GlobalObjectId[instanceObjectArray.Length];
                        var prefabIds = new GlobalObjectId[prefabObjectArray.Length];

                        GlobalObjectId.GetGlobalObjectIdsSlow(instanceObjectArray, instanceIds);
                        GlobalObjectId.GetGlobalObjectIdsSlow(prefabObjectArray, prefabIds);

                        for (var i = 0; i < instanceIds.Length; i++) {
                            if (instanceIds[i].identifierType == 0) continue;
                            if (prefabIds[i].identifierType == 0) continue;
                            lines.Add(instanceIds[i] + " " + prefabIds[i]);
                        }
                    }
                }

                var dir = Path.GetDirectoryName(cachePath);
                if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
                var tempPath = cachePath + ".tmp";
                File.WriteAllLines(tempPath, lines, System.Text.Encoding.UTF8);
                if (File.Exists(cachePath)) {
                    File.Replace(tempPath, cachePath, null);
                } else {
                    File.Move(tempPath, cachePath);
                }
            } catch {
            }
        }

        private static void LoadCacheFromDisk() {
            try {
                if (string.IsNullOrEmpty(cachePath) || !File.Exists(cachePath)) return;
                var lines = File.ReadAllLines(cachePath, System.Text.Encoding.UTF8);
                if (lines == null || lines.Length == 0) return;

                lock (mapLock) {
                    prefabByInstance.Clear();
                    var instanceIds = new List<GlobalObjectId>(lines.Length);
                    var prefabIds = new List<GlobalObjectId>(lines.Length);
                    foreach (var line in lines) {
                        if (string.IsNullOrWhiteSpace(line)) continue;
                        var split = line.IndexOf(' ');
                        if (split <= 0 || split >= line.Length - 1) continue;
                        var instanceText = line.Substring(0, split).Trim();
                        var prefabText = line.Substring(split + 1).Trim();
                        if (!GlobalObjectId.TryParse(instanceText, out var instanceId) || instanceId.identifierType == 0) continue;
                        if (!GlobalObjectId.TryParse(prefabText, out var prefabId) || prefabId.identifierType == 0) continue;
                        instanceIds.Add(instanceId);
                        prefabIds.Add(prefabId);
                    }

                    if (instanceIds.Count > 0) {
                        var instanceIdArray = instanceIds.ToArray();
                        var prefabIdArray = prefabIds.ToArray();
                        var instanceObjects = new UnityEngine.Object[instanceIdArray.Length];
                        var prefabObjects = new UnityEngine.Object[prefabIdArray.Length];

                        GlobalObjectId.GlobalObjectIdentifiersToObjectsSlow(instanceIdArray, instanceObjects);
                        GlobalObjectId.GlobalObjectIdentifiersToObjectsSlow(prefabIdArray, prefabObjects);

                        for (var i = 0; i < instanceObjects.Length; i++) {
                            var instance = instanceObjects[i];
                            var prefab = prefabObjects[i];
                            if (instance == null || prefab == null) continue;
                            prefabByInstance[instance] = prefab;
                        }
                    }
                }
            } catch {
            }
        }

        internal static void ClearCache() {
            try {
                lock (mapLock) {
                    prefabByInstance.Clear();
                }

                if (string.IsNullOrEmpty(cachePath)) return;
                if (File.Exists(cachePath)) File.Delete(cachePath);
                var tempPath = cachePath + ".tmp";
                if (File.Exists(tempPath)) File.Delete(tempPath);
            } catch {
            }
        }
    }
}
