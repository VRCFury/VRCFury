using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using VF.Feature.Base;
using VF.Inspector;
using VF.Model.StateAction;
using VF.Utils;

namespace VF.Actions {
    [FeatureTitle("SCSS Shader Inventory")]
    internal class ScssShaderInventoryActionBuilder : ActionBuilder<ShaderInventoryAction> {
        public AnimationClip Build(ShaderInventoryAction model, AnimationClip offClip) {
            var onClip = NewClip();
            var renderer = model.renderer;
            if (renderer == null) return onClip;
            var propertyName = $"material._InventoryItem{model.slot:D2}Animated";
            offClip.SetCurve(renderer, propertyName, 0);
            onClip.SetCurve(renderer, propertyName, 1);
            return onClip;
        }

        [FeatureEditor]
        public static VisualElement Editor(SerializedProperty prop) {
            var output = new VisualElement();
            output.Add(VRCFuryEditorUtils.Prop(prop.FindPropertyRelative("renderer"), "Renderer"));
            output.Add(VRCFuryEditorUtils.Prop(prop.FindPropertyRelative("slot"), "Slot Number"));
            return output;
        }
    }
}
