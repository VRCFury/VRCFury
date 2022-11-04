using UnityEditor;
using UnityEngine;

namespace VF.Inspector {
    public class VRCFuryGizmoUtils {
        public static void DrawArrow(
            Vector3 worldStart,
            Vector3 worldEnd,
            Color color
        ) {
            var cbak = Gizmos.color;
            try {
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
            } finally {
                Gizmos.color = cbak;
            }
        }

        public static void DrawCapsule(
            Vector3 worldPos,
            Quaternion worldRot,
            float worldLength,
            float worldRadius,
            Color color
        ) {
            var cbak = Handles.color;
            try {
                Handles.color = color;
                HandlesUtil.DrawWireCapsule(worldPos, worldRot, worldLength, worldRadius);
            } finally {
                Handles.color = cbak;
            }
        }
        
        public static void DrawSphere(
            Vector3 worldPos,
            float worldScale,
            Color color
        ) {
            var cbak = Gizmos.color;
            try {
                Gizmos.color = color;
                Gizmos.DrawWireSphere(worldPos, worldScale);
            } finally {
                Gizmos.color = cbak;
            }
        }

        public static void DrawText(
            Vector3 worldPos,
            string text,
            Color color
        ) {
            var style = new GUIStyle(GUI.skin.label);
            style.alignment = TextAnchor.UpperCenter;
            style.normal.textColor = color;
            Handles.Label(worldPos, text, style);
        }
    }
}
