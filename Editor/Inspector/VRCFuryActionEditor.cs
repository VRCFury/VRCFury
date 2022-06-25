using System;
using UnityEngine;
using UnityEditor;
using UnityEditor.UIElements;
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
                var row = new VisualElement();
                row.style.flexDirection = FlexDirection.Row;
                row.style.alignItems = Align.FlexStart;

                var label = new Label("Object Toggle");
                label.style.flexGrow = 0;
                label.style.flexBasis = VRCFuryEditorUtils.LABEL_WIDTH;
                row.Add(label);

                var propField = VRCFuryEditorUtils.PropWithoutLabel(prop.FindPropertyRelative("obj"));
                propField.style.flexGrow = 1;
                row.Add(propField);

                return row;
            } else if (type == VRCFuryAction.BLENDSHAPE) {
                var row = new VisualElement();
                row.style.flexDirection = FlexDirection.Row;
                row.style.alignItems = Align.FlexStart;

                var label = new Label("BlendShape");
                label.style.flexGrow = 0;
                label.style.flexBasis = VRCFuryEditorUtils.LABEL_WIDTH;
                row.Add(label);

                var propField = VRCFuryEditorUtils.PropWithoutLabel(prop.FindPropertyRelative("blendShape"));
                propField.style.flexGrow = 1;
                row.Add(propField);

                return row;
            } else {
                return new Label("Unknown action: " + type);
            }
        }, typeProp);
    }
}

}
