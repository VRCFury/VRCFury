using System;
using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;
using UnityEditor;
using UnityEngine;
using VF.Builder;
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

        private static bool ndmfPresent =
            ReflectionUtils.GetTypeFromAnyAssembly("nadena.dev.ndmf.AvatarProcessor") != null;

        public static string GetOutputString([CanBeNull] VFGameObject avatarObject = null) {
            var output = "";

            IEnumerable<VRCFury> vrcfComponents;
            if (avatarObject == null) {
                vrcfComponents = Object.FindObjectsOfType<VRCFury>();
            } else {
                vrcfComponents = avatarObject.GetComponentsInSelfAndChildren<VRCFury>();
            }

            var wdDisabled = vrcfComponents
                .SelectMany(c => c.GetAllFeatures())
                .OfType<FixWriteDefaults>()
                .Any(fwd => fwd.mode == FixWriteDefaults.FixWriteDefaultsMode.Disabled);
            if (wdDisabled) {
                output += "W";
            }
            
            if (!HapticsToggleMenuItem.Get()) {
                output += "H";
            }

            if (ndmfPresent) {
                output += "N";
            }

            if (!PlayModeMenuItem.Get()) {
                output += "P";
            }

            if (!ConstrainedProportionsMenuItem.Get()) {
                output += "C";
            }

            if (!string.IsNullOrEmpty(AssetDatabase.GUIDToAssetPath("0ad731f6b84696142a169af045691c7b"))
                || !string.IsNullOrEmpty(AssetDatabase.GUIDToAssetPath("ba7e30ad00ad0c247a3f4e816f1f7d53"))
                || !string.IsNullOrEmpty(AssetDatabase.GUIDToAssetPath("cc05f54cef1ff194fb23f8c1d552c492"))) {
                output += "B";
            }

            return output;
        }
    }
}
