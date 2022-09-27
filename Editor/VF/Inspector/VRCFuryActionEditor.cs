using UnityEditor;
using UnityEngine.UIElements;
using VF.Model;

namespace VF.Inspector {

[CustomPropertyDrawer(typeof(VF.Model.StateAction.Action))]
public class VRCFuryActionDrawer : PropertyDrawer {
    public override VisualElement CreatePropertyGUI(SerializedProperty prop) {
        return Render(prop);
    }

    public static VisualElement Render(SerializedProperty prop, bool singleLine = false) {

        if (prop.FindPropertyRelative("frame") != null) {
            var row = new VisualElement {
                style = {
                    flexDirection = FlexDirection.Row,
                    alignItems = Align.FlexStart
                }
            };

            if (!singleLine) {
                var label = new Label("Flipbook Frame") {
                    style = {
                        flexGrow = 0,
                        flexBasis = 100
                    }
                };
                row.Add(label);
            }

            var propField = VRCFuryEditorUtils.PropWithoutLabel(prop.FindPropertyRelative("obj"));
            propField.style.flexGrow = 1;
            row.Add(propField);
            
            var propField3 = VRCFuryEditorUtils.PropWithoutLabel(prop.FindPropertyRelative("frame"));
            propField3.style.flexGrow = 0;
            propField3.style.flexBasis = 30;
            row.Add(propField3);

            return row;
        }

        if (prop.FindPropertyRelative("obj") != null) {
            if (singleLine) return null;
            var row = new VisualElement {
                style = {
                    flexDirection = FlexDirection.Row,
                    alignItems = Align.FlexStart
                }
            };

            var label = new Label("Object Toggle") {
                style = {
                    flexGrow = 0,
                    flexBasis = 100
                }
            };
            row.Add(label);

            var propField = VRCFuryEditorUtils.PropWithoutLabel(prop.FindPropertyRelative("obj"));
            propField.style.flexGrow = 1;
            row.Add(propField);

            return row;
        }

        if (prop.FindPropertyRelative("blendShape") != null) {
            if (singleLine) return null;
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
                    flexBasis = 100
                }
            };
            row.Add(label);

            var propField = VRCFuryEditorUtils.PropWithoutLabel(prop.FindPropertyRelative("blendShape"));
            propField.style.flexGrow = 1;
            row.Add(propField);
            
            var valueField = VRCFuryEditorUtils.PropWithoutLabel(prop.FindPropertyRelative("blendShapeValue"));
            valueField.style.flexGrow = 0;
            valueField.style.flexBasis = 50;
            row.Add(valueField);

            return row;
        }
        
        if (prop.FindPropertyRelative("clip") != null) {
            var row = new VisualElement {
                style = {
                    flexDirection = FlexDirection.Row,
                    alignItems = Align.FlexStart
                }
            };

            if (!singleLine) {
                var label = new Label {
                    text = "Animation Clip",
                    style = {
                        flexGrow = 0,
                        flexBasis = 100
                    }
                };
                row.Add(label);
            }

            var propField = VRCFuryEditorUtils.PropWithoutLabel(prop.FindPropertyRelative("clip"));
            propField.style.flexGrow = 1;
            row.Add(propField);

            return row;
        }

        return new Label("Unknown action");
    }
}

}
