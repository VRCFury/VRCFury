using UnityEngine.UIElements;
using VF.Feature.Base;
using VF.Inspector;
using VF.Model.Feature;

namespace VF.Feature {
    [FeatureTitle("Slot 4 Fix")]
    [FeatureHideInMenu]
    internal class Slot4FixBuilder : FeatureBuilder<Slot4Fix> {

        [FeatureEditor]
        public static VisualElement Editor() {
            var content = new VisualElement();
            content.Add(VRCFuryEditorUtils.Error("This feature is deprecated and now does nothing. The slot 4 bug has been fixed in unity 2022."));
            return content;
        }
    }
}
