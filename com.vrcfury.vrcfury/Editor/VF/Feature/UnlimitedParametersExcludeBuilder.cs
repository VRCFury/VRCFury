using UnityEditor;
using UnityEngine.UIElements;
using VF.Feature.Base;
using VF.Inspector;
using VF.Model.Feature;
using VF.Utils;

namespace VF.Feature {
    internal class UnlimitedParametersExcludeBuilder : FeatureBuilder<UnlimitedParametersExclude> {
        public override string GetEditorTitle() {
            return "Unlimited Parameters Exclusion";
        }

        public override VisualElement CreateEditor(SerializedProperty prop) {
            var content = new VisualElement();
            content.Add(VRCFuryEditorUtils.Info("This feature will allow you to exclude from the Unlimited Parameters component all variables associated with a given menu item."));

            var pathProp = prop.FindPropertyRelative("path");
            content.Add(MoveMenuItemBuilder.SelectButton(avatarObject, false, pathProp));

            return content;
        }

        public override bool AvailableOnRootOnly() {
            return false;
        }
    }
}
