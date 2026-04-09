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
        public AnimationClip Build(ShaderInventoryAction model) {
            return MakeClip(model, 1);
        }
        public AnimationClip BuildOff(ShaderInventoryAction model) {
            return MakeClip(model, 0);
        }
        
        private AnimationClip MakeClip(ShaderInventoryAction model, float value) {
            var clip = NewClip();
            var renderer = model.renderer;
            if (renderer == null) return clip;
            var propertyName = $"material._InventoryItem{model.slot:D2}Animated";
            clip.SetCurve(renderer, propertyName, value);
            return clip;
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
