using UnityEditor;
using UnityEngine.UIElements;

namespace VF.Inspector {

public class VRCFuryEmoteEditor {
    public static VisualElement render(SerializedProperty prop, string myLabel = null, int labelWidth = 100) {
        var container = new VisualElement();

        var list = prop;

        void OnPlus() {
            VRCFuryEditorUtils.AddToList(list, entry => entry.managedReferenceValue = new Model.Feature.EmoteManager.Emote());
        }

        container.Add(VRCFuryEditorUtils.RefreshOnChange(() => {

            var body = new VisualElement();
            
            var showLabel = myLabel != null;

            var showPlus = list.arraySize == 0;
            var showList =  list.arraySize > 0;

            if (showLabel || showPlus) {
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
                if (showPlus) {
                    var plus = VRCFuryEditorUtils.Button("Add Emote +", OnPlus);
                    plus.style.flexGrow =  1;
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
