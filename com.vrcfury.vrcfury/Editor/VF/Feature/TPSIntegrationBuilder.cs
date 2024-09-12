using UnityEditor;
using UnityEngine.UIElements;
using VF.Feature.Base;
using VF.Inspector;
using VF.Model.Feature;

namespace VF.Feature {
    [FeatureTitle("TPS Integration")]
    [FeatureRootOnly]
    [FeatureHideInMenu]
    internal class TPSIntegrationBuilder : FeatureBuilder<TPSIntegration2> {
        [FeatureBuilderAction]
        public void Apply() {

        }
        
        public override VisualElement CreateEditor(SerializedProperty prop) {
            var content = new VisualElement();
            content.Add(VRCFuryEditorUtils.Error("This feature is deprecated and now does nothing. Use SPS instead! See vrcfury.com/sps"));
            return content;
        }
    }
}