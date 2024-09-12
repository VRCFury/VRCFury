using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using VF.Builder;
using VF.Feature.Base;
using VF.Inspector;
using VF.Model.Feature;
using VF.Utils;

namespace VF.Feature {
    [FeatureTitle("Override Menu Icon")]
    internal class SetIconBuilder : FeatureBuilder<SetIcon> {
        
        [FeatureEditor]
        public static VisualElement Editor(SerializedProperty prop, VFGameObject avatarObject) {
            var content = new VisualElement();
            content.Add(VRCFuryEditorUtils.Info("This feature will override the menu icon for the given menu path. You can use slashes to look in subfolders."));

            var pathProp = prop.FindPropertyRelative("path");
            content.Add(MoveMenuItemBuilder.SelectButton(avatarObject, false, pathProp));
            
            content.Add(VRCFuryEditorUtils.Prop(prop.FindPropertyRelative("icon"), "Icon"));
            return content;
        }
    }
}
