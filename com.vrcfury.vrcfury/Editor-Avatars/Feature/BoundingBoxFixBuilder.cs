using UnityEditor;
using UnityEngine.UIElements;
using VF.Feature.Base;
using VF.Inspector;
using VF.Model.Feature;

namespace VF.Feature {
    [FeatureTitle("Bounding Box Fix")]
    [FeatureFailWhenAdded(
        "Bounding Box Fix is now automatically enabled by default for all avatars" +
        " using any VRCFury component. Adding the Bounding Box Fix component is no longer needed."
    )]
    internal class BoundingBoxFixBuilder : FeatureBuilder<BoundingBoxFix2> {
        [FeatureEditor]
        public static VisualElement Editor() {
            var content = new VisualElement();
            content.Add(VRCFuryEditorUtils.Error(
                "This component is deprecated and now does nothing." +
                " Bounding Box Fix is now automatically enabled by default for all avatars using VRCFury."));
            return content;
        }
    }
}
