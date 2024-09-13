using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using VF.Feature.Base;
using VF.Inspector;
using VF.Model.StateAction;
using VF.Utils;

namespace VF.Actions {
    [FeatureTitle("Poiyomi UV Tile")]
    internal class PoiyomiUVTileActionBuilder : ActionBuilder<PoiyomiUVTileAction> {
        public AnimationClip Build(PoiyomiUVTileAction model, AnimationClip offClip) {
            var onClip = NewClip();
            var renderer = model.renderer;
            if (model.row > 3 || model.row < 0 || model.column > 3 || model.column < 0) {
                throw new ArgumentException("Poiyomi UV Tiles are ranges between 0-3, check if slots are within these ranges.");
            }
            if (renderer != null) {
                var propertyName = model.dissolve ? "_UVTileDissolveAlpha_Row" : "_UDIMDiscardRow";
                propertyName += $"{model.row}_{(model.column)}";
                if (model.renamedMaterial != "")
                    propertyName += $"_{model.renamedMaterial}";
                offClip.SetCurve(renderer, $"material.{propertyName}", 1);
                onClip.SetCurve(renderer, $"material.{propertyName}", 0);
            }
            return onClip;
        }

        [FeatureEditor]
        public static VisualElement Editor(SerializedProperty prop) {
            var content = new VisualElement();

            content.Add(VRCFuryEditorUtils.Prop(prop.FindPropertyRelative("renderer"), "Renderer"));
            content.Add(VRCFuryEditorUtils.Prop(prop.FindPropertyRelative("row"), "Row (0-3)"));
            content.Add(VRCFuryEditorUtils.Prop(prop.FindPropertyRelative("column"), "Column (0-3)"));

            var adv = new Foldout {
                text = "Advanced UV Tile Options",
                value = false
            };
            adv.Add(VRCFuryEditorUtils.Prop(prop.FindPropertyRelative("dissolve"), "Use UV Tile Dissolve"));
            adv.Add(VRCFuryEditorUtils.Prop(prop.FindPropertyRelative("renamedMaterial"), "Renamed Material", tooltip: "Material suffix when using poiyomi renamed properties"));
            content.Add(adv);
            return content;
        }
    }
}