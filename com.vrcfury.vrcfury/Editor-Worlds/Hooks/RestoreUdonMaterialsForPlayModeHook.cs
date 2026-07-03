using System;
using System.Collections.Generic;
using System.Linq;
using UdonSharp;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using VF.Menu;
using VF.Utils;
using VRC.Udon;

namespace VF.Hooks {
    internal static class RestoreUdonMaterialsForPlayModeHook {
        private static Dictionary<Material, Material> originalSnapshots;
        private static Dictionary<Material, Material> pendingSaveSnapshots;

        [VFInit]
        private static void Init() {
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange state) {
            if (state == PlayModeStateChange.ExitingEditMode) {
                BeginTracking();
            } else if (state == PlayModeStateChange.ExitingPlayMode) {
                RestoreForPlayModeExit();
            } else if (state == PlayModeStateChange.EnteredEditMode) {
                ClearSnapshots(ref pendingSaveSnapshots);
                ClearSnapshots(ref originalSnapshots);
            }
        }

        private static void BeginTracking() {
            ClearSnapshots(ref pendingSaveSnapshots);
            ClearSnapshots(ref originalSnapshots);

            if (!PlayModeMenuItem.Get()) return;

            var materials = FindTrackedMaterials().Distinct().Where(m => m != null).ToArray();
            if (materials.Length == 0) return;

            originalSnapshots = materials.ToDictionary(mat => mat, SnapshotMaterial);
        }

        private static IEnumerable<Material> FindTrackedMaterials() {
            var foundMaterials = new HashSet<Material>();

            foreach (var root in VFGameObject.GetRoots()) {
                foreach (var behaviour in root.GetComponentsInSelfAndChildren<UdonBehaviour>()) {
                    CollectTrackedMaterials(behaviour, foundMaterials);
                }
                foreach (var behaviour in root.GetComponentsInSelfAndChildren<UdonSharpBehaviour>()) {
                    CollectTrackedMaterials(behaviour, foundMaterials);
                }
            }

            return foundMaterials;
        }

        private static void CollectTrackedMaterials(UnityEngine.Object obj, HashSet<Material> foundMaterials) {
            var so = new SerializedObject(obj);
            foreach (var prop in so.IterateFast()) {
                if (prop.propertyType != SerializedPropertyType.ObjectReference) continue;
                AddTrackedMaterial(prop.objectReferenceValue, foundMaterials);
            }
        }

        private static void AddTrackedMaterial(UnityEngine.Object obj, HashSet<Material> foundMaterials) {
            switch (obj) {
                case Material mat:
                    foundMaterials.Add(mat);
                    break;
                case CustomRenderTexture crt when crt.material != null:
                    foundMaterials.Add(crt.material);
                    break;
            }
        }

        private static Material SnapshotMaterial(Material material) {
            var snapshot = UnityEngine.Object.Instantiate(material);
            snapshot.name = material.name;
            snapshot.hideFlags = HideFlags.HideAndDontSave;
            return snapshot;
        }

        private static Dictionary<Material, Material> SnapshotCurrentMaterials(IEnumerable<Material> materials) {
            if (materials == null) return null;
            return materials
                .Where(mat => mat != null)
                .Distinct()
                .ToDictionary(mat => mat, SnapshotMaterial);
        }

        private static void RestoreSnapshots(Dictionary<Material, Material> snapshots) {
            if (snapshots == null) return;

            foreach (var (material, snapshot) in snapshots) {
                if (material == null || snapshot == null) continue;
                EditorUtility.CopySerialized(snapshot, material);
                EditorUtility.SetDirty(material);
            }
        }

        private static void RestoreForPlayModeExit() {
            if (originalSnapshots == null) return;

            if (pendingSaveSnapshots != null) {
                RestoreSnapshots(pendingSaveSnapshots);
                ClearSnapshots(ref pendingSaveSnapshots);
            }

            LogUndoneProperties();
            RestoreSnapshots(originalSnapshots);
            ClearSnapshots(ref originalSnapshots);
        }

        private static void LogUndoneProperties() {
            if (originalSnapshots == null) return;

            var lines = new List<string>();
            foreach (var (material, snapshot) in originalSnapshots) {
                if (material == null || snapshot == null) continue;

                var changedPaths = GetChangedPropertyPaths(material, snapshot).Distinct().ToArray();
                if (changedPaths.Length == 0) continue;

                var assetPath = AssetDatabase.GetAssetPath(material);
                var label = string.IsNullOrEmpty(assetPath) ? material.name : assetPath;
                lines.Add(label + ": " + string.Join(", ", changedPaths));
            }

            if (lines.Count == 0) return;
            Debug.LogWarning(
                "[VRCFury] Restored play mode material changes on exit:\n" + string.Join("\n", lines)
            );
        }

        private static IEnumerable<string> GetChangedPropertyPaths(Material material, Material snapshot) {
            if (material == null || snapshot == null) yield break;
            if (material.shader == null || snapshot.shader == null) yield break;
            if (material.shader != snapshot.shader) {
                yield return "shader";
                yield break;
            }

            foreach (var i in Enumerable.Range(0, material.shader.GetPropertyCount())) {
                var propertyName = material.shader.GetPropertyName(i);
                switch (material.shader.GetPropertyType(i)) {
                    case ShaderPropertyType.Color:
                        if (material.GetColor(propertyName) != snapshot.GetColor(propertyName)) {
                            yield return propertyName;
                        }
                        break;
                    case ShaderPropertyType.Vector:
                        if (material.GetVector(propertyName) != snapshot.GetVector(propertyName)) {
                            yield return propertyName;
                        }
                        break;
                    case ShaderPropertyType.Float:
                    case ShaderPropertyType.Range:
                        if (!Mathf.Approximately(material.GetFloat(propertyName), snapshot.GetFloat(propertyName))) {
                            yield return propertyName;
                        }
                        break;
                    case ShaderPropertyType.Texture:
                        if (material.GetTexture(propertyName) != snapshot.GetTexture(propertyName)) {
                            yield return propertyName;
                        }
                        if (material.GetTextureOffset(propertyName) != snapshot.GetTextureOffset(propertyName)) {
                            yield return propertyName + "_Offset";
                        }
                        if (material.GetTextureScale(propertyName) != snapshot.GetTextureScale(propertyName)) {
                            yield return propertyName + "_Scale";
                        }
                        break;
                }
            }
        }

        private static void ReapplyAfterSave() {
            if (pendingSaveSnapshots == null) return;

            var snapshots = pendingSaveSnapshots;
            pendingSaveSnapshots = null;
            if (Application.isPlaying) {
                RestoreSnapshots(snapshots);
            }
            ClearSnapshots(ref snapshots);
        }

        private static void ClearSnapshots(ref Dictionary<Material, Material> snapshots) {
            if (snapshots == null) return;
            foreach (var snapshot in snapshots.Values.Where(snapshot => snapshot != null)) {
                UnityEngine.Object.DestroyImmediate(snapshot);
            }
            snapshots = null;
        }

        internal sealed class SaveHooks : UnityEditor.AssetModificationProcessor {
            private static string[] OnWillSaveAssets(string[] paths) {
                if (!Application.isPlaying) return paths;
                if (originalSnapshots == null || originalSnapshots.Count == 0) return paths;
                if (pendingSaveSnapshots != null) return paths;

                var pathSet = new HashSet<string>(paths.Where(path => !string.IsNullOrEmpty(path)));
                var materialsToRestore = originalSnapshots.Keys
                    .Where(mat => mat != null && pathSet.Contains(AssetDatabase.GetAssetPath(mat)))
                    .ToHashSet();
                if (materialsToRestore.Count == 0) return paths;

                Debug.Log(
                    "[VRCFury] Temporarily restoring tracked materials for asset save:\n" +
                    string.Join("\n", materialsToRestore.Select(mat => AssetDatabase.GetAssetPath(mat)))
                );

                pendingSaveSnapshots = SnapshotCurrentMaterials(materialsToRestore);
                RestoreSnapshots(originalSnapshots
                    .Where(pair => materialsToRestore.Contains(pair.Key))
                    .ToDictionary(pair => pair.Key, pair => pair.Value));
                EditorApplication.delayCall += ReapplyAfterSave;
                return paths;
            }
        }
    }
}
