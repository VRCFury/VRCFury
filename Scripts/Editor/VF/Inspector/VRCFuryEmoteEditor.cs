using UnityEditor;
using UnityEngine.UIElements;
using static VF.Model.Feature.EmoteManager;

namespace VF.Inspector {

[CustomPropertyDrawer(typeof(VF.Model.Feature.EmoteManager))]
public class VRCFuryEmoteDrawer : PropertyDrawer {
    public override VisualElement CreatePropertyGUI(SerializedProperty prop) {
        return Render(prop);
    }

    public static VisualElement Render(SerializedProperty prop) {

        var hasResetProp = prop.FindPropertyRelative("hasReset");
        var hasExitTimeProp = prop.FindPropertyRelative("hasExitTime");

        VisualElement addRow(string propName, string label) {
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
            return row;
        }

        var type = VRCFuryEditorUtils.GetManagedReferenceTypeName(prop);

        switch (type) {
            case nameof(Emote): {
                var container = new VisualElement();

                container.Add(addRow("name", "Name"));
                container.Add(addRow("emoteAnimation", "Clip"));

                var adv = new Foldout {
                    text = "Advanced Options",
                    value = false
                };
                adv.Add(addRow("icon", "Icon"));
                adv.Add(addRow("isToggle", "Is Toggle"));
                adv.Add(addRow("hasReset", "Has Reset"));
                adv.Add(VRCFuryEditorUtils.RefreshOnChange(() => {
                    var row = new VisualElement();
                    if (hasResetProp.boolValue) {
                        row.Add(addRow("resetAnimation", "Reset Animation"));
                    }
                    return row;
                }, hasResetProp));
                adv.Add(addRow("hasExitTime", "Has Exit Time"));
                adv.Add(VRCFuryEditorUtils.RefreshOnChange(() => {
                    var row = new VisualElement();
                    if (hasExitTimeProp.boolValue) {
                        row.Add(addRow("exitTime", "Exit Time"));
                    }
                    return row;
                }, hasExitTimeProp));
                VRCFuryEditorUtils.Padding(adv, 0, 10);
                container.Add(adv);

                return container;
            }
        }

        return VRCFuryEditorUtils.WrappedLabel($"Unknown action type: ${type}");
    }
}

}
