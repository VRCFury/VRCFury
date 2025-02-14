using System;
using UnityEditor;
using UnityEngine;

namespace VF.Builder {
    internal static class MainWindowUtils {
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

            var containerWinType = typeof(EditorWindow).Assembly.GetType("UnityEditor.ContainerWindow");
            if (containerWinType == null)
                throw new Exception("Can't find internal type ContainerWindow. Maybe something has changed inside Unity");
            var showModeField = containerWinType.GetField("m_ShowMode", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var positionProperty = containerWinType.GetProperty("position", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
            if (showModeField == null || positionProperty == null)
                throw new Exception("Can't find internal fields 'm_ShowMode' or 'position'. Maybe something has changed inside Unity");
            var windows = Resources.FindObjectsOfTypeAll(containerWinType);
            foreach (var win in windows) {
                var showmode = (int)showModeField.GetValue(win);
                if (showmode == 4) // main window
                {
                    var pos = (Rect)positionProperty.GetValue(win, null);
                    return pos;
                }
            }
            throw new Exception("Can't find internal main window. Maybe something has changed inside Unity");
        }
    }
}
