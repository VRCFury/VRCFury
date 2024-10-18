using UnityEngine.UIElements;
using VF.Feature.Base;
using VF.Inspector;
using VF.Model.Feature;

namespace VF.Feature {
    [FeatureTitle("Droppable (World Constraint)")]
    [FeatureFailWhenAdded(
        "VRCFury's Droppable component has been replaced by the World Drop action. Add a VRCFury Toggle component, then add a World Drop action to it."
    )]
    internal class WorldConstraintBuilder : FeatureBuilder<WorldConstraint> {
        [FeatureEditor]
        public static VisualElement Editor() {
            return new VisualElement();
        }
    }
}
