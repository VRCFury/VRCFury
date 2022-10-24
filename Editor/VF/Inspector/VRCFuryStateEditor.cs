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
            menu.AddItem(new GUIContent("Flipbook Frame"), false,
                () => { VRCFuryEditorUtils.AddToList(list, entry => entry.managedReferenceValue = new FlipbookAction()); });
            menu.AddItem(new GUIContent("Object Scale"), false,
                () => { VRCFuryEditorUtils.AddToList(list, entry => entry.managedReferenceValue = new ScaleAction()); });
            menu.ShowAsContext();
        }

        var actions = prop.FindPropertyRelative("actions");

        container.Add(VRCFuryEditorUtils.RefreshOnChange(() => {
            var body = new VisualElement();

            var showLabel = myLabel != "";
            VisualElement singleLineEditor = null;
            if (list.arraySize == 1) {
                singleLineEditor = VRCFuryActionDrawer.Render(list.GetArrayElementAtIndex(0), true);
            }

            var showPlus = singleLineEditor != null || list.arraySize == 0;
            var showSingleLineEditor = singleLineEditor != null;
            var showList = singleLineEditor == null && list.arraySize > 0;

            if (showLabel || showSingleLineEditor || showPlus) {
                var segments = new VisualElement();
                body.Add(segments);
                segments.style.flexDirection = FlexDirection.Row;
                segments.style.alignItems = Align.FlexStart;

                if (showLabel) {
                    var label = new Label(myLabel);
                    label.style.flexBasis = 100;
                    label.style.flexGrow = 0;
                    segments.Add(label);
                }
                if (showSingleLineEditor) {
                    singleLineEditor.style.flexGrow = 1;
                    segments.Add(singleLineEditor);
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
                        style = { flexGrow = showSingleLineEditor ? 0 : 1 },
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
