using System;
using UnityEditor;
using UnityEditor.Graphs;
using UnityEngine;
using UnityEngine.Rendering;

namespace VF.Inspector {
    public class VRCFuryGizmoUtils {
        public static void DrawArrow(
            Vector3 worldStart,
            Vector3 worldEnd,
            Color color
        ) {
            WithHandles(() => {
                Handles.color = color;
                var dir = worldEnd - worldStart;
                var length = dir.magnitude;
                Handles.DrawLine(worldStart, worldEnd);
                var capSize = length / 4;
                Handles.ConeHandleCap(0, worldEnd - dir.normalized * capSize * 0.7f, Quaternion.LookRotation(dir), capSize, EventType.Repaint);
            });
        }

        public static void DrawCapsule(
            Vector3 worldPos,
            Quaternion worldRot,
            float worldLength,
            float worldRadius,
            Color color
        ) {
            WithHandles(() => {
                Handles.color = color;
                HandlesUtil.DrawWireCapsule(worldPos, worldRot, worldLength, worldRadius);
            });
        }
        
        public static void DrawSphere(
            Vector3 worldPos,
            float worldRadius,
            Color color
        ) {
            WithHandles(() => {
                Handles.color = color;
                Handles.DrawWireDisc(worldPos, Vector3.forward, worldRadius);
                Handles.DrawWireDisc(worldPos, Vector3.up, worldRadius);
                Handles.DrawWireDisc(worldPos, Vector3.right, worldRadius);
            });
        }

        public static void DrawText(
            Vector3 worldPos,
            string text,
            Color color,
            bool worldSize = false,
            bool left = false
        ) {
            var style = new GUIStyle(GUI.skin.label);
            style.alignment = left ? TextAnchor.UpperLeft : TextAnchor.UpperCenter;
            style.normal.textColor = color;
            //style.fontSize = 12;
            if (worldSize) {
                style.fontSize = (int)(1.5 / HandleUtility.GetHandleSize(worldPos));
                if (style.fontSize < 8) return;
                if (style.fontSize > 40) style.fontSize = 40;
            }
            WithHandles(() => {
                Handles.Label(worldPos, text, style);                
            });
        }

        public static void WithHandles(Action func, Color? color = null) {
            var cbak = Handles.color;
            var mbak = Handles.matrix;
            var zbak = Handles.zTest;
            try {
                Handles.color = color ?? Color.white;
                Handles.matrix = Matrix4x4.identity;
                Handles.zTest = CompareFunction.Always;
                func.Invoke();
            } finally {
                Handles.color = cbak;
                Handles.matrix = mbak;
                Handles.zTest = zbak;
            }
        }
    }
}
