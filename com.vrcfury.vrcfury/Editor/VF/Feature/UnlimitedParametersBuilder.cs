using UnityEditor;
using UnityEngine.UIElements;
using VF.Feature.Base;
using VF.Inspector;
using VF.Model.Feature;

namespace VF.Feature {
    [FeatureAlias("Unlimited Parameters")]
    [FeatureTitle("Parameter Compressor")]
    [FeatureOnlyOneAllowed]
    [FeatureRootOnly]
    internal class UnlimitedParametersBuilder : FeatureBuilder<UnlimitedParameters> {
        [FeatureEditor]
        public static VisualElement Editor(SerializedProperty prop) {
            var content = new VisualElement();
            content.Add(VRCFuryEditorUtils.Info(
                "This component will optimize all synced float parameters used in radial menu toggles into 16 total bits"));
            content.Add(VRCFuryEditorUtils.Warn(
                "This feature is in BETA - Please report any issues on the VRCFury discord"));

            var includeBoolsProp = prop.FindPropertyRelative("includeBools");
            content.Add(VRCFuryEditorUtils.Prop(includeBoolsProp, "Optimize Bools"));
            content.Add(VRCFuryEditorUtils.RefreshOnChange(() => {
                if (includeBoolsProp.boolValue) {
                    return VRCFuryEditorUtils.Warn(
                        "Warning: Compressing bools often doesn't save much space, and can, in some rare cases, cause unusual sync issues with complex avatar systems.");
                }
                return new VisualElement();
            }, includeBoolsProp));

            var includePuppetsProp = prop.FindPropertyRelative("includePuppets");
            content.Add(VRCFuryEditorUtils.Prop(includePuppetsProp, "Optimize Puppets"));
            content.Add(VRCFuryEditorUtils.RefreshOnChange(() => {
                if (includePuppetsProp.boolValue) {
                    return VRCFuryEditorUtils.Warn(
                        "Warning: Compressing puppets may cause them to not move as smoothly for remote clients as you control them.");
                }
                return new VisualElement();
            }, includePuppetsProp));

            return content;
        }
    }
}
