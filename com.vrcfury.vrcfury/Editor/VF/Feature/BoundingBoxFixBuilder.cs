using UnityEditor;
using UnityEngine.UIElements;
using VF.Feature.Base;
using VF.Inspector;
using VF.Model.Feature;

namespace VF.Feature {

    public class BoundingBoxFixBuilder : FeatureBuilder<BoundingBoxFix2> {
        public override bool ShowInMenu() {
            return false;
        }

        public override string GetEditorTitle() {
            return "Bounding Box Fix";
        }
        
        public override VisualElement CreateEditor(SerializedProperty prop) {
            var content = new VisualElement();
            content.Add(VRCFuryEditorUtils.Error(
                "This feature is deprecated and now does nothing." +
                " Bounding Box Fix is now automatically enabled by default for all avatars using VRCFury."));
            return content;
        }
    }
}
