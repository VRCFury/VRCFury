using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using VF.Component;

namespace VF.Inspector {
    [CustomEditor(typeof(VRCFuryGlobalCollider), true)]
    internal class VRCFGlobalColliderEditor : VRCFuryComponentEditor<VRCFuryGlobalCollider> {
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

        protected override VisualElement CreateEditor(SerializedObject serializedObject, VRCFuryGlobalCollider target) {
            var container = new VisualElement();
            
            container.Add(VRCFuryEditorUtils.Info(
                "This will add a collider which can be used to interact with other player's physbones and trigger haptics.\n\n" +
                "Note: This steals one of the colliders from your avatar's fingers by default. If you don't need to interact with physbones," +
                " you can use a VRCFury Haptic Touch Sender instead, which can only trigger haptics and does  not steal a finger."
            ));
            container.Add(VRCFuryEditorUtils.Prop(serializedObject.FindProperty("rootTransform"), "Root Transform Override"));
            container.Add(VRCFuryEditorUtils.Prop(serializedObject.FindProperty("radius"), "Radius"));
            container.Add(VRCFuryEditorUtils.Prop(serializedObject.FindProperty("height"), "Height"));
            container.Add(VRCFuryEditorUtils.Prop(
                serializedObject.FindProperty("colliderOverride"),
                label: "Collider Override",
                tooltip: "Only hand and finger colliders affect physbones, but other colliders are available here for advanced setups."
            ));

            return container;
        }
    }
}
