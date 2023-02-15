using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using VF.Model;
using VF.Model.StateAction;
using static VF.Model.Feature.EmoteManager;

namespace VF.Inspector {

public class VRCFuryEmoteEditor {
    public static VisualElement render(SerializedProperty prop, string myLabel = null, int labelWidth = 100) {
        var container = new VisualElement();

        var list = prop;

        void OnPlus() {
            VRCFuryEditorUtils.AddToList(list, entry => entry.managedReferenceValue = new Emote());
        }

        container.Add(VRCFuryEditorUtils.RefreshOnChange(() => {
            var body = new VisualElement();

            var showLabel = myLabel != null;
            VisualElement singleLineEditor = null;
            
            if (list.arraySize == 1) {
                singleLineEditor = VRCFuryEmoteDrawer.Render(list.GetArrayElementAtIndex(0));
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
                    label.style.flexBasis = labelWidth;
                    label.style.flexGrow = 0;
                    segments.Add(label);
                }
                if (showSingleLineEditor) {
                    singleLineEditor.style.flexGrow = 1;
                    segments.Add(singleLineEditor);
                    var x = VRCFuryEditorUtils.Button("x", () => {
                        list.DeleteArrayElementAtIndex(0);
                        list.serializedObject.ApplyModifiedProperties();
                    });
                    x.style.flexGrow = 0;
                    x.style.flexBasis = 20;
                    segments.Add(x);
                }
                if (showPlus) {
                    var plus = VRCFuryEditorUtils.Button(singleLineEditor != null ? "+" : "Add Emote +", OnPlus);
                    plus.style.flexGrow = showSingleLineEditor ? 0 : 1;
                    plus.style.flexBasis = 20;
                    segments.Add(plus);
                }
            }
            if (showList) {
                body.Add(VRCFuryEditorUtils.List(list, onPlus: OnPlus));
            }

            return body;
        }, list));

        return container;
    }
}

}
