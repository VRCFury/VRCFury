using UnityEditor;
using UnityEngine.UIElements;
using VF.Model;

namespace VF.Inspector {

[CustomPropertyDrawer(typeof(VF.Model.StateAction.Action))]
public class VRCFuryActionDrawer : PropertyDrawer {
    public override VisualElement CreatePropertyGUI(SerializedProperty prop) {
        return Render(prop);
    }

    public static VisualElement Render(SerializedProperty prop) {
        
        if (prop.FindPropertyRelative("mat") != null) {
            var row = new VisualElement {
                style = {
                    flexDirection = FlexDirection.Row,
                    alignItems = Align.FlexStart
                }
            };

            var label = new Label("Material") {
                style = {
                    flexGrow = 0,
                    flexBasis = 100
                }
            };
            row.Add(label);

            var propField = VRCFuryEditorUtils.Prop(prop.FindPropertyRelative("obj"));
            propField.style.flexGrow = 1;
            row.Add(propField);
            
            var propField2 = VRCFuryEditorUtils.Prop(prop.FindPropertyRelative("materialIndex"));
            propField2.style.flexGrow = 0;
            propField2.style.flexBasis = 50;
            row.Add(propField2);
            
            var propField3 = VRCFuryEditorUtils.Prop(prop.FindPropertyRelative("mat"));
            propField3.style.flexGrow = 1;
            row.Add(propField3);

            return row;
        }

        if (prop.FindPropertyRelative("frame") != null) {
            var row = new VisualElement {
                style = {
                    flexDirection = FlexDirection.Row,
                    alignItems = Align.FlexStart
                }
            };

            var label = new Label("Flipbook Frame") {
                style = {
                    flexGrow = 0,
                    flexBasis = 100
                }
            };
            row.Add(label);

            var propField = VRCFuryEditorUtils.Prop(prop.FindPropertyRelative("obj"));
            propField.style.flexGrow = 1;
            row.Add(propField);
            
            var propField3 = VRCFuryEditorUtils.Prop(prop.FindPropertyRelative("frame"));
            propField3.style.flexGrow = 0;
            propField3.style.flexBasis = 30;
            row.Add(propField3);

            return row;
        }
        
        if (prop.FindPropertyRelative("scale") != null) {
            var row = new VisualElement {
                style = {
                    flexDirection = FlexDirection.Row,
                    alignItems = Align.FlexStart
                }
            };

            var label = new Label("Scale") {
                style = {
                    flexGrow = 0,
                    flexBasis = 100
                }
            };
            row.Add(label);

            var propField = VRCFuryEditorUtils.Prop(prop.FindPropertyRelative("obj"));
            propField.style.flexGrow = 1;
            row.Add(propField);
            
            var propField3 = VRCFuryEditorUtils.Prop(prop.FindPropertyRelative("scale"));
            propField3.style.flexGrow = 0;
            propField3.style.flexBasis = 50;
            row.Add(propField3);

            return row;
        }

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
                    flexBasis = 100
                }
            };
            row.Add(label);

            var propField = VRCFuryEditorUtils.Prop(prop.FindPropertyRelative("obj"));
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
                    flexBasis = 100
                }
            };
            row.Add(label);

            var propField = VRCFuryEditorUtils.Prop(prop.FindPropertyRelative("blendShape"));
            propField.style.flexGrow = 1;
            row.Add(propField);
            
            var valueField = VRCFuryEditorUtils.Prop(prop.FindPropertyRelative("blendShapeValue"));
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

            var label = new Label {
                text = "Animation Clip",
                style = {
                    flexGrow = 0,
                    flexBasis = 100
                }
            };
            row.Add(label);

            var propField = VRCFuryEditorUtils.Prop(prop.FindPropertyRelative("clip"));
            propField.style.flexGrow = 1;
            row.Add(propField);

            return row;
        }

        return VRCFuryEditorUtils.WrappedLabel("Unknown action");
    }
}

}
