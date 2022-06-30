using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using VF.Model;
using VF.Model.StateAction;

namespace VF.Inspector {

public class VRCFuryStateEditor {
    public static VisualElement render(SerializedProperty prop, string myLabel = "") {
        if (myLabel == null) myLabel = prop.name;

        var container = new VisualElement();

        var list = prop.FindPropertyRelative("actions");

        void OnPlus() {
            var menu = new GenericMenu();
            menu.AddItem(new GUIContent("Object Toggle"), false,
                () => { VRCFuryEditorUtils.AddToList(list, entry => entry.managedReferenceValue = new ObjectToggleAction()); });
            menu.AddItem(new GUIContent("BlendShape"), false,
                () => { VRCFuryEditorUtils.AddToList(list, entry => entry.managedReferenceValue = new BlendShapeAction()); });
            menu.AddItem(new GUIContent("Clip"), false,
                () => { VRCFuryEditorUtils.AddToList(list, entry => entry.managedReferenceValue = new AnimationClipAction()); });
            menu.ShowAsContext();
        }

        var clipProp = prop.FindPropertyRelative("clip");
        var actions = prop.FindPropertyRelative("actions");

        container.Add(VRCFuryEditorUtils.RefreshOnChange(() => {
            var body = new VisualElement();
            var hasActions = actions.arraySize > 0;

            var showLabel = myLabel != "";
            var showPlus = true; 
            var showActions = hasActions;

            if (showLabel || showPlus) {
                var segments = new VisualElement();
                body.Add(segments);
                segments.style.flexDirection = FlexDirection.Row;
                segments.style.alignItems = Align.FlexStart;

                if (showLabel) {
                    var label = new Label(myLabel);
                    label.style.flexBasis = VRCFuryEditorUtils.LABEL_WIDTH;
                    label.style.flexGrow = 0;
                    segments.Add(label);
                }
                if (showPlus) {
                    var plus = new Button(OnPlus);
                    plus.style.flexGrow = 0;
                    plus.text = "+";
                    segments.Add(plus);
                }
            }
            if (showActions) {
                body.Add(VRCFuryEditorUtils.List(list, onPlus: OnPlus));
            }

            return body;
        }, list, clipProp, actions));

        return container;
    }
}

}
