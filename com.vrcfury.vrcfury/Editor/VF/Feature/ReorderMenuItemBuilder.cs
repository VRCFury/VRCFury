using UnityEditor;
using UnityEngine.UIElements;
using VF.Feature.Base;
using VF.Inspector;
using VF.Model.Feature;

namespace VF.Feature {
    internal class ReorderMenuItemBuilder : FeatureBuilder<ReorderMenuItem> {
        public override string GetEditorTitle() {
            return "Reorder Menu Item";
        }

        public override VisualElement CreateEditor(SerializedProperty prop) {
            var content = new VisualElement();
            content.Add(VRCFuryEditorUtils.Info("This feature will change the position of a menu item within its folder."));
            content.Add(VRCFuryEditorUtils.Prop(prop.FindPropertyRelative("path"), "Menu Item Path"));
            content.Add(VRCFuryEditorUtils.Prop(prop.FindPropertyRelative("position"), "New Position",
                tooltip: "This is the position that you want to move the menu item to.\n\nExamples:\n" +
                         "0 = 'First' (Top Right)\n" +
                         "1 = 'Second'\n" +
                         "4 = 'Fourth'\n" +
                         "999 = 'Last' (Top Left)\n" +
                         "-1 = 'Second-to-Last'\n" + 
                         "-2 = 'Third-to-Last'\n" +
                        "etc"
            ));
            return content;
        }

        public override bool AvailableOnRootOnly() {
            return true;
        }
    }
}
