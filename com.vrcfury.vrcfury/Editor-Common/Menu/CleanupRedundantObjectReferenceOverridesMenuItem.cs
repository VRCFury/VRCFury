using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using VF.Exceptions;
using VF.Utils;

namespace VF.Menu {
    internal static class CleanupRedundantObjectReferenceOverridesMenuItem {
        [MenuItem(MenuItems.cleanupRedundantObjectReferenceOverrides, priority = MenuItems.cleanupRedundantObjectReferenceOverridesPriority)]
        private static void Run() {
            VRCFExceptionUtils.ErrorDialogBoundary(() => {
                var roots = GetSelectedRoots();
                if (roots.Count == 0) return;

                if (!DialogUtils.DisplayDialog(
                        "Cleanup Redundant Object Reference Overrides",
                        "This utility will revert prefab overrides in the selected hierarchy when the current value already matches the source prefab value.\n\nContinue?",
                        "Yes",
                        "Cancel"
                    )) return;

                var reverted = 0;
                foreach (var root in roots) {
                    reverted += Cleanup(root);
                }

                DialogUtils.DisplayDialog(
                    "Cleanup Redundant Object Reference Overrides",
                    $"Reverted {reverted} redundant object reference override{(reverted == 1 ? "" : "s")}.",
                    "Ok"
                );
            });
        }

        [MenuItem(MenuItems.cleanupRedundantObjectReferenceOverrides, true)]
        private static bool Validate() {
            return Selection.gameObjects.Any(go => go != null);
        }

        private static List<VFGameObject> GetSelectedRoots() {
            var selected = Selection.gameObjects
                .Where(go => go != null)
                .Select(go => go.asVf())
                .Distinct()
                .ToList();

            return selected
                .Where(go => !selected.Any(other => other != go && go.IsChildOf(other)))
                .ToList();
        }

        private static int Cleanup(VFGameObject root) {
            var reverted = 0;
            foreach (var obj in root.GetSelfAndAllChildren()) {
                foreach (var component in obj.GetComponents<UnityEngine.Component>()) {
                    if (component == null) continue;
                    reverted += Cleanup(component);
                }
            }
            return reverted;
        }

        private static int Cleanup(UnityEngine.Component component) {
            if (!PrefabUtility.IsPartOfPrefabInstance(component)) return 0;

            var sourceComponent = PrefabUtility.GetCorrespondingObjectFromSource(component);
            if (sourceComponent == null) return 0;

            var so = new SerializedObject(component);
            var sourceSo = new SerializedObject(sourceComponent);
            var revertPaths = new List<string>();

            foreach (var prop in so.IterateFast()) {
                if (!prop.prefabOverride) continue;
                if (prop.isDefaultOverride) continue;

                var sourceProp = sourceSo.FindProperty(prop.propertyPath);
                if (sourceProp == null) continue;
                if (sourceProp.propertyType != prop.propertyType) continue;
                if (!ValuesMatchUpstream(prop, sourceProp)) continue;

                revertPaths.Add(prop.propertyPath);
            }

            var reverted = 0;
            foreach (var path in revertPaths) {
                var revertSo = new SerializedObject(component);
                var revertProp = revertSo.FindProperty(path);
                if (revertProp == null) continue;
                if (!revertProp.prefabOverride) continue;
                if (revertProp.isDefaultOverride) continue;
                PrefabUtility.RevertPropertyOverride(revertProp, InteractionMode.AutomatedAction);
                reverted++;
            }

            return reverted;
        }

        private static bool ValuesMatchUpstream(SerializedProperty instanceProp, SerializedProperty sourceProp) {
            switch (instanceProp.propertyType) {
                case SerializedPropertyType.ObjectReference:
                    return ReferencesMatchUpstream(
                        instanceProp.GetObjectReferenceValueSafe(),
                        sourceProp.GetObjectReferenceValueSafe()
                    );
                case SerializedPropertyType.Integer:
                    return instanceProp.intValue == sourceProp.intValue;
                case SerializedPropertyType.Boolean:
                    return instanceProp.boolValue == sourceProp.boolValue;
                case SerializedPropertyType.Float:
                    return FloatsMatch(instanceProp.floatValue, sourceProp.floatValue);
                case SerializedPropertyType.Enum:
                    return instanceProp.enumValueIndex == sourceProp.enumValueIndex;
                case SerializedPropertyType.String:
                    return instanceProp.stringValue == sourceProp.stringValue;
                default:
                    return false;
            }
        }

        private static bool ReferencesMatchUpstream(Object instanceValue, Object sourceValue) {
            if (instanceValue == sourceValue) return true;
            if (instanceValue == null || sourceValue == null) return instanceValue == sourceValue;

            var instanceSource = PrefabUtility.GetCorrespondingObjectFromSource(instanceValue);
            if (instanceSource == sourceValue) return true;

            return false;
        }

        private static bool FloatsMatch(float a, float b) {
            var diff = Mathf.Abs(a - b);
            if (diff < 0.001f) return true;

            var largest = Mathf.Max(Mathf.Abs(a), Mathf.Abs(b));
            if (largest < 0.001f) return true;

            return diff / largest < 0.001f;
        }
    }
}
