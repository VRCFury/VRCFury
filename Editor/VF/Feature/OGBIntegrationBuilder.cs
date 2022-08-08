using VF.Feature.Base;
using VF.Menu;
using VF.Model.Feature;

namespace VF.Feature {
    public class OGBIntegrationBuilder : FeatureBuilder<OGBIntegration> {
        [FeatureBuilderAction(applyToVrcClone:true, priority: 100)]
        public void ApplyToVrcClone() {
            var isFirst = allFeaturesInRun.Find(m => m is OGBIntegration) == model;
            if (!isFirst) return;
            DPSContactUpgradeBuilder.Apply(avatarObject);
        }
        
        public override string GetEditorTitle() {
            return "OGB Integration";
        }

        public override bool AvailableOnProps() {
            return false;
        }
    }
}
