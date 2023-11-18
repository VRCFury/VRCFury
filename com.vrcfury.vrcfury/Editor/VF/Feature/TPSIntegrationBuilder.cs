using UnityEditor;
using UnityEngine.UIElements;
using VF.Feature.Base;
using VF.Inspector;
using VF.Model.Feature;

namespace VF.Feature {
    public class TPSIntegrationBuilder : FeatureBuilder<TPSIntegration2> {
        [FeatureBuilderAction]
        public void Apply() {

        }

        public override bool ShowInMenu() {
            return false;
        }

        public override string GetEditorTitle() {
            return "TPS Integration";
        }
        
        public override VisualElement CreateEditor(SerializedProperty prop) {
            var content = new VisualElement();
            content.Add(VRCFuryEditorUtils.Error("This feature is deprecated and now does nothing. Use SPS instead! See vrcfury.com/sps"));
            return content;
        }

        public override bool AvailableOnProps() {
            return false;
        }
    }
}