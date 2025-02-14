using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using VF.Builder;
using VF.Feature.Base;
using VF.Inspector;
using VF.Model.StateAction;
using VF.Utils;

namespace VF.Actions {
    [FeatureTitle("Object Toggle")]
    [FeatureHideTitleInEditor]
    internal class ObjectToggleActionBuilder : ActionBuilder<ObjectToggleAction> {
        public AnimationClip Build(ObjectToggleAction toggle) {
            return MakeClip(toggle);
        }
        public AnimationClip BuildOff(ObjectToggleAction toggle) {
            return MakeClip(toggle, true);
        }

        private static AnimationClip MakeClip(ObjectToggleAction toggle, bool invert = false) {
            var clip = NewClip();
            if (toggle.obj == null) {
                //Debug.LogWarning("Missing object in action: " + name);
                return clip;
            }

            var state = true;
            if (toggle.mode == ObjectToggleAction.Mode.TurnOff) {
                state = false;
            } else if (toggle.mode == ObjectToggleAction.Mode.Toggle) {
                state = !toggle.obj.activeSelf;
            }

            if (invert) state = !state;
            
            clip.name = $"{(state ? "Turn On" : "Turn Off")} {toggle.obj.name}";

            clip.SetEnabled(toggle.obj, state);
            return clip;
        }

        [FeatureEditor]
        public static VisualElement Editor(SerializedProperty prop) {
            var row = new VisualElement().Row();

            row.Add(VRCFuryEditorUtils.Prop(
                prop.FindPropertyRelative("mode"),
                formatEnum: str => {
                    if (str == "Toggle") return "Flip State (Deprecated)";
                    return str;
                }
            ).FlexBasis(100));

            row.Add(VRCFuryEditorUtils.Prop(prop.FindPropertyRelative("obj")).FlexGrow(1));

            return row;
        }
    }
}
