using System;
using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;
using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using VRC.SDK3.Avatars.Components;
using AnimatorAsCode.V0;
using UnityEditor.UIElements;
using UnityEngine.UIElements;
using UnityEditorInternal;

[Serializable]
public class SenkyFXState {
    public AnimationClip clip;
    public List<SenkyFXAction> actions = new List<SenkyFXAction>();
    public bool isEmpty() {
        return clip == null && actions.Count == 0;
    }
}

[CustomPropertyDrawer(typeof(SenkyFXState))]
public class SenkyFXStateDrawer : BetterPropertyDrawer {
    private void newItem(SerializedProperty list) {
        var menu = new GenericMenu();
        menu.AddItem(new GUIContent("Object Toggle"), false, () => {
            addToList(list, entry => entry.FindPropertyRelative("type").stringValue = SenkyFXAction.TOGGLE);
        });
        menu.AddItem(new GUIContent("BlendShape"), false, () => {
            addToList(list, entry => entry.FindPropertyRelative("type").stringValue = SenkyFXAction.BLENDSHAPE);
        });
        menu.ShowAsContext();
    }

    protected override void render(SerializedProperty prop, GUIContent label) {
        var listProp = prop.FindPropertyRelative("actions");
        var list = makeList(listProp, () => newItem(listProp));

        var clipProp = prop.FindPropertyRelative("clip");
        var hasClip = clipProp.objectReferenceValue != null;
        var actions = prop.FindPropertyRelative("actions");
        var hasActions = actions.arraySize > 0;

        var showLabel = label.text != "";
        var showClipBox = !hasActions || hasClip;
        var showPlus = !hasActions && !hasClip;
        var showActions = hasActions;

        if (showLabel || showClipBox || showPlus) {
            var segments = new List<float>();
            if (showLabel) segments.Add(1);
            if (showClipBox) segments.Add(1);
            if (showPlus) segments.Add(line);
            var rects = renderFlex(line, segments.ToArray());
            var i = 0;
            if (showLabel) renderLabel(rects[i++], label.text);
            if (showClipBox) renderProp(rects[i++], clipProp);
            if (showPlus) renderButton(rects[i++], "+", () => newItem(listProp));
        }
        if (showActions) {
            renderList(list);
        }
    }
}
