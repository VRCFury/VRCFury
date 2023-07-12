using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using VF.Component;
using VF.Model;

namespace VF.Inspector {
    [CustomEditor(typeof(VRCFuryGlobalCollider), true)]
    public class VRCFGlobalColliderEditor : VRCFuryComponentEditor<VRCFuryGlobalCollider> {
        [DrawGizmo(GizmoType.Selected | GizmoType.Active | GizmoType.InSelectionHierarchy)]
        static void DrawGizmo(VRCFuryGlobalCollider collider, GizmoType gizmoType) {
            var transform = collider.GetTransform();
            var worldHeight = collider.height * transform.lossyScale.x;
            var worldRadius = collider.radius * transform.lossyScale.x;

            VRCFuryGizmoUtils.DrawCapsule(
                transform.position,
                transform.rotation * Quaternion.Euler(90,0,0),
                worldHeight,
                worldRadius,
                Color.blue
            );
        }
        
        public override VisualElement CreateEditor(SerializedObject serializedObject, VRCFuryGlobalCollider target) {
            var container = new VisualElement();
            
            container.Add(VRCFuryEditorUtils.Prop(serializedObject.FindProperty("rootTransform"), "Root Transform Override"));
            container.Add(VRCFuryEditorUtils.Prop(serializedObject.FindProperty("radius"), "Radius"));
            container.Add(VRCFuryEditorUtils.Prop(serializedObject.FindProperty("height"), "Height"));

            return container;
        }
    }
}
