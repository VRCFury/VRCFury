using UnityEditor;
using UnityEngine.UIElements;
using VRCF.Model;

namespace VRCF.Inspector {

[CustomPropertyDrawer(typeof(VRCFuryAction))]
public class VRCFuryActionDrawer : PropertyDrawer {
    public override VisualElement CreatePropertyGUI(SerializedProperty prop) {
        var typeProp = prop.FindPropertyRelative("type");

        return VRCFuryEditorUtils.RefreshOnChange(() => {
            var type = typeProp.stringValue;
            if (type == VRCFuryAction.TOGGLE) {
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

            if (type == VRCFuryAction.BLENDSHAPE) {
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

            return new Label("Unknown action: " + type);
        }, typeProp);
    }
}

}
