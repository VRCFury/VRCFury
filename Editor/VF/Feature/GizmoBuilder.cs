using UnityEditor;
using UnityEngine.UIElements;
using VF.Feature.Base;
using VF.Inspector;
using VF.Model.Feature;

namespace VF.Feature {
    public class GizmoBuilder : FeatureBuilder<Gizmo> {
        [FeatureBuilderAction]
        public void Apply() {
        }
        
        public override string GetEditorTitle() {
            return "Gizmo";
        }

        public override VisualElement CreateEditor(SerializedProperty prop) {
            var content = new VisualElement();
            content.Add(VRCFuryEditorUtils.Prop(prop.FindPropertyRelative("rotation"), "Rotation"));
            content.Add(VRCFuryEditorUtils.Prop(prop.FindPropertyRelative("text"), "Text"));
            content.Add(VRCFuryEditorUtils.Prop(prop.FindPropertyRelative("sphereRadius"), "Sphere Radius"));
            content.Add(VRCFuryEditorUtils.Prop(prop.FindPropertyRelative("arrowLength"), "Arrow Length"));
            return content;
        }
    }
}