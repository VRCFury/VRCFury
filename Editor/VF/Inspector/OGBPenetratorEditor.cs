using System.Collections.Generic;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using VF.Model;

namespace VF.Inspector {
    [CustomEditor(typeof(OGBPenetrator), true)]
    public class OGBPenetratorEditor : Editor {
        public override VisualElement CreateInspectorGUI() {
            var self = (OGBPenetrator)target;

            var container = new VisualElement();
            
            container.Add(new PropertyField(serializedObject.FindProperty("name"), "Name Override"));
            container.Add(new PropertyField(serializedObject.FindProperty("length"), "Length Override"));
            container.Add(new PropertyField(serializedObject.FindProperty("radius"), "Radius Override"));

            return container;
        }
        
        [DrawGizmo(GizmoType.Selected | GizmoType.Active | GizmoType.InSelectionHierarchy)]
        static void DrawGizmo(OGBPenetrator scr, GizmoType gizmoType) {
            var size = scr.GetSize();
            var worldLength = size.Item1;
            var worldRadius = size.Item2;
            var forward = new Vector3(0, 0, 1);
            var tightPos = forward * (worldLength / 2);
            var tightRot = Quaternion.LookRotation(forward) * Quaternion.LookRotation(Vector3.up);

            var worldPosTip = scr.transform.TransformPoint(forward * worldLength / scr.transform.lossyScale.x);

            var c = Handles.color;
            try {
                Handles.color = Color.red;
                DrawCapsule(scr.gameObject, tightPos, tightRot, worldLength, worldRadius);
                Handles.Label(worldPosTip, "Tip");
            } finally {
                Handles.color = c;
            }
        }

        public static void DrawCapsule(
            GameObject obj,
            Vector3 localPositionInWorldScale,
            Quaternion localRotation,
            float worldLength,
            float worldRadius
        ) {
            var worldPos = obj.transform.TransformPoint(localPositionInWorldScale / obj.transform.lossyScale.x);
            var worldRot = obj.transform.rotation * localRotation;
            HandlesUtil.DrawWireCapsule(worldPos, worldRot, worldLength, worldRadius);
        }
    }
}
