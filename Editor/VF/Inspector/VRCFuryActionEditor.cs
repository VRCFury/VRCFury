using UnityEditor;
using UnityEngine.UIElements;
using VF.Model;

namespace VF.Inspector {

[CustomPropertyDrawer(typeof(VF.Model.StateAction.Action))]
public class VRCFuryActionDrawer : PropertyDrawer {
    public override VisualElement CreatePropertyGUI(SerializedProperty prop) {

        if (prop.FindPropertyRelative("obj") != null) {
            var row = new VisualElement {
                style = {
                    flexDirection = FlexDirection.Row,
                    alignItems = Align.FlexStart
                }
            };

            var label = new Label("Object Toggle") {
                style = {
                    flexGrow = 0,
                    flexBasis = VRCFuryEditorUtils.LABEL_WIDTH
                }
            };
            row.Add(label);

            var propField = VRCFuryEditorUtils.PropWithoutLabel(prop.FindPropertyRelative("obj"));
            propField.style.flexGrow = 1;
            row.Add(propField);

            return row;
        }

        if (prop.FindPropertyRelative("blendShape") != null) {
            var row = new VisualElement {
                style = {
                    flexDirection = FlexDirection.Row,
                    alignItems = Align.FlexStart
                }
            };

            var label = new Label {
                text = "BlendShape",
                style = {
                    flexGrow = 0,
                    flexBasis = VRCFuryEditorUtils.LABEL_WIDTH
                }
            };
            row.Add(label);

            var propField = VRCFuryEditorUtils.PropWithoutLabel(prop.FindPropertyRelative("blendShape"));
            propField.style.flexGrow = 1;
            row.Add(propField);

            return row;
        }
        
        if (prop.FindPropertyRelative("clip") != null) {
            var row = new VisualElement {
                style = {
                    flexDirection = FlexDirection.Row,
                    alignItems = Align.FlexStart
                }
            };

            var label = new Label {
                text = "Animation Clip",
                style = {
                    flexGrow = 0,
                    flexBasis = VRCFuryEditorUtils.LABEL_WIDTH
                }
            };
            row.Add(label);

            var propField = VRCFuryEditorUtils.PropWithoutLabel(prop.FindPropertyRelative("clip"));
            propField.style.flexGrow = 1;
            row.Add(propField);

            return row;
        }

        return new Label("Unknown action");
    }
}

}
