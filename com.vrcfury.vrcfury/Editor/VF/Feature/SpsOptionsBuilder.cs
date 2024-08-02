using UnityEditor;
using UnityEngine.UIElements;
using VF.Feature.Base;
using VF.Inspector;
using VF.Model.Feature;

namespace VF.Feature {
    internal class SpsOptionsBuilder : FeatureBuilder<SpsOptions> {
        public override string GetEditorTitle() {
            return "SPS Options";
        }

        public override VisualElement CreateEditor(SerializedProperty prop) {
            var c = new VisualElement();
            c.Add(VRCFuryEditorUtils.Prop(prop.FindPropertyRelative("menuIcon"), "SPS Menu Icon Override"));
            
            var pathProp = prop.FindPropertyRelative("menuPath");
            c.Add(MoveMenuItemBuilder.SelectButton(
                avatarObject,
                true,
                pathProp,
                append: () => "SPS",
                label: "SPS Menu Path Override (Default: SPS)",
                selectLabel: "Select"
            ));

            c.Add(VRCFuryEditorUtils.Prop(prop.FindPropertyRelative("saveSockets"), "Save Sockets Between Worlds"));
            return c;
        }

        public override bool AvailableOnRootOnly() {
            return true;
        }
        
        public override bool OnlyOneAllowed() {
            return true;
        }
    }
}
