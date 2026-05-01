using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using JetBrains.Annotations;
using UnityEditor;
using UnityEngine;
using VF.Exceptions;
using VF.Utils;
using Object = UnityEngine.Object;

namespace VF.Menu {
    internal static class ApplySuperSampledUiMaterialOverridesMenuItem {
        private const string MaterialName = "VRCSuperSampledUIMaterial";

        [MenuItem(MenuItems.applySuperSampledUiMaterialOverrides, priority = MenuItems.applySuperSampledUiMaterialOverridesPriority)]
        private static void Run() {
            VRCFExceptionUtils.ErrorDialogBoundary(() => {
                if (!DialogUtils.DisplayDialog(
                        "Apply VRCSuperSampledUIMaterial Overrides",
                        "This utility finds prefab overrides in loaded scenes where an object reference changed from null to VRCSuperSampledUIMaterial, applies them to the deepest prefab base, then removes redundant supersampled overrides.\n\nContinue?",
                        "Yes",
                        "Cancel"
                    )) return;
                var counts = ApplyInLoadedScenes();
                DialogUtils.DisplayDialog(
                    "Apply VRCSuperSampledUIMaterial Overrides",
                    $"Set {counts.prefabPropertiesSet} prefab propert{(counts.prefabPropertiesSet == 1 ? "y" : "ies")}, reverted {counts.nestedPrefabOverridesReverted} nested prefab override{(counts.nestedPrefabOverridesReverted == 1 ? "" : "s")}, reverted {counts.sceneOverridesReverted} scene override{(counts.sceneOverridesReverted == 1 ? "" : "s")}, saved {counts.prefabsSaved} prefab{(counts.prefabsSaved == 1 ? "" : "s")}.",
                    "Ok"
                );
            });
        }

        public class Result {
            public int prefabPropertiesSet;
            public int nestedPrefabOverridesReverted;
            public int sceneOverridesReverted;
            public int prefabsSaved;
        }

        public static Result ApplyInLoadedScenes() {
            var idCache = new Dictionary<Object, string>();
            var prefabPropertiesSet = 0;
            var nestedPrefabOverridesReverted = 0;
            var sceneOverridesReverted = 0;
            var prefabsSaved = 0;

            VRCFuryAssetDatabase.WithAssetEditing(() => {
                var ssMaterial = AssetDatabase.FindAssets($"{MaterialName} t:Material")
                    .Select(AssetDatabase.GUIDToAssetPath)
                    .Select(AssetDatabase.LoadAssetAtPath<Material>)
                    .FirstOrDefault(m => m != null && m.name == MaterialName);
                if (ssMaterial == null) {
                    Debug.LogWarning($"{MaterialName} not found");
                    return;
                }

                var actionsToTake = new Dictionary<(string path, string objectid, string propertyName), string>();
                void AddTask(UnityEngine.Component c, string propertyName, string task) {
                    var assetPath = AssetDatabase.GetAssetPath(c);
                    if (assetPath == null) return;
                    var id = Id(c);
                    if (id == null) return;
                    actionsToTake[(assetPath, id, propertyName)] = task;
                }

                foreach (var sceneComponent in VFGameObject.GetRoots().SelectMany(root => root.GetComponentsInSelfAndChildren())) {
                    if (!PrefabUtility.IsPartOfPrefabInstance(sceneComponent)) continue;
                    foreach (var sceneProp in new SerializedObject(sceneComponent).IterateFast()) {
                        if (sceneProp.propertyType != SerializedPropertyType.ObjectReference) continue;
                        if (sceneProp.objectReferenceValue != ssMaterial) continue;

                        UnityEngine.Component sourceToHoldSsMaterial = null;
                        var c = sceneComponent;
                        while (true) {
                            var source = PrefabUtility.GetCorrespondingObjectFromSource(c);
                            if (source == null) {
                                if (c != sceneComponent) sourceToHoldSsMaterial = c;
                                break;
                            } else {
                                var sourceProp = new SerializedObject(source).FindProperty(sceneProp.propertyPath);
                                if (sourceProp == null || sourceProp.propertyType != SerializedPropertyType.ObjectReference) break;
                                if (sourceProp.objectReferenceValue != ssMaterial && sourceProp.objectReferenceValue != null) break;
                                c = source;
                            }
                        }

                        if (sourceToHoldSsMaterial != null) {
                            AddTask(sourceToHoldSsMaterial, sceneProp.propertyPath, "set");
                            PrefabUtility.RevertPropertyOverride(sceneProp, InteractionMode.AutomatedAction);
                            sceneOverridesReverted++;
                        }
                    }
                }

                foreach (var singlePrefabActions in actionsToTake.GroupBy(pair => pair.Key.path)) {
                    var assetPath = singlePrefabActions.Key;
                    PrefabUtils.WithWritablePrefab(assetPath, root => {
                        var idsToComponents = root.GetComponentsInSelfAndChildren()
                            .Where(c => PrefabUtility.GetCorrespondingObjectFromSource(c) == null)
                            .Select(c => (Id(c), c))
                            .Where(pair => pair.Item1 != null)
                            .ToImmutableDictionary(pair => pair.Item1, pair => pair.Item2);
                        foreach (var action in singlePrefabActions) {
                            if (!idsToComponents.TryGetValue(action.Key.objectid, out var component)) continue;
                            var so = new SerializedObject(component);
                            var prop = so.FindProperty(action.Key.propertyName);
                            if (prop == null) continue;
                            var task = action.Value;
                            if (task == "revert") {
                                PrefabUtility.RevertPropertyOverride(prop, InteractionMode.AutomatedAction);
                                nestedPrefabOverridesReverted++;
                            } else if (task == "set") {
                                prop.objectReferenceValue = ssMaterial;
                                so.ApplyModifiedPropertiesWithoutUndo();
                                prefabPropertiesSet++;
                            }
                        }
                        prefabsSaved++;
                        return true;
                    });
                }
            });

            string Id(UnityEngine.Component obj) {
                if (idCache.TryGetValue(obj, out var id)) return id;
                var allComponents = obj.transform.root.asVf().GetComponentsInSelfAndChildren().Cast<Object>().ToArray();
                var ids = new GlobalObjectId[allComponents.Length];
                GlobalObjectId.GetGlobalObjectIdsSlow(allComponents, ids);
                foreach (var pair in allComponents.Zip(ids)) {
                    idCache[pair.Item1] = pair.Item2.assetGUID + "-" + pair.Item2.targetObjectId;
                }
                return idCache.GetValueOrDefault(obj);
            }

            return new Result {
                prefabPropertiesSet = prefabPropertiesSet,
                nestedPrefabOverridesReverted = nestedPrefabOverridesReverted,
                sceneOverridesReverted = sceneOverridesReverted,
                prefabsSaved = prefabsSaved
            };
        }
    }
}
