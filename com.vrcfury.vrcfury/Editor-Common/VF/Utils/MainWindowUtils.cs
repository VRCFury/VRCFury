using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using VF.Utils;

namespace VF.Builder {
    internal static class MainWindowUtils {
        private abstract class Reflection : ReflectionHelper {
            public static readonly Type ContainerWindowType =
                typeof(EditorWindow).Assembly.GetType("UnityEditor.ContainerWindow");
            public static readonly FieldInfo ShowModeField = ContainerWindowType?.VFField("m_ShowMode");
            public static readonly PropertyInfo PositionProperty = ContainerWindowType?.VFProperty("position");
        }

        public static Rect GetEditorMainWindowPos() {
#if UNITY_2020_1_OR_NEWER
            return EditorGUIUtility.GetMainWindowPosition();
#else
            return LegacyGetEditorMainWindowPos();
#endif
        }

        public static Rect LegacyGetEditorMainWindowPos() {
            if (Application.isBatchMode) {
                return Rect.zero;
            }

            if (!ReflectionHelper.IsReady<Reflection>()) {
                throw new Exception("Couldn't find unity internals to calculate window position");
            }

            var windows = Resources.FindObjectsOfTypeAll(Reflection.ContainerWindowType);
            foreach (var win in windows) {
                var showmode = (int)Reflection.ShowModeField.GetValue(win);
                if (showmode == 4) // main window
                {
                    var pos = (Rect)Reflection.PositionProperty.GetValue(win, null);
                    return pos;
                }
            }
            throw new Exception("Can't find internal main window. Maybe something has changed inside Unity");
        }
    }
}
