using UnityEditor;
using UnityEngine.UIElements;
using VF.Feature.Base;
using VF.Inspector;
using VF.Menu;
using VF.Model.Feature;

namespace VF.Feature {
    public class OGBIntegrationBuilder : FeatureBuilder<OGBIntegration2> {
        [FeatureBuilderAction(FeatureOrder.AddOgbComponents)]
        public void Apply() {
            OGBUpgradeMenuItem.Apply(featureBaseObject, false);
        }
        
        public override bool ShowInMenu() {
            return false;
        }
        
        public override string GetEditorTitle() {
            return "OGB Component Auto-Adder";
        }
        
        public override VisualElement CreateEditor(SerializedProperty prop) {
            var content = new VisualElement();
            content.Add(VRCFuryEditorUtils.Error("This feature is deprecated. Add OGB Orifice and OGB Penetrator (with TPS autoconfiguration) components instead!"));
            return content;
        }
    }
}
