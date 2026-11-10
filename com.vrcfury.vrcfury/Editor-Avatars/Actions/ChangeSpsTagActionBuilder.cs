using UnityEditor;
using UnityEngine.UIElements;
using VF.Feature.Base;
using VF.Inspector;
using VF.Model.StateAction;
using VF.Utils.Controller;

namespace VF.Actions {
    [FeatureTitle("Legacy Change SPS Tags")]
    [FeatureHideInMenu]
    internal class ChangeSpsTagActionBuilder : ActionBuilder<ChangeSpsTagAction> {
        public VFClip Build(ChangeSpsTagAction model, bool debugMode) => NewClip();

        [FeatureEditor]
        public static VisualElement Editor(SerializedProperty prop) => VRCFuryEditorUtils.Warn(
            "This legacy action has no effect. Use Change SPS Channels instead.");
    }
}
