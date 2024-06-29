using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
using VF.Builder;
using VF.Builder.Exceptions;

namespace VF.Menu {
    internal static class RemoveUselessOverridesMenuItem {
        [MenuItem(MenuItems.uselessOverrides, priority = MenuItems.uselessOverridesPriority)]
        private static void Run() {
            VRCFExceptionUtils.ErrorDialogBoundary(() => {
                Run(MenuUtils.GetSelectedAvatar());
            });
        }
        [MenuItem(MenuItems.uselessOverrides, true)]
        private static bool Check() {
            return MenuUtils.GetSelectedAvatar() != null;
        }
        
        private static void Run(VFGameObject avatarObj) {
            if (!EditorUtility.DisplayDialog(
                    "Useless Override Cleanup",
                    "This utility will remove all useless prefab overrides from the selected avatar." +
                    "These are overrides where the override value is identical to the base value, and may be useful to cleanup" +
                    " overrides after using unity's Reconnect Prefab feature." +
                    "\n\nContinue?",
                    "Yes",
                    "Cancel"
                )) return;

            foreach (var obj in avatarObj.GetSelfAndAllChildren()) {
                Cleanup(obj);
            }
        }

        private static void Cleanup(VFGameObject obj) {
            if (!PrefabUtility.IsOutermostPrefabInstanceRoot(obj)) return;
            Debug.Log(obj.GetPath());
            var overrides = PrefabUtility.GetPropertyModifications(obj);
            PrefabUtility.SetPropertyModifications(obj, overrides.Where(ShouldKeepModification).ToArray());
        }

        private static bool ShouldKeepModification(PropertyModification o) {
            var component = o.target as UnityEngine.Component;
            if (component == null) return true;
            var so = new SerializedObject(component);
            var originalProp = so.FindProperty(o.propertyPath);
            if (originalProp == null || originalProp.propertyType != SerializedPropertyType.Float) return true;
            var originalValue = originalProp.floatValue;
            var overrideValueStr = o.value;
            if (!float.TryParse(overrideValueStr, out var overrideValue)) return true;

            if (IsClose(originalValue, overrideValue, o.propertyPath.Contains("Scale"))) {
                return false;
            }

            //Debug.Log(component.owner().GetPath() + " " + o.propertyPath + " " + originalValue + " -> " + overrideValue + " " + (Math.Abs((originalValue - overrideValue) / originalValue)));
            return true;
        }

        private static bool IsClose(float a, float b, bool isScale) {
            if (!isScale) {
                if (Math.Abs(a - b) < 0.001) return true;
            }
            if (a == 0 || b == 0) return a == b;
            return Math.Abs((a - b) / a) < 0.001;
        }
    }
}
