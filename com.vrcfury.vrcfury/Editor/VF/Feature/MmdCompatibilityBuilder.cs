using System.Linq;
using UnityEditor;
using UnityEngine.UIElements;
using VF.Feature.Base;
using VF.Inspector;
using VF.Model.Feature;

namespace VF.Feature {
    public class MmdCompatibilityBuilder : FeatureBuilder<MmdCompatibility> {
        public override string GetEditorTitle() {
            return "MMD Compatibility";
        }

        public override VisualElement CreateEditor(SerializedProperty prop) {
            return VRCFuryEditorUtils.Info(
                "This component will improve MMD compatibility for your avatar, by maintaining MMD" +
                " blendshapes, and avoiding usage of layers that MMD worlds are known to interfere with.");
        }

        public override bool OnlyOneAllowed() {
            return true;
        }

        public override bool AvailableOnProps() {
            return false;
        }

        [FeatureBuilderAction(FeatureOrder.AvoidMmdLayers)]
        public void Apply() {
            var fx = GetFx();
            if (fx.GetLayers().Count() <= 1) {
                return;
            }

            fx.NewLayer("MMD Dummy Layer 1", 1);
            fx.NewLayer("MMD Dummy Layer 2", 2);
        }
    }
}
