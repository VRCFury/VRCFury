using UnityEditor;
using UnityEngine.UIElements;
using VF.Feature.Base;
using VF.Inspector;
using VF.Menu;
using VF.Model.Feature;

namespace VF.Feature {
    public class OGBIntegrationBuilder : FeatureBuilder<OGBIntegration> {
        [FeatureBuilderAction((int)FeatureOrder.AddOgbComponents)]
        public void Apply() {
            DPSContactUpgradeBuilder.Apply(featureBaseObject);
        }
        
        public override string GetEditorTitle() {
            return "OGB Component Auto-Adder";
        }
        
        public override VisualElement CreateEditor(SerializedProperty prop) {
            return VRCFuryEditorUtils.WrappedLabel(
                "This feature will search for TPS/DPS orifices and penetrators that are children of this object, and automatically add OGB components to them." +
                " You don't need this if you've already run the OGB upgrade tool yourself (which means the components are already added).");
        }
    }
}
