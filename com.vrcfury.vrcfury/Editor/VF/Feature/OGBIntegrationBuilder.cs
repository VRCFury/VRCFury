using UnityEditor;
using UnityEngine.UIElements;
using VF.Builder.Haptics;
using VF.Feature.Base;
using VF.Inspector;
using VF.Model.Feature;

namespace VF.Feature {
    [FeatureTitle("Haptic Component Auto-Adder")]
    [FeatureHideInMenu]
    internal class OGBIntegrationBuilder : FeatureBuilder<OGBIntegration2> {
        [FeatureBuilderAction(FeatureOrder.UpgradeLegacyHaptics)]
        public void Apply() {
            SpsUpgrader.Apply(featureBaseObject, false, SpsUpgrader.Mode.AutomatedComponent);
        }
        
        public override VisualElement CreateEditor(SerializedProperty prop) {
            var content = new VisualElement();
            content.Add(VRCFuryEditorUtils.Error("This feature is deprecated. Use SPS instead! See vrcfury.com/sps"));
            return content;
        }
    }
}
