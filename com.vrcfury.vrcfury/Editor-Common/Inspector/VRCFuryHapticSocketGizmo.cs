using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using VF.Builder.Haptics;
using VF.Component;
using VF.Utils;

namespace VF.Inspector {
    [CustomEditor(typeof(VRCFurySocketGizmo), true)]
    internal class VRCFuryHapticPlaySocketEditor : UnityEditor.Editor {
        [VFInit]
        private static void Init() {
            VRCFurySocketGizmo.EnableSceneLighting = () => {
                var sv = EditorWindowFinder.GetWindows<SceneView>().FirstOrDefault();
                if (sv != null) {
                    sv.sceneLighting = true;
                    sv.drawGizmos = true;
                }
            };
        }

        [DrawGizmo(GizmoType.Selected | GizmoType.NonSelected | GizmoType.Pickable)]
        static void DrawGizmo2(VRCFurySocketGizmo gizmo, GizmoType gizmoType) {
            if (!gizmo.show) return;
            if (gizmo.data == null) return;
            VRCFuryHapticSocketGizmo.DrawGizmo(gizmo.owner(), gizmo.data);
        }
    }

    internal static class VRCFuryHapticSocketGizmo {
        static Vector3 TransformLocalPoint(Vector3 origin, Quaternion rotation, Vector3 localPoint) {
            return origin + rotation * localPoint;
        }

        static Vector3 InverseTransformLocalPoint(Vector3 origin, Quaternion rotation, Vector3 worldPoint) {
            return Quaternion.Inverse(rotation) * (worldPoint - origin);
        }

        static Vector3 GetDefaultTangentOut(Vector3 start, Quaternion startRot, Vector3 end) {
            var distance = Vector3.Distance(start, end) * 0.5f;
            return start - (startRot * Vector3.forward) * distance;
        }

        static Vector3 GetDefaultTangentIn(Vector3 start, Vector3 end, Quaternion endRot) {
            var distance = Vector3.Distance(start, end) * 0.5f;
            return end + (endRot * Vector3.forward) * distance;
        }

        static void DrawRadiusOffsetPlane(Vector3 worldPos, Quaternion worldRot) {
            var worldUp = worldRot * Vector3.up;
            var worldRight = worldRot * Vector3.right * 0.02f;
            var worldForward = worldRot * Vector3.forward * 0.02f;
            var a = worldPos - worldRight - worldForward;
            var b = worldPos + worldRight - worldForward;
            var c = worldPos + worldRight + worldForward;
            var d = worldPos - worldRight + worldForward;
            var color = new Color(1f, 0.8f, 0.2f);
            VRCFuryGizmoUtils.DrawLine(a, b, color);
            VRCFuryGizmoUtils.DrawLine(b, c, color);
            VRCFuryGizmoUtils.DrawLine(c, d, color);
            VRCFuryGizmoUtils.DrawLine(d, a, color);
            VRCFuryGizmoUtils.DrawArrow(worldPos, worldPos + worldUp * 0.02f, color);
        }

        static string GetSocketText(VRCFuryHapticSocket.AddLight type, string name) {
            var orange = new Color(1f, 0.5f, 0);
            var text = "SPS Socket";
            if (!string.IsNullOrWhiteSpace(name)) text += $" '{name}'";
            if (!BuildTargetUtils.IsDesktop()) {
                text += " (Deformation Disabled)\nThis is an Android/iOS project!";
            } else if (type == VRCFuryHapticSocket.AddLight.Hole) {
                text += " (Hole)\nPlug follows orange arrow";
            } else if (type == VRCFuryHapticSocket.AddLight.Ring) {
                text += " (Ring)\nSPS enters either direction\nDPS/TPS only follow orange arrow";
            } else if (type == VRCFuryHapticSocket.AddLight.RingOneWay) {
                text += " (One-Way Ring)\nPlug follows orange arrow";
            } else {
                text += " (Deformation disabled)";
            }
            return text;
        }

        static void DrawSocketGizmo(Vector3 worldPos, Quaternion worldRot, VRCFuryHapticSocket.AddLight type) {
            var orange = new Color(1f, 0.5f, 0);
            var discColor = orange;
            if (!BuildTargetUtils.IsDesktop() || type == VRCFuryHapticSocket.AddLight.None) {
                discColor = Color.red;
            }

            var worldForward = worldRot * Vector3.forward;
            VRCFuryGizmoUtils.DrawDisc(worldPos, worldForward, 0.02f, discColor);
            VRCFuryGizmoUtils.DrawDisc(worldPos, worldForward, 0.04f, discColor);
            if (type == VRCFuryHapticSocket.AddLight.RingOneWay) {
                VRCFuryGizmoUtils.DrawArrow(
                    worldPos + worldForward * 0.05f,
                    worldPos + worldForward * -0.05f,
                    orange
                );
            } else if (type == VRCFuryHapticSocket.AddLight.Ring) {
                VRCFuryGizmoUtils.DrawArrow(
                    worldPos,
                    worldPos + worldForward * -0.05f,
                    orange
                );
                VRCFuryGizmoUtils.DrawArrow(
                    worldPos,
                    worldPos + worldForward * 0.05f,
                    Color.white
                );
            } else {
                VRCFuryGizmoUtils.DrawArrow(
                    worldPos + worldForward * 0.1f,
                    worldPos,
                    orange
                );
            }

            Gizmos.color = Color.clear;
            Gizmos.DrawSphere(worldPos, 0.04f);
        }

        static void DrawGizmo(Vector3 worldPos, Quaternion worldRot, VRCFuryHapticSocket.AddLight type, bool radiusOffset) {
            if (!radiusOffset) {
                DrawSocketGizmo(worldPos, worldRot, type);
                return;
            }

            var offsetPos = worldPos + worldRot * (Vector3.up * 0.04f);
            DrawRadiusOffsetPlane(worldPos, worldRot);
            DrawSocketGizmo(offsetPos, worldRot, type);
        }

        public static VRCFurySocketGizmo.SocketGizmoData BuildGizmoData(VRCFuryHapticSocket socket) {
            var (lightType, localPosition, localRotation) = VRCFuryHapticSocketEditor.GetInfoFromLightsOrComponent(socket);
            var handTouchZoneSize = VRCFuryHapticSocketEditor.GetHandTouchZoneSize(socket);
            var data = new VRCFurySocketGizmo.SocketGizmoData {
                type = lightType,
                pos = localPosition,
                rot = localRotation,
                useRadiusOffset = socket.useRadiusOffset,
                name = HapticUtils.GetPreferredId(
                    socket,
                    s => s.name,
                    s => HapticUtils.GetFallbackId(s.owner())
                ),
                hasHandTouchZone = handTouchZoneSize != null,
                handTouchZoneLength = handTouchZoneSize?.Item1 ?? 0,
                handTouchZoneRadius = handTouchZoneSize?.Item2 ?? 0
            };

            var guidedPathStops = socket.guidedPathStops
                .Where(stop => stop != null && stop.transform != null)
                .ToList();
            for (var i = 0; i < guidedPathStops.Count; i++) {
                var stop = guidedPathStops[i];
                data.guidedPathStops.Add(new VRCFurySocketGizmo.GuidedPathStopData {
                    transform = stop.transform,
                    customizeTangentIn = stop.customizeTangentIn,
                    customizeTangentOut = stop.customizeTangentOut,
                    tangentIn = stop.tangentIn,
                    tangentOut = stop.tangentOut
                });
            }

            return data;
        }

        public static void DrawGizmo(VFGameObject owner, VRCFurySocketGizmo.SocketGizmoData data) {
            var guidedPathStops = data.guidedPathStops ?? new List<VRCFurySocketGizmo.GuidedPathStopData>();
            var worldStart = owner.TransformPoint(data.pos);
            var worldRotation = owner.worldRotation * data.rot;
            var localForward = data.rot * Vector3.forward;

            if (data.hasHandTouchZone) {
                var worldForward = owner.worldRotation * localForward;
                var worldEnd = worldStart - worldForward * data.handTouchZoneLength;
                VRCFuryGizmoUtils.DrawCapsule(
                    worldStart,
                    worldEnd,
                    data.handTouchZoneRadius,
                    Color.gray
                );
            }

            if (guidedPathStops.Count > 0 && data.type != VRCFuryHapticSocket.AddLight.None) {
                DrawGizmo(
                    worldStart,
                    worldRotation,
                    VRCFuryHapticSocket.AddLight.RingOneWay,
                    data.useRadiusOffset
                );
                if (owner.IsSelected()) {
                    VRCFuryGizmoUtils.DrawText(
                        worldStart,
                        "\n" + GetSocketText(VRCFuryHapticSocket.AddLight.RingOneWay, data.name),
                        Color.gray,
                        true,
                        true
                    );
                }
                var previousPos = worldStart + (data.useRadiusOffset ? worldRotation * (Vector3.up * 0.04f) : Vector3.zero);
                var previousRot = worldRotation;
                for (var i = 0; i < guidedPathStops.Count; i++) {
                    var stop = guidedPathStops[i];
                    if (stop == null || stop.transform == null) continue;
                    var stopPos = stop.transform.position;
                    var stopRot = stop.transform.rotation;
                    var isLast = i == guidedPathStops.Count - 1;
                    var stopType = isLast && data.type == VRCFuryHapticSocket.AddLight.Hole
                        ? VRCFuryHapticSocket.AddLight.Hole
                        : VRCFuryHapticSocket.AddLight.RingOneWay;
                    var previousOut = stop.customizeTangentOut
                        ? TransformLocalPoint(previousPos, previousRot, stop.tangentOut)
                        : GetDefaultTangentOut(previousPos, previousRot, stopPos);
                    var currentIn = stop.customizeTangentIn
                        ? TransformLocalPoint(stopPos, stopRot, stop.tangentIn)
                        : GetDefaultTangentIn(previousPos, stopPos, stopRot);
                    Handles.DrawBezier(
                        previousPos,
                        stopPos,
                        previousOut,
                        currentIn,
                        new Color(1f, 0.5f, 0),
                        null,
                        2f
                    );
                    DrawGizmo(stopPos, stopRot, stopType, false);
                    previousPos = stopPos;
                    previousRot = stopRot;
                }
                return;
            }

            DrawGizmo(worldStart, worldRotation, data.type, data.useRadiusOffset);
            if (owner.IsSelected()) {
                VRCFuryGizmoUtils.DrawText(
                    worldStart,
                    "\n" + GetSocketText(data.type, data.name),
                    Color.gray,
                    true,
                    true
                );
            }
        }

        internal static void DrawEditableTangents(VRCFuryHapticSocket socket) {
            if (socket == null) return;
            var guidedPathStops = socket.guidedPathStops ?? new List<VRCFuryHapticSocket.GuidedPathStop>();
            var previousPos = socket.owner().worldPosition + (socket.useRadiusOffset ? socket.owner().worldRotation * (Vector3.up * 0.04f) : Vector3.zero);
            var previousRot = socket.owner().worldRotation * Quaternion.Euler(socket.rotation);
            for (var i = 0; i < guidedPathStops.Count; i++) {
                var stop = guidedPathStops[i];
                if (stop == null || stop.transform == null) continue;
                var stopPos = stop.transform.position;
                var stopRot = stop.transform.rotation;
                if (stop.customizeTangentOut) {
                    EditorGUI.BeginChangeCheck();
                    var tangentOutWorld = TransformLocalPoint(previousPos, previousRot, stop.tangentOut);
                    var newTangentOutWorld = Handles.PositionHandle(tangentOutWorld, Quaternion.identity);
                    if (EditorGUI.EndChangeCheck()) {
                        Undo.RecordObject(socket, "Move SPS Tangent Out");
                        stop.tangentOut = InverseTransformLocalPoint(previousPos, previousRot, newTangentOutWorld);
                        EditorUtility.SetDirty(socket);
                    }
                }

                if (stop.customizeTangentIn) {
                    EditorGUI.BeginChangeCheck();
                    var tangentInWorld = TransformLocalPoint(stopPos, stopRot, stop.tangentIn);
                    var newTangentInWorld = Handles.PositionHandle(tangentInWorld, Quaternion.identity);
                    if (EditorGUI.EndChangeCheck()) {
                        Undo.RecordObject(socket, "Move SPS Tangent In");
                        stop.tangentIn = InverseTransformLocalPoint(stopPos, stopRot, newTangentInWorld);
                        EditorUtility.SetDirty(socket);
                    }
                }

                previousPos = stopPos;
                previousRot = stopRot;
            }
        }

        [DrawGizmo(GizmoType.Selected | GizmoType.NonSelected | GizmoType.Pickable)]
        static void DrawGizmo2(VRCFuryHapticSocket socket, GizmoType gizmoType) {
            DrawGizmo(socket.owner(), BuildGizmoData(socket));
        }
    }
}
