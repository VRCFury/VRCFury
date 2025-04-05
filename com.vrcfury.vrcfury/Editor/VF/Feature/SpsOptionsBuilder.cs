using UnityEditor;
using UnityEngine.UIElements;
using VF.Builder;
using VF.Feature.Base;
using VF.Inspector;
using VF.Model.Feature;

namespace VF.Feature {
    [FeatureTitle("SPS Options")]
    [FeatureOnlyOneAllowed]
    [FeatureRootOnly]
    internal class SpsOptionsBuilder : FeatureBuilder<SpsOptions> {

        [FeatureEditor]
        public static VisualElement Editor(SerializedProperty prop, VFGameObject avatarObject) {
            var c = new VisualElement();
            c.Add(VRCFuryEditorUtils.Prop(prop.FindPropertyRelative("menuIcon"), "SPS Menu Icon Override"));
            
            var pathProp = prop.FindPropertyRelative("menuPath");
            c.Add(MoveMenuItemBuilder.SelectButton(
                avatarObject,
                null,
                true,
                pathProp,
                append: () => "SPS",
                label: "SPS Menu Path Override (Default: SPS)",
                selectLabel: "Select"
            ));

            c.Add(VRCFuryEditorUtils.Prop(prop.FindPropertyRelative("saveSockets"), "Save Sockets Between Worlds"));
            return c;
        }
    }
}
