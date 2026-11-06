using UnityEngine.UIElements;
using VF.Feature.Base;
using VF.Inspector;
using VF.Model.Feature;

namespace VF.Feature {
    [FeatureTitle("Haptic Component Auto-Adder")]
    [FeatureHideInMenu]
    internal class OGBIntegrationBuilder : FeatureBuilder<OGBIntegration2> {
        [FeatureEditor]
        public static VisualElement Editor() {
            return VRCFuryEditorUtils.Error("This feature is deprecated and now does nothing. Use SPS instead! See vrcfury.com/sps");
        }
    }
}
