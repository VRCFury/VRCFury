using UnityEditor;
using UnityEngine.UIElements;
using VF.Feature.Base;
using VF.Inspector;
using VF.Model.Feature;

namespace VF.Feature {
    public class SpsOptionsBuilder : FeatureBuilder<SpsOptions> {
        public override string GetEditorTitle() {
            return "SPS Options";
        }

        public override VisualElement CreateEditor(SerializedProperty prop) {
            var c = new VisualElement();
            c.Add(VRCFuryEditorUtils.Prop(prop.FindPropertyRelative("menuIcon"), "SPS Menu Icon Override"));
            c.Add(VRCFuryEditorUtils.Prop(prop.FindPropertyRelative("menuPath"), "SPS Menu Path Override (Default: SPS)"));
            c.Add(VRCFuryEditorUtils.Prop(prop.FindPropertyRelative("enableLightlessToggle"), "Enable Experimental SPSLL Toggle"));
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
