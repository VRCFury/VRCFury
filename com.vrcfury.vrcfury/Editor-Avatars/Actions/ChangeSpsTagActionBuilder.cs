using UnityEditor;
using UnityEngine.UIElements;
using VF.Feature.Base;
using VF.Inspector;
using VF.Model.StateAction;
using VF.Utils.Controller;

namespace VF.Actions {
    [FeatureTitle("Change SPS Tag")]
    [FeatureHideInMenu]
    internal class ChangeSpsTagActionBuilder : ActionBuilder<ChangeSpsTagAction> {
        public VFClip Build(ChangeSpsTagAction model, bool debugMode) => NewClip();

        [FeatureEditor]
        public static VisualElement Editor(SerializedProperty prop) => VRCFuryEditorUtils.Warn(
            "Change SPS Tag is not supported at this time. Only one set of channels can currently be configured on the SPS components.");
    }
}
