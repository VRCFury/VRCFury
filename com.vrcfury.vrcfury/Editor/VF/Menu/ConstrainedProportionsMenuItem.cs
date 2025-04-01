using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using VF.Builder.Haptics;
using VF.Utils;
using Object = UnityEngine.Object;

namespace VF.Menu {
    internal class ConstrainedProportionsMenuItem {
        private const string EditorPref = "com.vrcfury.constrainedProportions";
        
        private static readonly HashSet<Transform> unlocked = new HashSet<Transform>();

        private static readonly PropertyInfo constrainProportionsScale = typeof(Transform)
            .GetProperty("constrainProportionsScale",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

        [InitializeOnLoadMethod]
        private static void Init() {
            if (constrainProportionsScale == null) {
                Debug.LogWarning("Failed to find constrainProportionsScale");
                return;
            }

            HarmonyUtils.Patch(
                typeof(PrefixClass),
                nameof(PrefixClass.DoAllGOsHaveConstrainProportionsEnabled),
                typeof(Selection),
                "DoAllGOsHaveConstrainProportionsEnabled",
                internalReplacementClass: typeof(ReplacementClass)
            );
            HarmonyUtils.Patch(
                typeof(PrefixClass),
                nameof(PrefixClass.SetConstrainProportions),
                "UnityEditor.ConstrainProportionsTransformScale",
                "SetConstrainProportions"
            );
            EditorApplication.delayCall += UpdateMenu;
            Selection.selectionChanged += () => unlocked.Clear();
        }

        private static IEnumerable<Transform> Transforms(IEnumerable<Object> objs) {
            return objs.Select(o => {
                if (o is GameObject go) return go.transform;
                if (o is Transform t) return t;
                return null;
            }).NotNull();
        }

        private static bool ShouldForceLock(UnityEngine.Object[] targetObjects) {
            unlocked.UnionWith(Transforms(targetObjects).Where(t => HapticUtils.IsNonUniformScale(t)));
            return Get() && Transforms(targetObjects).All(t => !unlocked.Contains(t));
        }

        public static class PrefixClass {
            public static bool DoAllGOsHaveConstrainProportionsEnabled(UnityEngine.Object[] __0, ref bool __result) {
                if (ShouldForceLock(__0)) {
                    __result = true;
                    return false;
                }
                return true;
            }
            public static void SetConstrainProportions(UnityEngine.Object[] __0) {
                unlocked.UnionWith(Transforms(__0));
            }
        }
        public static class ReplacementClass {
            public static bool DoAllGOsHaveConstrainProportionsEnabled(UnityEngine.Object[] targetObjects) {
                if (ShouldForceLock(targetObjects)) return true;
                // OG Behaviour
                return Transforms(targetObjects).All(t => (bool)constrainProportionsScale.GetValue(t));
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
    }
}
