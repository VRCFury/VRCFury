using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
using VF.Utils;

namespace VF.Inspector {
    internal static class SpsMaterialEditor {
        private const string FoldoutKey = "VF.SPS.MaterialProperties";

        [VFInit]
        private static void Init() => Editor.finishedDefaultHeaderGUI += DrawHeader;

        internal static bool ShouldDraw(Editor editor) => editor is MaterialEditor && editor.targets.Length > 0
            && editor.targets.All(target => target is Material material && material.HasProperty("_SPS_Bake"));

        internal static void DrawHeader(Editor editor) {
            if (!ShouldDraw(editor)) return;
            var materialEditor = (MaterialEditor)editor;

            using (new EditorGUILayout.VerticalScope(EditorStyles.inspectorDefaultMargins)) {
                var expanded = EditorGUILayout.Foldout(SessionState.GetBool(FoldoutKey, false), "SPS Properties", true);
                SessionState.SetBool(FoldoutKey, expanded);
                if (!expanded) return;

                using (new EditorGUI.IndentLevelScope()) {
                    EditorGUI.BeginChangeCheck();
                    foreach (var property in MaterialEditor.GetMaterialProperties(editor.targets)
                                 .Where(property => property.name.StartsWith("_SPS_", StringComparison.Ordinal))) {
                        materialEditor.ShaderProperty(property, property.displayName);
                    }
                    if (EditorGUI.EndChangeCheck()) materialEditor.PropertiesChanged();
                }
            }
        }
    }
}
