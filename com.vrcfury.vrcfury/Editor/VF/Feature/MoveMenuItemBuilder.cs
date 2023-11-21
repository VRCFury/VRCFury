using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using VF.Feature.Base;
using VF.Inspector;
using VF.Model.Feature;

namespace VF.Feature {
    public class MoveMenuItemBuilder : FeatureBuilder<MoveMenuItem> {
        public override string GetEditorTitle() {
            return "Move Menu Item";
        }
        
        public override VisualElement CreateEditor(SerializedProperty prop) {
            var content = new VisualElement();
            content.Add(VRCFuryEditorUtils.Info("This feature will move a menu item to another location. You can use slashes to make subfolders."));
            content.Add(VRCFuryEditorUtils.Prop(prop.FindPropertyRelative("fromPath"), "From Path"));
            content.Add(VRCFuryEditorUtils.Prop(prop.FindPropertyRelative("toPath"), "To Path"));
            return content;
        }

        public override bool AvailableOnRootOnly() {
            return true;
        }
    }
}