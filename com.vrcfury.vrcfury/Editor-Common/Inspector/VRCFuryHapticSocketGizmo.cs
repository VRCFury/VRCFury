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
        private void OnSceneGUI() {
            if (!(target is VRCFurySocketGizmo gizmo) || !gizmo.show || gizmo.data?.guidedPathStops == null) return;
            VRCFuryHapticSocketGizmo.DrawEditableStops(gizmo.data.guidedPathStops
                .Where(stop => stop != null).Select(stop => stop.transform));
        }

        [VFInit]
        private static void Init() {
            VRCFurySocketGizmo.EnableSceneGizmos = () => {
                var sv = EditorWindowFinder.GetWindows<SceneView>().FirstOrDefault();
                if (sv != null) sv.drawGizmos = true;
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
        internal static void DrawEditableStops(IEnumerable<Transform> stops) {
            foreach (var stop in stops.Take(SpsTags.MaxUserPathStops).Where(stop => stop != null).Distinct()) {
                VRCFuryGizmoUtils.DrawTransformHandle(stop, "Transform SPS Path Stop");
            }
        }

        internal static Vector3 GetDefaultTangentOut(TransformData start, TransformData end) {
            var distance = Vector3.Distance(start.position, end.position) * 0.5f;
            return start.position - start.TransformDirection(Vector3.forward) * distance;
        }

        internal static Vector3 GetDefaultTangentIn(TransformData start, TransformData end) {
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

        static string GetSocketText(VRCFuryHapticSocket.AddLight type, string name, string system = "SPS") {
            var orange = new Color(1f, 0.5f, 0);
            var text = "SPS Socket";
            if (!string.IsNullOrWhiteSpace(name)) text += $" '{name}'";
            if (!BuildTargetUtils.IsDesktop()) {
                text += " (Deformation Disabled)\nThis is an Android/iOS project!";
            } else if (type == VRCFuryHapticSocket.AddLight.Hole) {
                text += $" (Hole)\n{(system == "SPS3" ? "SPS3 plug" : "Plug")} follows orange arrow";
            } else if (type == VRCFuryHapticSocket.AddLight.Ring) {
                text += system == "SPS3"
                    ? " (Ring)\nSPS3 plug enters either direction"
                    : $" (Ring)\n{system} enters either direction\nDPS/TPS only follow orange arrow";
            } else if (type == VRCFuryHapticSocket.AddLight.RingOneWay) {
                text += $" (One-Way Ring)\n{(system == "SPS3" ? "SPS3 plug" : "Plug")} follows orange arrow";
            } else {
                text += " (Deformation disabled)";
            }
            return text;
        }

        static void DrawSocketGizmo(
            Vector3 worldPos,
            Quaternion worldRot,
            VRCFuryHapticSocket.AddLight type,
            Color primaryColor,
            Color secondaryColor,
            Color? ringArrowColor = null,
            bool drawCross = false
        ) {
            var orange = new Color(1f, 0.5f, 0);
            var markerColor = primaryColor;
            if (!BuildTargetUtils.IsDesktop() || type == VRCFuryHapticSocket.AddLight.None) {
                markerColor = Color.red;
            }

            var worldForward = worldRot * Vector3.forward;
            if (drawCross) {
                var right = worldRot * Vector3.right * 0.02f;
                var up = worldRot * Vector3.up * 0.02f;
                VRCFuryGizmoUtils.DrawLine(worldPos - right, worldPos + right, markerColor);
                VRCFuryGizmoUtils.DrawLine(worldPos - up, worldPos + up, markerColor);
            } else {
                VRCFuryGizmoUtils.DrawDisc(worldPos, worldForward, 0.02f, markerColor);
                VRCFuryGizmoUtils.DrawDisc(worldPos, worldForward, 0.04f, markerColor);
            }
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
                    ringArrowColor ?? primaryColor
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

        static void DrawGizmo(
            Vector3 worldPos,
            Quaternion worldRot,
            VRCFuryHapticSocket.AddLight type,
            bool radiusOffset,
            Color primaryColor,
            Color secondaryColor,
            bool drawCross
        ) {
            if (!radiusOffset) {
                DrawSocketGizmo(worldPos, worldRot, type, primaryColor, secondaryColor, drawCross: drawCross);
                return;
            }

            var offsetPos = worldPos + worldRot * (Vector3.up * VRCFuryHapticSocketEditor.GizmoRadiusOffset);
            DrawRadiusOffsetPlane(worldPos, worldRot);
            DrawSocketGizmo(offsetPos, worldRot, type, primaryColor, secondaryColor, drawCross: drawCross);
        }

        internal static VRCFurySocketGizmo.SocketGizmoData BuildGizmoData(
            VRCFuryHapticSocket socket,
            VRCFuryHapticSocket.AddLight lightType,
            Vector3 localPosition,
            Quaternion localRotation
        ) {
            var data = new VRCFurySocketGizmo.SocketGizmoData {
                type = lightType,
                legacyType = VRCFuryHapticSocketEditor.GetLegacyLightType(socket, lightType),
                pos = localPosition,
                rot = localRotation,
                useRadiusOffset = socket.useRadiusOffset,
                useLegacyLights = true,
                overrideLegacyOffset = socket.overrideLegacyOffset,
                legacyOffsetLocal = socket.legacyOffsetLocal,
                name = HapticUtils.GetPreferredId(
                    socket,
                    s => s.name,
                    s => HapticUtils.GetFallbackId(s.owner())
                )
            };

            var guidedPathStops = socket.guidedPathStops
                .Take(SpsTags.MaxUserPathStops)
                .Where(stop => stop != null && stop.transform != null)
                .ToList();
            for (var i = 0; i < guidedPathStops.Count; i++) {
                var stop = guidedPathStops[i];
                data.guidedPathStops.Add(new VRCFurySocketGizmo.GuidedPathStopData {
                    transform = stop.transform
                });
            }

            return data;
        }

        public static void DrawGizmo(VFGameObject owner, VRCFurySocketGizmo.SocketGizmoData data) {
            var socket = owner.GetComponent<VRCFuryHapticSocket>();
            var drawCross = owner.IsSelected() && !Tools.hidden && socket != null
                && (socket.plugEntryRadiusActions.Count > 0 || socket.depthActions2.Any(action =>
                    action.units != VRCFuryHapticSocket.DepthActionUnits.Plugs));
            var guidedPathStops = (data.guidedPathStops ?? new List<VRCFurySocketGizmo.GuidedPathStopData>())
                .Take(SpsTags.MaxUserPathStops).ToList();
            var socketTransformWorld = new TransformData(owner) * new TransformData(data.pos, data.rot);
            var worldStart = socketTransformWorld.position;
            var worldRotation = owner.worldRotation * data.rot;
            var showLegacyOffset = data.useLegacyLights && data.overrideLegacyOffset
                && (!EditorApplication.isPlaying || (data.legacyLights != null && data.legacyLights.activeInHierarchy));
            if (guidedPathStops.Count > 0 && data.type != VRCFuryHapticSocket.AddLight.None) {
                DrawGizmo(
                    worldStart,
                    worldRotation,
                    VRCFuryHapticSocket.AddLight.RingOneWay,
                    data.useRadiusOffset,
                    new Color(1f, 0.5f, 0),
                    Color.white,
                    drawCross
                );
                if (owner.IsSelected()) {
                    VRCFuryGizmoUtils.DrawText(
                        worldStart,
                        "\n" + GetSocketText(VRCFuryHapticSocket.AddLight.RingOneWay, data.name, showLegacyOffset ? "SPS3" : "SPS"),
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
                    var previousOut = GetDefaultTangentOut(previousTransformWorld, stopTransformWorld);
                    var currentIn = GetDefaultTangentIn(previousTransformWorld, stopTransformWorld);
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
                        Color.white,
                        drawCross
                    );
                    previousTransformWorld = stopTransformWorld;
                }
            } else {
                DrawGizmo(worldStart, worldRotation, data.type, data.useRadiusOffset, new Color(1f, 0.5f, 0), Color.white, drawCross);
                if (owner.IsSelected()) {
                    VRCFuryGizmoUtils.DrawText(
                        worldStart,
                        "\n" + GetSocketText(data.type, data.name, showLegacyOffset ? "SPS3" : "SPS"),
                        Color.gray,
                        true,
                        true
                    );
                }
            }

            if (showLegacyOffset) {
                var legacyOrigin = socketTransformWorld.TransformPoint(data.legacyOffsetLocal);
                DrawSocketGizmo(
                    legacyOrigin,
                    worldRotation,
                    data.legacyType,
                    Color.gray,
                    Color.gray,
                    new Color(1f, 0.5f, 0),
                    drawCross
                );
                if (owner.IsSelected()) {
                    VRCFuryGizmoUtils.DrawText(
                        legacyOrigin,
                        "\nLegacy " + GetSocketText(data.legacyType, "", "SPS1/SPS2").Replace("SPS Socket", "SPS Socket Override"),
                        Color.gray,
                        true,
                        true
                    );
                }
            }
        }

        internal static void DrawEditableLegacyOffset(VRCFuryHapticSocket socket) {
            if (socket == null) return;
            if (!socket.overrideLegacyOffset) return;
            if (Tools.hidden || EditorUtility.IsPersistent(socket)) return;
            if (Tools.current != Tool.Move && Tools.current != Tool.Transform) return;

            var owner = socket.owner();
            var socketTransformWorld = new TransformData(owner)
                                       * VRCFuryHapticSocketEditor.GetSocketTransformLocal(socket).Item2;

            EditorGUI.BeginChangeCheck();
            var offsetWorld = socketTransformWorld.TransformPoint(socket.legacyOffsetLocal);
            var handleRotation = Tools.pivotRotation == PivotRotation.Local
                ? socketTransformWorld.rotation
                : Quaternion.identity;
            var newOffsetWorld = Handles.PositionHandle(offsetWorld, handleRotation);
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
