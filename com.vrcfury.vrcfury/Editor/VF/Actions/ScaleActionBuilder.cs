using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using VF.Builder;
using VF.Feature.Base;
using VF.Inspector;
using VF.Model.StateAction;
using VF.Utils;

namespace VF.Actions {
    [FeatureTitle("Scale")]
    internal class ScaleActionBuilder : ActionBuilder<ScaleAction> {
        public AnimationClip Build(ScaleAction model) {
            var clip = NewClip();
            if (model.obj == null) return clip;
            var localScale = model.obj.asVf().localScale;
            var newScale = localScale * model.scale;
            clip.SetScale(model.obj, newScale);
            return clip;
        }

        [FeatureEditor]
        public static VisualElement Editor(SerializedProperty prop) {
            var row = new VisualElement();
            row.Add(VRCFuryEditorUtils.Prop(prop.FindPropertyRelative("obj"), "Object"));
            row.Add( VRCFuryEditorUtils.Prop(prop.FindPropertyRelative("scale"), "Multiplier"));
            return row;
        }
    }
}