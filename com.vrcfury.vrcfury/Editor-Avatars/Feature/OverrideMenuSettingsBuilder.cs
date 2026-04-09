using UnityEditor;
using UnityEngine.UIElements;
using VF.Feature.Base;
using VF.Inspector;
using VF.Model.Feature;

namespace VF.Feature {
    [FeatureTitle("Override Menu Settings")]
    [FeatureOnlyOneAllowed]
    [FeatureRootOnly]
    internal class OverrideMenuSettingsBuilder : FeatureBuilder<OverrideMenuSettings> {

        [FeatureEditor]
        public static VisualElement Editor(SerializedProperty prop) {
            var c = new VisualElement();
            c.Add(VRCFuryEditorUtils.Prop(prop.FindPropertyRelative("nextText"), "'Next' Text"));
            c.Add(VRCFuryEditorUtils.Prop(prop.FindPropertyRelative("nextIcon"), "'Next' Icon"));
            return c;
        }
    }
}
