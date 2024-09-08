using System;
using UnityEditor;
using UnityEditor.Graphs;
using UnityEngine;
using UnityEngine.Rendering;

namespace VF.Inspector {
    internal static class VRCFuryGizmoUtils {

        public static void DrawDisc(
            Vector3 worldCenter,
            Vector3 worldForward,
            float worldRadius,
            Color color
        ) {
            VRCFuryGizmoUtils.WithHandles(() => {
                Handles.color = color;
                Handles.DrawWireDisc(worldCenter, worldForward, worldRadius);
            });
        }
        
        public static void DrawLine(
            Vector3 worldStart,
            Vector3 worldEnd,
            Color color
        ) {
            WithHandles(() => {
                Handles.color = color;
                Handles.DrawLine(worldStart, worldEnd);
            });
        }
        
        public static void DrawArrow(
            Vector3 worldStart,
            Vector3 worldEnd,
            Color color
        ) {
            DrawLine(worldStart, worldEnd, color);
            WithHandles(() => {
                Handles.color = color;
                var dir = worldEnd - worldStart;
                var length = dir.magnitude;
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
        
        public static void DrawCappedCylinder(
            Vector3 worldStart,
            Vector3 worldEnd,
            float worldRadius,
            Color color
        ) {
            var worldLength = (worldEnd - worldStart).magnitude;
            var worldForward = (worldEnd - worldStart).normalized;
            var worldRight = Vector3.Cross(worldForward, Vector3.up).normalized;
            if (worldRight.magnitude == 0) worldRight = Vector3.right;
            var worldUp = Vector3.Cross(worldRight, worldForward).normalized;

            var cylinderLength = worldLength - worldRadius;
            var cylinderEnd = worldStart + worldForward * cylinderLength;
            
            DrawLine(worldStart + worldRight * worldRadius, worldStart - worldRight * worldRadius, Color.gray);
            DrawLine(worldStart + worldUp * worldRadius, worldStart - worldUp * worldRadius, Color.gray);

            DrawLine(worldStart + worldRight * worldRadius, cylinderEnd + worldRight * worldRadius, color);
            DrawLine(worldStart - worldRight * worldRadius, cylinderEnd - worldRight * worldRadius, color);
            DrawLine(worldStart + worldUp * worldRadius, cylinderEnd + worldUp * worldRadius, color);
            DrawLine(worldStart - worldUp * worldRadius, cylinderEnd - worldUp * worldRadius, color);
            DrawDisc(worldStart, worldForward, worldRadius, color);

            WithHandles(() => {
                Handles.color = color;
                Handles.DrawWireArc(cylinderEnd, worldUp, worldRight, 180f, worldRadius);
                Handles.DrawWireArc(cylinderEnd, -worldRight, worldUp, 180f, worldRadius);
            });
        }

        public static void DrawCapsule(
            Vector3 worldStart,
            Vector3 worldEnd,
            float worldRadius,
            Color color
        ) {
            var mid = worldStart + (worldEnd - worldStart) / 2;
            var rot = Quaternion.FromToRotation(Vector3.up, worldEnd - worldStart);
            DrawCapsule(mid, rot, (worldEnd - worldStart).magnitude, worldRadius, color);
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
                if (style.fontSize < 8) style.fontSize = 8;
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
