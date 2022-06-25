using System;
using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

public class SenkyFXStateEditor {
    public static VisualElement render(SerializedProperty prop, string myLabel = "") {
        if (myLabel == null) myLabel = prop.name;

        var container = new VisualElement();

        var list = prop.FindPropertyRelative("actions");

        Action onPlus = () => {
            var menu = new GenericMenu();
            menu.AddItem(new GUIContent("Object Toggle"), false, () => {
                SenkyUIHelper.addToList(list, entry => entry.FindPropertyRelative("type").stringValue = SenkyFXAction.TOGGLE);
            });
            menu.AddItem(new GUIContent("BlendShape"), false, () => {
                SenkyUIHelper.addToList(list, entry => entry.FindPropertyRelative("type").stringValue = SenkyFXAction.BLENDSHAPE);
            });
            menu.ShowAsContext();
        };

        var clipProp = prop.FindPropertyRelative("clip");
        var actions = prop.FindPropertyRelative("actions");

        container.Add(SenkyUIHelper.RefreshOnChange(() => {
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
                    label.style.flexBasis = SenkyUIHelper.LABEL_WIDTH;
                    label.style.flexGrow = 0;
                    segments.Add(label);
                }
                if (showClipBox) {
                    var clipBox = SenkyUIHelper.PropWithoutLabel(clipProp);
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
                body.Add(SenkyUIHelper.List(list, onPlus: onPlus));
            }

            return body;
        }, list, clipProp, actions));

        return container;
    }
}
