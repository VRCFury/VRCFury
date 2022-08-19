using System.Collections.Generic;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using VF.Model;

namespace VF.Inspector {
    [CustomEditor(typeof(OGBOrifice), true)]
    public class OGBOrificeEditor : Editor {
        public override VisualElement CreateInspectorGUI() {
            var self = (OGBOrifice)target;

            var container = new VisualElement();
            
            container.Add(new PropertyField(serializedObject.FindProperty("name"), "Name Override"));
            container.Add(new PropertyField(serializedObject.FindProperty("addLight"), "Add DPS Light"));

            return container;
        }
        
        [DrawGizmo(GizmoType.Selected | GizmoType.Active | GizmoType.InSelectionHierarchy)]
        static void DrawGizmo(OGBOrifice scr, GizmoType gizmoType) {
            var autoInfo = OGBOrifice.GetInfoFromLights(scr.gameObject);
            var forward = Vector3.forward;
            if (autoInfo != null) {
                forward = autoInfo.Item1;
            }
            
            var oscDepth = 0.25f;
            var tightRot = Quaternion.LookRotation(forward) * Quaternion.LookRotation(Vector3.up);
            var closeRadius = 0.1f;

            var c = Handles.color;
            try {
                Handles.color = Color.red;
                OGBPenetratorEditor.DrawCapsule(
                    scr.gameObject,
                    forward * -(oscDepth / 2),
                    tightRot,
                    oscDepth,
                    closeRadius
                );
                Handles.Label(scr.transform.position, "Entrance");
                Handles.Label(scr.transform.TransformPoint(forward * -(oscDepth / 2) / scr.transform.lossyScale.x), "Inside");
            } finally {
                Handles.color = c;
            }
        }
    }
}