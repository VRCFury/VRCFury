using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using VF.Exceptions;
using VF.Utils;

namespace VF.Menu {
    internal static class ApplySuperSampledUiMaterialOverridesMenuItem {
        private const string MaterialName = "VRCSuperSampledUIMaterial";

        [MenuItem(MenuItems.applySuperSampledUiMaterialOverrides, priority = MenuItems.applySuperSampledUiMaterialOverridesPriority)]
        private static void Run() {
            VRCFExceptionUtils.ErrorDialogBoundary(() => {
                if (!DialogUtils.DisplayDialog(
                        "Apply VRCSuperSampledUIMaterial Overrides",
                        "This utility finds prefab overrides in loaded scenes where an object reference changed from null to VRCSuperSampledUIMaterial, then applies each override down to the deepest prefab base.\n\nContinue?",
                        "Yes",
                        "Cancel"
                    )) return;

                var applyCount = 0;
                var propertyCount = 0;
                for (var sceneIndex = 0; sceneIndex < SceneManager.sceneCount; sceneIndex++) {
                    var scene = SceneManager.GetSceneAt(sceneIndex);
                    if (!scene.IsValid() || !scene.isLoaded) continue;

                    foreach (var root in scene.GetRootGameObjects()) {
                        foreach (var component in root.GetComponentsInChildren<UnityEngine.Component>(true)) {
                            if (component == null) continue;
                            propertyCount += ApplyMatchingOverrides(component, ref applyCount);
                        }
                    }
                }

                DialogUtils.DisplayDialog(
                    "Apply VRCSuperSampledUIMaterial Overrides",
                    $"Applied {propertyCount} override{(propertyCount == 1 ? "" : "s")} across {applyCount} prefab layer step{(applyCount == 1 ? "" : "s")}.",
                    "Ok"
                );
            });
        }

        [MenuItem(MenuItems.applySuperSampledUiMaterialOverrides, true)]
        private static bool Validate() {
            return Enumerable.Range(0, SceneManager.sceneCount)
                .Select(SceneManager.GetSceneAt)
                .Any(scene => scene.IsValid() && scene.isLoaded);
        }

        private static int ApplyMatchingOverrides(UnityEngine.Component component, ref int applyCount) {
            if (!PrefabUtility.IsPartOfPrefabInstance(component)) return 0;
            var applyPaths = new System.Collections.Generic.List<string>();

            foreach (var prop in new SerializedObject(component).IterateFast()) {
                if (!IsMatchingOverride(prop, component)) continue;
                applyPaths.Add(prop.propertyPath);
            }

            var propertyCount = 0;
            foreach (var path in applyPaths) {
                var steps = ApplyToDeepestBase(component, path);
                if (steps <= 0) continue;
                applyCount += steps;
                propertyCount++;
            }

            return propertyCount;
        }

        private static int ApplyToDeepestBase(UnityEngine.Component component, string propertyPath) {
            var steps = 0;
            var current = component;

            while (current != null && PrefabUtility.IsPartOfPrefabInstance(current)) {
                var sourceComponent = PrefabUtility.GetCorrespondingObjectFromSource(current);
                if (sourceComponent == null) break;

                var currentSo = new SerializedObject(current);
                var currentProp = currentSo.FindProperty(propertyPath);
                if (!IsMatchingOverride(currentProp, current)) break;

                var assetPath = AssetDatabase.GetAssetPath(sourceComponent);
                if (string.IsNullOrEmpty(assetPath)) break;

                PrefabUtility.ApplyPropertyOverride(currentProp, assetPath, InteractionMode.AutomatedAction);
                steps++;
                current = sourceComponent;
            }

            return steps;
        }

        private static bool IsMatchingOverride(SerializedProperty prop, UnityEngine.Component component) {
            if (prop == null) return false;
            if (!prop.prefabOverride) return false;
            if (prop.isDefaultOverride) return false;
            if (prop.propertyType != SerializedPropertyType.ObjectReference) return false;
            if (!(prop.GetObjectReferenceValueSafe() is Material mat)) return false;
            if (mat.name != MaterialName) return false;

            var sourceComponent = PrefabUtility.GetCorrespondingObjectFromSource(component);
            if (sourceComponent == null) return false;

            var sourceProp = new SerializedObject(sourceComponent).FindProperty(prop.propertyPath);
            if (sourceProp == null) return false;
            if (sourceProp.propertyType != SerializedPropertyType.ObjectReference) return false;
            if (sourceProp.GetObjectReferenceValueSafe() != null) return false;

            return true;
        }
    }
}
