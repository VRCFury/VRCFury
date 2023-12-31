using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
using VF.Menu;
using VF.Model;
using VF.Model.Feature;
using Object = UnityEngine.Object;

namespace VF.Inspector {
    [InitializeOnLoad]
    public class SceneViewOverlay {
        static SceneViewOverlay() {
            SceneView.duringSceneGui += DuringSceneGui;
        }

        private static void DuringSceneGui(SceneView view) {
            Handles.BeginGUI();
            try {
                DuringSceneGuiUnsafe(view);
            } catch (Exception e) {
                Debug.LogWarning(e);
            }
            Handles.EndGUI();
        }

        private static void DuringSceneGuiUnsafe(SceneView view) {
            var output = GetOutputString();

            var bak = GUI.contentColor;
            try {
                GUILayout.BeginArea(new Rect(0, view.position.height-42, 100, 30));
                GUI.contentColor = new Color(1, 1, 1, 0.1f);
                if (!string.IsNullOrWhiteSpace(output)) {
                    GUILayout.Label(output);
                }
                GUILayout.EndArea();
            } finally {
                GUI.contentColor = bak;
            }
        }

        public static string GetOutputString() {
            var output = "";
            
            var wdDisabled = Object
                .FindObjectsOfType<VRCFury>()
                .SelectMany(c => c.config.features)
                .OfType<FixWriteDefaults>()
                .Any(fwd => fwd.mode == FixWriteDefaults.FixWriteDefaultsMode.Disabled);
            if (wdDisabled) {
                output += "W";
            }
            
            if (!HapticsToggleMenuItem.Get()) {
                output += "H";
            }

            if (!NdmfFirstMenuItem.Get()) {
                output += "N";
            }

            if (!PlayModeMenuItem.Get()) {
                output += "P";
            }

            if (!ConstrainedProportionsMenuItem.Get()) {
                output += "C";
            }

            return output;
        }
    }
}
