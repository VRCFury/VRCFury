using UnityEngine.UIElements;
using VF.Builder.Haptics;
using VF.Feature.Base;
using VF.Inspector;
using VF.Injector;
using VF.Model.Feature;

namespace VF.Feature {
    [FeatureTitle("Haptic Component Auto-Adder")]
    [FeatureHideInMenu]
    internal class OGBIntegrationBuilder : FeatureBuilder<OGBIntegration2> {
        [FeatureEditor]
        public static VisualElement Editor() {
            var content = new VisualElement();
            content.Add(VRCFuryEditorUtils.Error("This feature is deprecated. Use SPS instead! See vrcfury.com/sps"));
            return content;
        }
    }
}
