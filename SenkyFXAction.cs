#if UNITY_EDITOR

using System;
using UnityEngine;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

[Serializable]
public class SenkyFXAction {
    public const string TOGGLE = "toggle";
    public const string BLENDSHAPE = "blendShape";

    public string type;
    public GameObject obj;
    public string blendShape;
}

[CustomPropertyDrawer(typeof(SenkyFXAction))]
public class SenkyFXActionDrawer : PropertyDrawer {
    public override VisualElement CreatePropertyGUI(SerializedProperty prop) {
        var typeProp = prop.FindPropertyRelative("type");

        return SenkyUIHelper.RefreshOnChange(() => {
            var type = typeProp.stringValue;
            if (type == SenkyFXAction.TOGGLE) {
                var row = new VisualElement();
                row.style.flexDirection = FlexDirection.Row;
                row.style.alignItems = Align.FlexStart;

                var label = new Label("Object Toggle");
                label.style.flexGrow = 0;
                label.style.flexBasis = SenkyUIHelper.LABEL_WIDTH;
                row.Add(label);

                var propField = SenkyUIHelper.PropWithoutLabel(prop.FindPropertyRelative("obj"));
                propField.style.flexGrow = 1;
                row.Add(propField);

                return row;
            } else if (type == SenkyFXAction.BLENDSHAPE) {
                var row = new VisualElement();
                row.style.flexDirection = FlexDirection.Row;
                row.style.alignItems = Align.FlexStart;

                var label = new Label("BlendShape");
                label.style.flexGrow = 0;
                label.style.flexBasis = SenkyUIHelper.LABEL_WIDTH;
                row.Add(label);

                var propField = SenkyUIHelper.PropWithoutLabel(prop.FindPropertyRelative("blendShape"));
                propField.style.flexGrow = 1;
                row.Add(propField);

                return row;
            } else {
                return new Label("Unknown action: " + type);
            }
        }, typeProp);
    }
}

#endif
