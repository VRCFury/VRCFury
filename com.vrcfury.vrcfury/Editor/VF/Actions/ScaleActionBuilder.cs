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
            return MakeClip(model);
        }
        public AnimationClip BuildOff(ScaleAction model) {
            return MakeClip(model, true);
        }

        private AnimationClip MakeClip(ScaleAction model, bool basis = false) {
            var clip = NewClip();
            if (model.obj == null) {
                //Debug.LogWarning("Missing object in action: " + name);
            } else {
                var localScale = model.obj.asVf().localScale;
                if (basis) {
                    clip.SetScale(model.obj, localScale);
                } else {
                    var newScale = localScale * model.scale;
                    clip.SetScale(model.obj, newScale);
                }
            }
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