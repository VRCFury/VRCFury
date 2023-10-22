using UnityEditor;
using UnityEngine.UIElements;
using VF.Feature.Base;
using VF.Inspector;
using VF.Model.Feature;

namespace VF.Feature {
    public class OverrideMenuSettingsBuilder : FeatureBuilder<OverrideMenuSettings> {
        public override string GetEditorTitle() {
            return "Override Menu Settings";
        }

        public override VisualElement CreateEditor(SerializedProperty prop) {
            var c = new VisualElement();
            c.Add(VRCFuryEditorUtils.Prop(prop.FindPropertyRelative("nextText"), "'Next' Text"));
            c.Add(VRCFuryEditorUtils.Prop(prop.FindPropertyRelative("nextIcon"), "'Next' Icon"));
            return c;
        }

        public override bool AvailableOnProps() {
            return false;
        }
        
        public override bool OnlyOneAllowed() {
            return true;
        }
    }
}
