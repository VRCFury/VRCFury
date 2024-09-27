using UnityEngine.UIElements;
using VF.Feature.Base;
using VF.Inspector;
using VF.Model.Feature;

namespace VF.Feature {
    [FeatureTitle("Direct Tree Optimizer")]
    [FeatureOnlyOneAllowed]
    [FeatureRootOnly]
    internal class LayerToTreeBuilder : FeatureBuilder<DirectTreeOptimizer> {
        [FeatureEditor]
        public static VisualElement Editor() {
            var content = new VisualElement();
            content.Add(VRCFuryEditorUtils.Info(
                "This feature will automatically convert all non-conflicting toggle layers into a single direct blend tree layer."
            ));
            return content;
        }
    }
}
