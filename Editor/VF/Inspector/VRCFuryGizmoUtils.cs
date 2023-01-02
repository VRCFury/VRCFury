using System;
using UnityEditor;
using UnityEngine;

namespace VF.Inspector {
    public class VRCFuryGizmoUtils {
        public static void DrawArrow(
            Vector3 worldStart,
            Vector3 worldEnd,
            Color color
        ) {
            WithGizmos(() => {
                Gizmos.color = color;
                var dir = worldEnd - worldStart;
                var length = dir.magnitude;
                var up = Vector3.Cross(dir, Vector3.up).normalized;
                var left = Vector3.Cross(dir, Vector3.left).normalized;
                var a = worldEnd - dir * 0.1f + up * length * 0.1f;
                var b = worldEnd - dir * 0.1f + -up * length * 0.1f;
                var c = worldEnd - dir * 0.1f + left * length * 0.1f;
                var d = worldEnd - dir * 0.1f + -left * length * 0.1f;
                Gizmos.DrawLine(worldStart, worldEnd);
                Gizmos.DrawLine(worldEnd, a);
                Gizmos.DrawLine(worldEnd, b);
                Gizmos.DrawLine(worldEnd, c);
                Gizmos.DrawLine(worldEnd, d);
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
            WithGizmos(() => {
                Gizmos.color = color;
                Gizmos.DrawWireSphere(worldPos, worldRadius);
            });
        }

        public static void DrawText(
            Vector3 worldPos,
            string text,
            Color color
        ) {
            var style = new GUIStyle(GUI.skin.label);
            style.alignment = TextAnchor.UpperCenter;
            style.normal.textColor = color;
            WithHandles(() => {
                Handles.Label(worldPos, text, style);                
            });
        }

        private static void WithHandles(Action func) {
            var cbak = Handles.color;
            var mbak = Handles.matrix;
            try {
                Handles.color = Color.white;
                Handles.matrix = Matrix4x4.identity;
                func.Invoke();
            } finally {
                Handles.color = cbak;
                Handles.matrix = mbak;
            }
        }
        private static void WithGizmos(Action func) {
            var cbak = Gizmos.color;
            try {
                Gizmos.color = Color.white;
                func.Invoke();
            } finally {
                Gizmos.color = cbak;
            }
        }
    }
}
