using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using VF.Builder.Haptics;
using VF.Utils;

namespace VF.Menu {
    internal class ConstrainedProportionsMenuItem : UnityEditor.AssetModificationProcessor {
        private const string EditorPref = "com.vrcfury.constrainedProportions";

        private static Action reset;

        private static void Reset() {
            try {
                reset?.Invoke();
            } catch (Exception) {
            }

            reset = null;
        }

        [InitializeOnLoadMethod]
        private static void Init() {
            EditorApplication.delayCall += UpdateMenu;
            AssemblyReloadEvents.beforeAssemblyReload += OnBeforeAssemblyReload;
            Selection.selectionChanged += () => {
                Reset();
                if (!Get()) return;

                foreach (var t in Selection.transforms) {
                    FixTransform(t);
                }
            };
        }
        
        private static void OnBeforeAssemblyReload() {
            Reset();
        }
        
        private static void FixTransform(Transform transform) {
            var so = new SerializedObject(transform);
            var prop = so.FindProperty("m_ConstrainProportionsScale");
            if (prop == null || prop.propertyType != SerializedPropertyType.Boolean) return;
            var oldValue = prop.boolValue;
            var newValue = !HapticUtils.IsNonUniformScale(transform);
            if (oldValue == newValue) return;

            var shouldReset = prop.isInstantiatedPrefab && !prop.prefabOverride && !Application.isPlaying;
            prop.boolValue = newValue;
            prop.serializedObject.ApplyModifiedPropertiesWithoutUndo();

            if (shouldReset) {
                reset += () => {
                    PrefabUtility.RevertPropertyOverride(new SerializedObject(prop.serializedObject.targetObject).FindProperty(prop.propertyPath), InteractionMode.AutomatedAction);
                };
            }
        }

        public static bool Get() {
            return EditorPrefs.GetBool(EditorPref, true);
        }
        private static void UpdateMenu() {
            UnityEditor.Menu.SetChecked(MenuItems.constrainedProportions, Get());
        }

        [MenuItem(MenuItems.constrainedProportions, priority = MenuItems.constrainedProportionsPriority)]
        private static void Click() {
            if (Get()) {
                var ok = DialogUtils.DisplayDialog(
                    "Warning",
                    "Without Constrained Proportions, it's easy to accidentally scale objects non-uniformly," +
                    " introducing what is called 'Shear' in unity. This can make bones stretch different amounts depending on which" +
                    " direction they are rotated. Are you sure you want to continue?",
                    "Yes, stop enabling Constrained Proportions",
                    "Cancel"
                );
                if (!ok) return;
            }
            EditorPrefs.SetBool(EditorPref, !Get());
            UpdateMenu();
        }
        
        static string[] OnWillSaveAssets(string[] paths) {
            Reset();
            return paths;
        }
    }
}
