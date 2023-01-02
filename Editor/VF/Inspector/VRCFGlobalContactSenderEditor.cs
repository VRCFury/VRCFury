using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using VF.Model;

namespace VF.Inspector {
    [CustomEditor(typeof(VRCFGlobalContactSender), true)]
    public class VRCFGlobalContactSenderEditor : Editor {
        [DrawGizmo(GizmoType.Selected | GizmoType.Active | GizmoType.InSelectionHierarchy)]
        static void DrawGizmo(VRCFGlobalContactSender contact, GizmoType gizmoType) {
            var worldRadius = contact.radius * contact.transform.lossyScale.x;

            VRCFuryGizmoUtils.DrawCapsule(
                contact.transform.position,
                Quaternion.identity,
                0,
                worldRadius,
                Color.blue
            );
        }
        
        public override VisualElement CreateInspectorGUI() {
            var self = (VRCFGlobalContactSender)target;

            var container = new VisualElement();
            
            container.Add(VRCFuryEditorUtils.Prop(serializedObject.FindProperty("radius"), "Radius"));

            return container;
        }
    }
}
