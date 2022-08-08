using UnityEditor;
using UnityEngine.UIElements;
using VF.Feature.Base;
using VF.Inspector;
using VF.Menu;
using VF.Model.Feature;

namespace VF.Feature {
    public class OGBIntegrationBuilder : FeatureBuilder<OGBIntegration> {
        [FeatureBuilderAction(100)]
        public void Apply() {
            var isFirst = allFeaturesInRun.Find(m => m is OGBIntegration) == model;
            if (!isFirst) return;
            DPSContactUpgradeBuilder.Apply(avatarObject);
        }
        
        public override string GetEditorTitle() {
            return "OGB Integration";
        }
        
        public override VisualElement CreateEditor(SerializedProperty prop) {
            return VRCFuryEditorUtils.WrappedLabel("This feature will automatically add OGB contacts to your avatar (only during upload)");
        }

        public override bool AvailableOnProps() {
            return false;
        }
    }
}
