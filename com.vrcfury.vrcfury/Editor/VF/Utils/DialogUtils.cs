using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using VF.Builder;
using VF.Builder.Exceptions;
using VF.Inspector;

namespace VF.Utils {
    internal static class DialogUtils {
        private static string FormatMessage(string message, Exception ex = null) {
            if (ex != null) {
                var exLine = "(";
                exLine += ex.GetBaseException().GetType().Name;
                var closestLine = VRCFExceptionUtils.GetClosestVrcfuryLine(ex);
                if (!string.IsNullOrWhiteSpace(closestLine)) exLine += " in " + closestLine;
                exLine += ")";
                message += $"\n\n{exLine}";
            }

            // Unity shows nothing in the dialog at all if it's >= 8000 characters
            if (message.Length > 7000) {
                message = message.Substring(0, 7000) + "...";
            }

            return message;
        }

        public static void DisplayDialog(string title, string message, string ok, Exception ex = null) {
            message = FormatMessage(message, ex);
            EditorUtility.DisplayDialog(title, message, ok);
        }
        
        public static bool DisplayDialog(string title, string message, string ok, string cancel) {
            message = FormatMessage(message);
            return EditorUtility.DisplayDialog(title, message, ok, cancel);
        }
        
        public static int DisplayDialogComplex(string title, string message, string ok, string cancel, string alt) {
            message = FormatMessage(message);
            return EditorUtility.DisplayDialogComplex(title, message, ok, cancel, alt);
        }

        private class MyModal : EditorWindow {
            public static MyModal Create(string title, string message, string ok) {
                var window = CreateInstance<MyModal>();
                window.titleContent = new GUIContent(title);
                var mainWindowPos = MainWindowUtils.GetEditorMainWindowPos();
                var size = new Vector2(500, 250);
                window.position = new Rect(mainWindowPos.xMin + (mainWindowPos.width - size.x) * 0.5f, mainWindowPos.yMin + 100, size.x, size.y);
                
                var root = window.rootVisualElement;

                var infoBox = new VisualElement().Padding(20);
                root.Add(infoBox);
            
                var label = new Label(message);
                infoBox.Add(label);
                label.AddToClassList("label");
                
                var debugLine = new Label(VrcfDebugLine.GetOutputString());
                root.Add(debugLine);
                debugLine.style.position = Position.Absolute;
                debugLine.style.right = 3;
                debugLine.style.bottom = -2;
                debugLine.style.opacity = 0.3f;
                debugLine.style.fontSize = 8;

                var buttons = new VisualElement().Row();
                buttons.Add(new VisualElement().FlexGrow(1));
                buttons.Add(new Button(() => {
                    window.Close();
                }) { text = ok });
                root.Add(buttons);

                window.ShowModal();
                return window;
            }
        }
    }
}
