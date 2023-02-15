using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using VF.Model;
using VF.Model.StateAction;
using VRC.SDK3.Avatars.Components;
using static VF.Model.Feature.EmoteManager;

namespace VF.Inspector {

[CustomPropertyDrawer(typeof(VF.Model.Feature.EmoteManager))]
public class VRCFuryEmoteDrawer : PropertyDrawer {
    public override VisualElement CreatePropertyGUI(SerializedProperty prop) {
        return Render(prop);
    }

    public static VisualElement Render(SerializedProperty prop) {

        void addRow(VisualElement container, string propName, string label) {
            var row = new VisualElement {
                style = {
                    flexDirection = FlexDirection.Row,
                    alignItems = Align.FlexStart
                }
            };
            var lab = new Label {
                text = label,
                style = {
                    flexGrow = 0,
                    flexBasis = 150
                }
            };
            row.Add(lab);

            var propField = VRCFuryEditorUtils.Prop(prop.FindPropertyRelative(propName));
            propField.style.flexGrow = 1;
            row.Add(propField);
            container.Add(row);
        }

        var type = VRCFuryEditorUtils.GetManagedReferenceTypeName(prop);

        switch (type) {
            case nameof(Emote): {
                var container = new VisualElement();

                addRow(container, "name", "Name");
                addRow(container, "emoteAnimation", "Clip");
                addRow(container, "number", "VRCEmote Value");
                addRow(container, "isToggle", "Is Toggle");
                addRow(container, "hasReset", "Has Reset");
                addRow(container, "hasExitTime", "Has Exit Time");

                return container;
            }
        }

        return VRCFuryEditorUtils.WrappedLabel($"Unknown action type: ${type}");
    }
}

}
