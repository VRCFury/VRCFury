using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using VF.Builder.Haptics;

namespace VF.Menu {
    [InitializeOnLoad]
    public class ConstrainedProportionsMenuItem {
        private const string EditorPref = "com.vrcfury.constrainedProportions";

        static ConstrainedProportionsMenuItem() {
            EditorApplication.delayCall += UpdateMenu;
            Selection.selectionChanged += () => {
                if (!Get()) return;
                var method = typeof(Transform).GetMethod(
                    "SetConstrainProportionsScale",
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance,
                    null,
                    new Type[] { typeof(bool) },
                    null
                );
                if (method == null) return;
                foreach (var t in Selection.transforms) {
                    method.Invoke(t, new object[] { !HapticUtils.IsNonUniformScale(t) });
                }
            };
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
                var ok = EditorUtility.DisplayDialog(
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
