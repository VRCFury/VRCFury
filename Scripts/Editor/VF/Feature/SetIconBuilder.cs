using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using VF.Feature.Base;
using VF.Inspector;
using VF.Model.Feature;

namespace VF.Feature {
    public class SetIconBuilder : FeatureBuilder<SetIcon> {
        // We run this twice, once before MoveMenuItems and once after, so users can set the icon on either the
        // old or new path.
        [FeatureBuilderAction(FeatureOrder.SetMenuIcons1)]
        public void Apply1() {
            Apply();
        }
        [FeatureBuilderAction(FeatureOrder.SetMenuIcons2)]
        public void Apply2() {
            Apply();
        }
        
        public void Apply() {
            var result = manager.GetMenu().SetIcon(model.path, model.icon);
            if (!result) {
                Debug.LogWarning("Failed to find menu item to set icon");
            }
        }

        public override string GetEditorTitle() {
            return "Override Menu Icon";
        }
        
        public override VisualElement CreateEditor(SerializedProperty prop) {
            var content = new VisualElement();
            content.Add(VRCFuryEditorUtils.Info("This feature will override the menu icon for the given menu path. You can use slashes to look in subfolders."));
            content.Add(VRCFuryEditorUtils.Prop(prop.FindPropertyRelative("path"), "Menu Path"));
            content.Add(VRCFuryEditorUtils.Prop(prop.FindPropertyRelative("icon"), "Icon"));
            return content;
        }
    }
}