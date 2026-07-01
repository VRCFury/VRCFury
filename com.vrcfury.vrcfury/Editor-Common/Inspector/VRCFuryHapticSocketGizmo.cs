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

        static void DrawSocketGizmo(Vector3 worldPos, Quaternion worldRot, VRCFuryHapticSocket.AddLight type, string name, VFGameObject owner) {
            var orange = new Color(1f, 0.5f, 0);

            var discColor = orange;

            var text = "SPS Socket";
            if (!string.IsNullOrWhiteSpace(name)) text += $" '{name}'";
            if (!BuildTargetUtils.IsDesktop()) {
                text += " (Deformation Disabled)\nThis is an Android/iOS project!";
                discColor = Color.red;
            } else if (type == VRCFuryHapticSocket.AddLight.Hole) {
                text += " (Hole)\nPlug follows orange arrow";
            } else if (type == VRCFuryHapticSocket.AddLight.Ring) {
                text += " (Ring)\nSPS enters either direction\nDPS/TPS only follow orange arrow";
            } else if (type == VRCFuryHapticSocket.AddLight.RingOneWay) {
                text += " (One-Way Ring)\nPlug follows orange arrow";
            } else {
                text += " (Deformation disabled)";
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

            if (owner.IsSelected()) {
                VRCFuryGizmoUtils.DrawText(
                    worldPos,
                    "\n" + text,
                    Color.gray,
                    true,
                    true
                );
            }

            Gizmos.color = Color.clear;
            Gizmos.DrawSphere(worldPos, 0.04f);
        }

        static void DrawGizmo(Vector3 worldPos, Quaternion worldRot, VRCFuryHapticSocket.AddLight type, string name, bool radiusOffset, VFGameObject owner) {
            if (!radiusOffset) {
                DrawSocketGizmo(worldPos, worldRot, type, name, owner);
                return;
            }

            var offsetPos = worldPos + worldRot * (Vector3.up * 0.04f);
            DrawRadiusOffsetPlane(worldPos, worldRot);
            DrawSocketGizmo(offsetPos, worldRot, type, name, owner);
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

            data.guidedPathStops = socket.guidedPathStops
                .Where(stop => stop != null && stop.transform != null)
                .Select(stop => new VRCFurySocketGizmo.GuidedPathStopData {
                    transform = stop.transform
                })
                .ToList();

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
                    data.name,
                    data.useRadiusOffset,
                    owner
                );
                var previousPos = worldStart + (data.useRadiusOffset ? worldRotation * (Vector3.up * 0.04f) : Vector3.zero);
                foreach (var stop in guidedPathStops) {
                    if (stop == null || stop.transform == null) continue;
                    var stopPos = stop.transform.position;
                    var stopRot = stop.transform.rotation;
                    var offsetStopPos = stopPos + (data.useRadiusOffset ? stopRot * (Vector3.up * 0.04f) : Vector3.zero);
                    VRCFuryGizmoUtils.DrawArrow(previousPos, offsetStopPos, new Color(1f, 0.5f, 0));
                    DrawGizmo(stopPos, stopRot, VRCFuryHapticSocket.AddLight.RingOneWay, "", data.useRadiusOffset, owner);
                    previousPos = offsetStopPos;
                }
                return;
            }

            DrawGizmo(worldStart, worldRotation, data.type, data.name, data.useRadiusOffset, owner);
        }

        [DrawGizmo(GizmoType.Selected | GizmoType.NonSelected | GizmoType.Pickable)]
        static void DrawGizmo2(VRCFuryHapticSocket socket, GizmoType gizmoType) {
            DrawGizmo(socket.owner(), BuildGizmoData(socket));
        }
    }
}
