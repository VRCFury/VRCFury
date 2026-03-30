using UnityEditor;
using UnityEngine.UIElements;
using VF.Feature.Base;
using VF.Inspector;
using VF.Model.Feature;

namespace VF.Feature {
    [FeatureTitle("Gizmo")]
    internal class GizmoBuilder : FeatureBuilder<Gizmo> {
        [FeatureEditor]
        public static VisualElement Editor(SerializedProperty prop) {
            var content = new VisualElement();
            content.Add(VRCFuryEditorUtils.Info("This adds an editor gizmo to the current object. Informational only, no changes are made to the avatar."));
            content.Add(VRCFuryEditorUtils.Prop(prop.FindPropertyRelative("rotation"), "Rotation"));
            content.Add(VRCFuryEditorUtils.Prop(prop.FindPropertyRelative("text"), "Text"));
            content.Add(VRCFuryEditorUtils.Prop(prop.FindPropertyRelative("sphereRadius"), "Sphere Radius"));
            content.Add(VRCFuryEditorUtils.Prop(prop.FindPropertyRelative("arrowLength"), "Arrow Length"));
            return content;
        }
    }
}
