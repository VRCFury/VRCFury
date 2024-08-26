using UnityEditor;
using UnityEngine.UIElements;
using VF.Builder.Haptics;
using VF.Feature.Base;
using VF.Inspector;
using VF.Model.Feature;

namespace VF.Feature {
    internal class OGBIntegrationBuilder : FeatureBuilder<OGBIntegration2> {
        [FeatureBuilderAction(FeatureOrder.UpgradeLegacyHaptics)]
        public void Apply() {
            SpsUpgrader.Apply(featureBaseObject, false, SpsUpgrader.Mode.AutomatedComponent);
        }
        
        public override bool ShowInMenu() {
            return false;
        }
        
        public override string GetEditorTitle() {
            return "Haptic Component Auto-Adder";
        }
        
        public override VisualElement CreateEditor(SerializedProperty prop) {
            var content = new VisualElement();
            content.Add(VRCFuryEditorUtils.Error("This feature is deprecated. Use SPS instead! See vrcfury.com/sps"));
            return content;
        }
    }
}
