using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using VF.Feature.Base;
using VF.Inspector;
using VF.Model.StateAction;
using VF.Utils;

namespace VF.Actions {
    [FeatureTitle("Object Toggle")]
    [FeatureHideTitleInEditor]
    internal class ObjectToggleActionBuilder : ActionBuilder<ObjectToggleAction> {
        public AnimationClip Build(ObjectToggleAction toggle, AnimationClip offClip) {
            var onClip = NewClip();
            if (toggle.obj == null) {
                //Debug.LogWarning("Missing object in action: " + name);
                return onClip;
            }

            var onState = true;
            if (toggle.mode == ObjectToggleAction.Mode.TurnOff) {
                onState = false;
            } else if (toggle.mode == ObjectToggleAction.Mode.Toggle) {
                onState = !toggle.obj.activeSelf;
            }
            
            onClip.name = $"{(onState ? "Turn On" : "Turn Off")} {toggle.obj.name}";

            offClip.SetEnabled(toggle.obj, !onState);
            onClip.SetEnabled(toggle.obj, onState);
            return onClip;
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
