using UnityEditor;
using UnityEngine.UIElements;
using VF.Feature.Base;
using VF.Inspector;
using VF.Model.Feature;
using Toggle = UnityEngine.UIElements.Toggle;

namespace VF.Feature {
    [FeatureAlias("Unlimited Parameters")]
    [FeatureTitle("Parameter Compressor")]
    [FeatureOnlyOneAllowed]
    [FeatureRootOnly]
    [FeatureFailWhenAdded(
        "VRCFury Parameter Compressor is now automatically enabled by default (when needed) for all avatars" +
        " using any VRCFury component. Adding the Parameter Compressor component is no longer needed."
    )]
    internal class UnlimitedParametersBuilder : FeatureBuilder<UnlimitedParameters> {
        [FeatureEditor]
        public static VisualElement Editor() {
            var content = new VisualElement();
            content.Add(VRCFuryEditorUtils.Error(
                "This component is deprecated and now does nothing." +
                " The VRCFury Parameter Compressor is now applied automatically for all avatars using VRCFury (if you are over the limit)."));
            return content;
        }
    }
}
