using UnityEditor;
using UnityEngine.UIElements;
using VF.Builder;
using VF.Feature.Base;
using VF.Inspector;
using VF.Model.Feature;
using VF.Utils;

namespace VF.Feature {
    [FeatureTitle("Reorder Menu Item")]
    [FeatureRootOnly]
    internal class ReorderMenuItemBuilder : FeatureBuilder<ReorderMenuItem> {

        [FeatureEditor]
        public static VisualElement Editor(SerializedProperty prop, VFGameObject avatarObject) {
            var content = new VisualElement();
            content.Add(VRCFuryEditorUtils.Info("This feature will change the position of a menu item within its folder."));

            var pathProp = prop.FindPropertyRelative("path");
            content.Add(MoveMenuItemBuilder.SelectButton(avatarObject, false, pathProp));

            content.Add(VRCFuryEditorUtils.Prop(prop.FindPropertyRelative("position"), "New Position",
                tooltip: "This is the position that you want to move the menu item to.\n\nExamples:\n" +
                         "0 = 'First' (Top Right)\n" +
                         "1 = 'Second'\n" +
                         "4 = 'Fifth'\n" +
                         "999 = 'Last' (Top Left)\n" +
                         "-1 = 'Second-to-Last'\n" + 
                         "-2 = 'Third-to-Last'\n" +
                        "etc"
            ));
            return content;
        }
    }
}
