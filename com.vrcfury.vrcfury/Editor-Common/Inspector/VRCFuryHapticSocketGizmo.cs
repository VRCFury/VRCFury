using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using VF.Builder.Haptics;
using VF.Component;
using VF.Injector;
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
        static Vector3 DrawColoredHandle(Vector3 position, Color color) {
            var oldColor = Handles.color;
            Handles.color = color;
            var size = HandleUtility.GetHandleSize(position) * 0.5f;
            var result = position;
            result = Handles.Slider(result, Vector3.right, size, Handles.ArrowHandleCap, 0);
            result = Handles.Slider(result, Vector3.up, size, Handles.ArrowHandleCap, 0);
            result = Handles.Slider(result, Vector3.forward, size, Handles.ArrowHandleCap, 0);
            Handles.color = oldColor;
            return result;
        }

        static Vector3 GetDefaultTangentOut(TransformData start, TransformData end) {
            var distance = Vector3.Distance(start.position, end.position) * 0.5f;
            return start.position - start.TransformDirection(Vector3.forward) * distance;
        }

        static Vector3 GetDefaultTangentIn(TransformData start, TransformData end) {
            var distance = Vector3.Distance(start.position, end.position) * 0.5f;
            return end.position + end.TransformDirection(Vector3.forward) * distance;
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

        static void DrawSocketGizmo(Vector3 worldPos, Quaternion worldRot, VRCFuryHapticSocket.AddLight type, Color primaryColor, Color secondaryColor) {
            var orange = new Color(1f, 0.5f, 0);
            var discColor = primaryColor;
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
                    primaryColor
                );
            } else if (type == VRCFuryHapticSocket.AddLight.Ring) {
                VRCFuryGizmoUtils.DrawArrow(
                    worldPos,
                    worldPos + worldForward * -0.05f,
                    primaryColor
                );
                VRCFuryGizmoUtils.DrawArrow(
                    worldPos,
                    worldPos + worldForward * 0.05f,
                    secondaryColor
                );
            } else {
                VRCFuryGizmoUtils.DrawArrow(
                    worldPos + worldForward * 0.1f,
                    worldPos,
                    primaryColor
                );
            }

            Gizmos.color = Color.clear;
            Gizmos.DrawSphere(worldPos, 0.04f);
        }

        static void DrawGizmo(Vector3 worldPos, Quaternion worldRot, VRCFuryHapticSocket.AddLight type, bool radiusOffset, Color primaryColor, Color secondaryColor) {
            if (!radiusOffset) {
                DrawSocketGizmo(worldPos, worldRot, type, primaryColor, secondaryColor);
                return;
            }

            var offsetPos = worldPos + worldRot * (Vector3.up * VRCFuryHapticSocketEditor.GizmoRadiusOffset);
            DrawRadiusOffsetPlane(worldPos, worldRot);
            DrawSocketGizmo(offsetPos, worldRot, type, primaryColor, secondaryColor);
        }

        internal static VRCFurySocketGizmo.SocketGizmoData BuildGizmoData(
            VRCFuryHapticSocket socket,
            VRCFuryHapticSocket.AddLight lightType,
            Tuple<float, float> handTouchZoneSize,
            Vector3 localPosition,
            Quaternion localRotation
        ) {
            var data = new VRCFurySocketGizmo.SocketGizmoData {
                type = lightType,
                legacyType = VRCFuryHapticSocketEditor.GetLegacyLightType(socket, lightType),
                pos = localPosition,
                rot = localRotation,
                useRadiusOffset = socket.useRadiusOffset,
                useLegacyLights = socket.useLights,
                overrideLegacyOffset = socket.overrideLegacyOffset,
                legacyOffsetLocal = socket.legacyOffsetLocal,
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
                    tangentInLocal = stop.tangentInLocal,
                    tangentOutLocal = stop.tangentOutLocal
                });
            }

            return data;
        }

        public static void DrawGizmo(VFGameObject owner, VRCFurySocketGizmo.SocketGizmoData data) {
            var guidedPathStops = data.guidedPathStops ?? new List<VRCFurySocketGizmo.GuidedPathStopData>();
            var socketTransformWorld = new TransformData(owner) * new TransformData(data.pos, data.rot);
            var worldStart = socketTransformWorld.position;
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
                    data.useRadiusOffset,
                    new Color(1f, 0.5f, 0),
                    Color.white
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
                var previousTransformWorld = socketTransformWorld.WithPosition(
                    worldStart + (data.useRadiusOffset
                        ? worldRotation * (Vector3.up * VRCFuryHapticSocketEditor.GizmoRadiusOffset)
                        : Vector3.zero)
                );
                for (var i = 0; i < guidedPathStops.Count; i++) {
                    var stop = guidedPathStops[i];
                    if (stop == null || stop.transform == null) continue;
                    TransformData stopTransformWorld = stop.transform;
                    var isLast = i == guidedPathStops.Count - 1;
                    var stopType = isLast && data.type == VRCFuryHapticSocket.AddLight.Hole
                        ? VRCFuryHapticSocket.AddLight.Hole
                        : VRCFuryHapticSocket.AddLight.RingOneWay;
                    var previousOut = stop.customizeTangentOut
                        ? previousTransformWorld.TransformPoint(stop.tangentOutLocal)
                        : GetDefaultTangentOut(previousTransformWorld, stopTransformWorld);
                    var currentIn = stop.customizeTangentIn
                        ? stopTransformWorld.TransformPoint(stop.tangentInLocal)
                        : GetDefaultTangentIn(previousTransformWorld, stopTransformWorld);
                    Handles.DrawBezier(
                        previousTransformWorld.position,
                        stopTransformWorld.position,
                        previousOut,
                        currentIn,
                        new Color(1f, 0.5f, 0),
                        null,
                        2f
                    );
                    DrawGizmo(
                        stopTransformWorld.position,
                        stopTransformWorld.rotation,
                        stopType,
                        false,
                        new Color(1f, 0.5f, 0),
                        Color.white
                    );
                    previousTransformWorld = stopTransformWorld;
                }
            } else {
                DrawGizmo(worldStart, worldRotation, data.type, data.useRadiusOffset, new Color(1f, 0.5f, 0), Color.white);
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

            if (data.useLegacyLights && data.overrideLegacyOffset) {
                var legacyOrigin = socketTransformWorld.TransformPoint(data.legacyOffsetLocal);
                DrawGizmo(legacyOrigin, worldRotation, data.legacyType, false, Color.yellow, Color.yellow);
                if (owner.IsSelected()) {
                    VRCFuryGizmoUtils.DrawText(
                        legacyOrigin,
                        "\nLegacy " + GetSocketText(data.legacyType, ""),
                        Color.yellow,
                        true,
                        true
                    );
                }
            }
        }

        internal static void DrawEditableTangents(VRCFuryHapticSocket socket) {
            if (socket == null) return;
            var guidedPathStops = socket.guidedPathStops ?? new List<VRCFuryHapticSocket.GuidedPathStop>();
            var owner = socket.owner();
            var socketTransformWorld = new TransformData(owner)
                                       * VRCFuryHapticSocketEditor.GetSocketTransformLocal(socket).Item2;
            var previousTransformWorld = socketTransformWorld.WithPosition(
                socketTransformWorld.position
                + (socket.useRadiusOffset
                    ? socketTransformWorld.TransformDirection(
                        Vector3.up * VRCFuryHapticSocketEditor.GizmoRadiusOffset
                    )
                    : Vector3.zero)
            );
            for (var i = 0; i < guidedPathStops.Count; i++) {
                var stop = guidedPathStops[i];
                if (stop == null || stop.transform == null) continue;
                if (stop.customizeTangentOut) {
                    EditorGUI.BeginChangeCheck();
                    var tangentOutWorld = previousTransformWorld.TransformPoint(stop.tangentOutLocal);
                    var newTangentOutWorld = DrawColoredHandle(tangentOutWorld, new Color(0.7f, 0.3f, 1f));
                    if (EditorGUI.EndChangeCheck()) {
                        Undo.RecordObject(socket, "Move SPS Tangent Out");
                        stop.tangentOutLocal = previousTransformWorld.InverseTransformPoint(newTangentOutWorld);
                        EditorUtility.SetDirty(socket);
                    }
                }

                if (stop.customizeTangentIn) {
                    EditorGUI.BeginChangeCheck();
                    var tangentInWorld = stop.transform.TransformPoint(stop.tangentInLocal);
                    var newTangentInWorld = DrawColoredHandle(tangentInWorld, new Color(0.7f, 0.3f, 1f));
                    if (EditorGUI.EndChangeCheck()) {
                        Undo.RecordObject(socket, "Move SPS Tangent In");
                        stop.tangentInLocal = stop.transform.InverseTransformPoint(newTangentInWorld);
                        EditorUtility.SetDirty(socket);
                    }
                }

                previousTransformWorld = stop.transform;
            }
        }

        internal static void DrawEditableLegacyOffset(VRCFuryHapticSocket socket) {
            if (socket == null) return;
            if (!socket.useLights) return;
            if (!socket.overrideLegacyOffset) return;

            var owner = socket.owner();
            var socketTransformWorld = new TransformData(owner)
                                       * VRCFuryHapticSocketEditor.GetSocketTransformLocal(socket).Item2;

            EditorGUI.BeginChangeCheck();
            var offsetWorld = socketTransformWorld.TransformPoint(socket.legacyOffsetLocal);
            var newOffsetWorld = DrawColoredHandle(offsetWorld, Color.yellow);
            if (EditorGUI.EndChangeCheck()) {
                Undo.RecordObject(socket, "Move SPS Legacy Light Offset");
                socket.legacyOffsetLocal = socketTransformWorld.InverseTransformPoint(newOffsetWorld);
                EditorUtility.SetDirty(socket);
            }
        }

        [DrawGizmo(GizmoType.Selected | GizmoType.NonSelected | GizmoType.Pickable)]
        static void DrawGizmo2(VRCFuryHapticSocket socket, GizmoType gizmoType) {
            var copy = VRCFuryComponentEditor.CreateUpgradedClone(socket, out var cloneObject);
            try {
                var baker = VRCFuryPerFrameInjector.GetPerFrameInjector(socket.owner())
                    .GetService<VRCFuryHapticSocketBaker>();
                DrawGizmo(socket.owner(), baker.GetGizmoData(copy));
            } finally {
                UnityEngine.Object.DestroyImmediate(cloneObject);
            }
        }
    }
}
