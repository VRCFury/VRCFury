using System;
using System.Reflection;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using VF.Builder;
using VF.Inspector;

namespace VF {
    [InitializeOnLoad]
    public class VRCFProgressWindow : EditorWindow {
        static VRCFProgressWindow() {
            EditorApplication.delayCall += () => {
                foreach (var w in Resources.FindObjectsOfTypeAll<VRCFProgressWindow>()) {
                    if (w) w.Close();
                }
            };
        }
        
        public static VRCFProgressWindow Create() {
            var window = CreateInstance<VRCFProgressWindow>();
            var mainWindowPos = MainWindowUtils.GetEditorMainWindowPos();
            var size = new Vector2(500, 250);
            window.position = new Rect(mainWindowPos.xMin + (mainWindowPos.width - size.x) * 0.5f, mainWindowPos.yMin + 100, size.x, size.y);
            window.ShowPopup();
            return window;
        }
        
        private Label label;
        private ProgressBar progress;
        
        public void OnEnable() {
            var root = rootVisualElement;
            root.AddToClassList("VRCFProgressWindow");
            root.styleSheets.Add(VRCFuryEditorUtils.GetResource<StyleSheet>("VRCFuryStyle.uss"));

            var infoBox = new VisualElement();
            root.Add(infoBox);
            VRCFuryEditorUtils.Padding(infoBox, 20);
            
            progress = new ProgressBar
            {
                value = 50
            };
            infoBox.Add(progress);
            
            label = new Label("");
            infoBox.Add(label);
            label.AddToClassList("label");

            var logo = new Image
            {
                image = VRCFuryEditorUtils.GetResource<Texture>("logo.png"),
                scaleMode = ScaleMode.ScaleToFit
            };
            root.Add(logo);
        }

        public void Progress(float current, string info) {
            var percent = Math.Round(current * 100);
            Debug.Log($"Progress ({percent}%): {info}");
            label.text = info;
            progress.value = current * 100;
            progress.title = $"{percent}%";
            RepaintNow();
        }

        private void RepaintNow() {
            GetType().GetMethod("RepaintImmediately", BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic)?.Invoke(this, new object[]{});
        }
    }
}
