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
            menu.AddItem(new GUIContent("Animation Clip"), false,
                () => { VRCFuryEditorUtils.AddToList(list, entry => entry.managedReferenceValue = new AnimationClipAction()); });
            menu.ShowAsContext();
        }

        var actions = prop.FindPropertyRelative("actions");

        container.Add(VRCFuryEditorUtils.RefreshOnChange(() => {
            var body = new VisualElement();

            var showLabel = myLabel != "";
            SerializedProperty singleClip = null;
            if (list.arraySize == 1) {
                singleClip = list.GetArrayElementAtIndex(0).FindPropertyRelative("clip");
            }

            var showPlus = singleClip != null || list.arraySize == 0;
            var showSingleClip = singleClip != null;
            var showList = singleClip == null && list.arraySize > 0;

            if (showLabel || showSingleClip || showPlus) {
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
                if (showSingleClip) {
                    var clipField = VRCFuryEditorUtils.PropWithoutLabel(singleClip);
                    clipField.style.flexGrow = 1;
                    segments.Add(clipField);
                    var x = new Button(() => {
                        list.DeleteArrayElementAtIndex(0);
                        list.serializedObject.ApplyModifiedProperties();
                    }) {
                        style = { flexGrow = 0 },
                        text = "x",
                    };
                    segments.Add(x);
                }
                if (showPlus) {
                    var plus = new Button(OnPlus) {
                        style = { flexGrow = showSingleClip ? 0 : 1 },
                        text = "+",
                    };
                    segments.Add(plus);
                }
            }
            if (showList) {
                body.Add(VRCFuryEditorUtils.List(list, onPlus: OnPlus));
            }

            return body;
        }, list, actions));

        return container;
    }
}

}
