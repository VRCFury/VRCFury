using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using VRCF.Model;

namespace VRCF.Inspector {

public class VRCFuryStateEditor {
    public static VisualElement render(SerializedProperty prop, string myLabel = "") {
        if (myLabel == null) myLabel = prop.name;

        var container = new VisualElement();

        var list = prop.FindPropertyRelative("actions");

        Action onPlus = () => {
            var menu = new GenericMenu();
            menu.AddItem(new GUIContent("Object Toggle"), false, () => {
                VRCFuryEditorUtils.AddToList(list, entry => entry.FindPropertyRelative("type").stringValue = VRCFuryAction.TOGGLE);
            });
            menu.AddItem(new GUIContent("BlendShape"), false, () => {
                VRCFuryEditorUtils.AddToList(list, entry => entry.FindPropertyRelative("type").stringValue = VRCFuryAction.BLENDSHAPE);
            });
            menu.ShowAsContext();
        };

        var clipProp = prop.FindPropertyRelative("clip");
        var actions = prop.FindPropertyRelative("actions");

        container.Add(VRCFuryEditorUtils.RefreshOnChange(() => {
            var body = new VisualElement();
            var hasClip = clipProp.objectReferenceValue != null;
            var hasActions = actions.arraySize > 0;

            var showLabel = myLabel != "";
            var showClipBox = !hasActions || hasClip;
            var showPlus = !hasActions && !hasClip;
            var showActions = hasActions;

            if (showLabel || showClipBox || showPlus) {
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
                if (showClipBox) {
                    var clipBox = VRCFuryEditorUtils.PropWithoutLabel(clipProp);
                    clipBox.style.flexGrow = 1;
                    segments.Add(clipBox);
                }
                if (showPlus) {
                    var plus = new Button(onPlus);
                    plus.style.flexGrow = 0;
                    plus.text = "+";
                    segments.Add(plus);
                }
            }
            if (showActions) {
                body.Add(VRCFuryEditorUtils.List(list, onPlus: onPlus));
            }

            return body;
        }, list, clipProp, actions));

        return container;
    }
}

}
