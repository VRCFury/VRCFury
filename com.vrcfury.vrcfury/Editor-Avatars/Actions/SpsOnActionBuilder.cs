using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using VF.Feature.Base;
using VF.Inspector;
using VF.Model.StateAction;
using VF.Utils;

namespace VF.Actions {
    [FeatureTitle("Enable SPS")]
    [FeatureHideTitleInEditor]
    internal class SpsOnActionBuilder : ActionBuilder<SpsOnAction> {
        public AnimationClip Build(SpsOnAction model) {
            return MakeClip(model, true);
        }
        public AnimationClip BuildOff(SpsOnAction model) {
            return MakeClip(model, false);
        }

        private AnimationClip MakeClip(SpsOnAction model, bool enabled) {
            var clip = NewClip();
            if (model.target == null) {
                //Debug.LogWarning("Missing target in action: " + name);
                return clip;
            }
            clip.SetCurve(model.target, "spsAnimatedEnabled", enabled ? 1 : 0);
            return clip;
        }

        [FeatureEditor]
        public static VisualElement Editor(SerializedProperty prop) {
            var row = new VisualElement().Row();
            row.Add(VRCFuryActionDrawer.Title("Enable SPS").FlexBasis(100));
            row.Add(VRCFuryEditorUtils.Prop(prop.FindPropertyRelative("target")).FlexGrow(1));
            return row;
        }
    }
}