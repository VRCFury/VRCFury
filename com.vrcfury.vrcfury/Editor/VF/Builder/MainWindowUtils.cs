using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace VF.Builder {
    public static class MainWindowUtils {
        // https://discussions.unity.com/t/editor-window-how-to-center-a-window/137800/2
        // Temporary fix til unity 2020
        private static System.Type[] GetAllDerivedTypes(System.AppDomain aAppDomain, System.Type aType)
        {
            var assemblies = aAppDomain.GetAssemblies();
            return (from assembly in assemblies from type in assembly.GetTypes() where type.IsSubclassOf(aType) select type).ToArray();
        }

        public static Rect GetEditorMainWindowPos()
        {
            var containerWinType = GetAllDerivedTypes(AppDomain.CurrentDomain, typeof(ScriptableObject))
                .FirstOrDefault(t => t.Name == "ContainerWindow");
            if (containerWinType == null)
                throw new System.MissingMemberException("Can't find internal type ContainerWindow. Maybe something has changed inside Unity");
            var showModeField = containerWinType.GetField("m_ShowMode", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var positionProperty = containerWinType.GetProperty("position", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
            if (showModeField == null || positionProperty == null)
                throw new System.MissingFieldException("Can't find internal fields 'm_ShowMode' or 'position'. Maybe something has changed inside Unity");
            var windows = Resources.FindObjectsOfTypeAll(containerWinType);
            foreach (var win in windows)
            {
                var showmode = (int)showModeField.GetValue(win);
                if (showmode != 4) continue; // main window
                var pos = (Rect)positionProperty.GetValue(win, null);
                return pos;
            }
            throw new System.NotSupportedException("Can't find internal main window. Maybe something has changed inside Unity");
        }
    }
}