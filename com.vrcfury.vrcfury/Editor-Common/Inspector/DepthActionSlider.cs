using System;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using VF.Component;
using VF.Builder.Haptics;
using VF.Exceptions;
using VF.Injector;
using VF.Utils;

namespace VF.Inspector {
    internal class DepthActionSlider : BaseField<Vector2> {
        private static readonly Color[] AnimationColors = {
            Color.red,
            new Color(1, 0.5f, 0),
            Color.yellow,
            Color.green,
            Color.cyan,
            Color.blue,
            new Color(0.7f, 0, 1)
        };
        private readonly MinMaxSlider slider;
        private readonly bool separateValues;
        private readonly string minimumPath;
        private readonly FloatField xField;
        private readonly FloatField yField;
        private readonly Label minMeters;
        private readonly Label maxMeters;
        private readonly UnityEngine.Component owner;
        private readonly bool radius;
        private readonly SerializedObject serialized;
        private readonly string rangePath;
        private readonly int actionIndex;
        private readonly VRCFuryHapticSocket.DepthActionUnits units;
        
        public DepthActionSlider(
            SerializedProperty prop,
            VRCFuryHapticSocket.DepthActionUnits units,
            bool radius = false,
            SerializedProperty minimum = null
        ) : base(null, null) {
            this.radius = radius;
            this.units = units;
            separateValues = prop.propertyType == SerializedPropertyType.Float;
            minimumPath = minimum?.propertyPath;
            if (separateValues && minimum == null) throw new ArgumentException("A float range requires a minimum property");
            serialized = prop.serializedObject;
            rangePath = prop.propertyPath;
            actionIndex = prop.GetArrayIndex() ?? 0;
            if (!separateValues) bindingPath = prop.propertyPath;
            Clear();
            style.flexDirection = FlexDirection.Column;

            var output = new VisualElement();
            output.Row();

            xField = new FloatField().FlexBasis(50);
            xField.isDelayed = true;
            xField.RegisterValueChangedCallback(e => {
                var v = e.newValue;
                Changed(new Vector2(
                    v,
                    Mathf.Max(v, value.y)
                ));
            });
            if (separateValues) xField.name = minimum.name;
            output.Add(xField);

            var c = new VisualElement();

            if (!radius) {
                var test = new Label(units == VRCFuryHapticSocket.DepthActionUnits.Plugs ? "Fully\n\u2193 Inserted" : units == VRCFuryHapticSocket.DepthActionUnits.Local ? "Tip inside\n\u2193 1 local-unit" : "Tip\n\u2193 inside 1m");
                test.style.position = Position.Absolute;
                test.style.bottom = 15;
                test.style.fontSize = 9;
                c.Add(test);

                var test2 = new Label("Tip at\n\u2193 Entrance");
                test2.style.position = Position.Absolute;
                test2.style.bottom = 15;
                test2.style.left = Length.Percent(25);
                test2.style.fontSize = 9;
                c.Add(test2);
        
                var test3 = new Label(units == VRCFuryHapticSocket.DepthActionUnits.Plugs ? "Tip 3 plug-lengths\naway \u2193" : units == VRCFuryHapticSocket.DepthActionUnits.Local ? "Tip 3 local-units\naway \u2193" : "Tip 3m\naway \u2193");
                test3.style.position = Position.Absolute;
                test3.style.bottom = 15;
                test3.style.right = 0;
                test3.style.fontSize = 9;
                test3.style.unityTextAlign = TextAnchor.UpperRight;
                c.Add(test3);
            }

            slider = new MinMaxSlider {
                highLimit = radius ? 0.5f : 3,
                lowLimit = radius ? 0 : -1
            };
            slider.RegisterValueChangedCallback(e => {
                // Keep more precision for the smaller radius ranges.
                Changed(new Vector2(
                    (float)Math.Round(e.newValue.x, radius ? 3 : 2),
                    (float)Math.Round(e.newValue.y, radius ? 3 : 2)
                ));
            });
            c.Add(slider);

            output.style.marginTop = radius ? 0 : 20;

            output.Add(c.FlexGrow(1).FlexBasis(0));

            yField = new FloatField().FlexBasis(50);
            if (separateValues) yField.name = prop.name;
            yField.isDelayed = true;
            yField.RegisterValueChangedCallback(e => {
                var v = e.newValue;
                Changed(new Vector2(
                    Mathf.Min(v, value.x),
                    v
                ));
            });
            output.Add(yField);

            output.FlexGrow(1);
            Add(output);
            if (units == VRCFuryHapticSocket.DepthActionUnits.Local
                && prop.serializedObject.targetObject is UnityEngine.Component component) {
                owner = component;
                var meters = new VisualElement().Row();
                meters.style.justifyContent = Justify.SpaceBetween;
                minMeters = VRCFuryEditorUtils.WrappedLabel("");
                minMeters.name = "rangeMetersMin";
                maxMeters = VRCFuryEditorUtils.WrappedLabel("");
                maxMeters.name = "rangeMetersMax";
                maxMeters.style.unityTextAlign = TextAnchor.UpperRight;
                meters.Add(minMeters);
                meters.Add(maxMeters);
                Add(meters);
                meters.ScheduleSafe(UpdateMeters).Every(100);
                UpdateMeters();
            }
            if (separateValues) {
                SetValueWithoutNotify(ReadRange(prop));
                this.TrackProperty(prop, p => SetValueWithoutNotify(ReadRange(p)));
                this.TrackProperty(minimum, _ => {
                    var current = serialized.FindProperty(rangePath);
                    if (current != null) SetValueWithoutNotify(ReadRange(current));
                });
                this.RegisterValueChangedCallback(evt => {
                    if (evt.target != this) return;
                    var current = serialized.FindProperty(rangePath);
                    if (current == null) return;
                    WriteRange(current, evt.newValue);
                    serialized.ApplyModifiedProperties();
                });
            }
            if ((serialized.targetObject is VRCFuryHapticSocket && units != VRCFuryHapticSocket.DepthActionUnits.Plugs)
                || (!radius && serialized.targetObject is VRCFuryHapticPlug)) {
                void DrawScene(SceneView _) {
                    if (serialized.targetObject is UnityEngine.Component target && target.owner().IsSelected() && !Tools.hidden) {
                        try {
                            float? offDepth = null;
                            EditRangeVisually((center, rotation, value, minimum) => {
                                if (radius) return DrawRadiusHandle(center, rotation, value, minimum);
                                if (!minimum) offDepth = value;
                                return DrawDepthHandle(center, rotation, value, minimum, offDepth);
                            });
                        } catch (VRCFBuilderException e) {
                            Handles.Label(((UnityEngine.Component)serialized.targetObject).transform.position, e.Message);
                        }
                    }
                }
                RegisterCallback<AttachToPanelEvent>(_ => SceneView.duringSceneGui += DrawScene);
                RegisterCallback<DetachFromPanelEvent>(_ => {
                    SceneView.duringSceneGui -= DrawScene;
                    SceneView.RepaintAll();
                });
            }
        }

        internal void EditRangeVisually(Func<Vector3, Quaternion, float, bool, float> draw) {
            if (!(serialized.targetObject is UnityEngine.Component component)) return;
            var scale = units == VRCFuryHapticSocket.DepthActionUnits.Local
                ? Mathf.Abs(component.transform.lossyScale.x) : 1;
            TransformData frame;
            if (component is VRCFuryHapticSocket socket) {
                if (units == VRCFuryHapticSocket.DepthActionUnits.Plugs) return;
                frame = new TransformData(socket.owner()) * VRCFuryHapticSocketEditor.GetSocketTransformLocal(socket).Item2;
            } else if (component is VRCFuryHapticPlug plug && !radius) {
                frame = new TransformData(plug.owner()) * new TransformData(plug.position, Quaternion.Euler(plug.rotation));
                var length = plug.autoLength
                    ? VRCFuryPerFrameInjector.GetPerFrameInjector(plug.owner()).GetService<PlugSizeService>().GetWorldSize(plug).worldLength
                    : plug.length * (plug.unitsInMeters ? 1 : Mathf.Abs(plug.transform.lossyScale.x));
                frame = frame.WithPosition(frame.position + frame.TransformDirection(Vector3.forward) * length);
                if (units == VRCFuryHapticSocket.DepthActionUnits.Plugs) scale = length;
            } else return;
            if (!(scale > 0)) return;
            serialized.UpdateIfRequiredOrScript();
            var range = serialized.FindProperty(rangePath);
            if (range == null) return;
            // Radius editors stay on the actual socket/path; depth editors sit beside the main line.
            var offset = radius ? Vector3.zero
                : Quaternion.Euler(0, 0, 45 + (actionIndex % 4) * 90) * Vector3.right * 0.02f;
            var center = frame.position + frame.rotation * offset;
            var handleRotation = frame.rotation;
            var current = ReadRange(range);
            if (separateValues && current.y <= 0) return;
            if (separateValues && component is VRCFuryHapticSocket depthSocket) {
                var action = serialized.FindProperty(rangePath.Substring(0, rangePath.LastIndexOf('.')));
                var depthUnits = (VRCFuryHapticSocket.DepthActionUnits)action.FindPropertyRelative("units").enumValueIndex;
                if (depthUnits == VRCFuryHapticSocket.DepthActionUnits.Plugs) return;
                var depth = action.FindPropertyRelative("range").vector2Value.x;
                if (depthUnits == VRCFuryHapticSocket.DepthActionUnits.Local) depth *= Mathf.Abs(component.transform.lossyScale.x);
                if (float.IsInfinity(depth) || float.IsNaN(depth)) return;
                var sample = new SocketDepthPath(depthSocket, center, handleRotation).Sample(depth);
                center = sample.position;
                handleRotation = Quaternion.FromToRotation(frame.TransformDirection(Vector3.forward), sample.outward) * frame.rotation;
            }
            float Draw(float input, bool minimum) {
                if (float.IsNaN(input) || float.IsInfinity(input)) return input;
                var worldValue = input * scale;
                var edited = draw(center, handleRotation, worldValue, minimum);
                return edited == worldValue ? input : edited / scale;
            }
            // Draw the maximum first so it stays draggable when both handles are at zero.
            var maximum = Draw(current.y, false);
            var minimum = radius && current.x == 0 ? 0 : Draw(current.x, true);
            var changed = new Vector2(
                radius ? Mathf.Clamp(minimum, 0, current.y) : Mathf.Min(minimum, current.y),
                radius ? Mathf.Max(0, maximum) : maximum
            );
            changed.y = Mathf.Max(changed.x, changed.y);
            if (changed == current) return;
            Undo.SetCurrentGroupName(radius ? "Edit SPS Plug Entry Radius Range" : "Edit SPS Depth Range");
            WriteRange(range, changed);
            serialized.ApplyModifiedProperties();
            SetValueWithoutNotify(changed);
        }

        private float DrawRadiusHandle(Vector3 center, Quaternion rotation, float radius, bool minimum) {
            if (minimum && radius == 0) return radius;
            using (new Handles.DrawingScope(AnimationColors[actionIndex % AnimationColors.Length], Matrix4x4.identity)) {
                Handles.DrawWireDisc(center, rotation * Vector3.forward, radius);
                foreach (var axis in new[] { Vector3.right, Vector3.up, Vector3.left, Vector3.down }) {
                    var direction = rotation * (minimum ? Quaternion.Euler(0, 0, 45) * axis : axis);
                    var point = center + direction * radius;
                    EditorGUI.BeginChangeCheck();
                    var moved = Handles.Slider(
                        point,
                        direction,
                        HandleUtility.GetHandleSize(point) * 0.04f,
                        Handles.DotHandleCap,
                        0
                    );
                    if (EditorGUI.EndChangeCheck()) radius = Mathf.Max(0, Vector3.Dot(moved - center, direction));
                }
                VRCFuryGizmoUtils.DrawText(
                    center + rotation * (Vector3.right * radius),
                    $"{(separateValues ? "D" : "R")}{actionIndex + 1} {(minimum ? "Off" : "Fully On")}",
                    Handles.color,
                    left: true
                );
            }
            return radius;
        }

        private float DrawDepthHandle(Vector3 center, Quaternion rotation, float depth, bool minimum, float? offDepth) {
            using (new Handles.DrawingScope(AnimationColors[actionIndex % AnimationColors.Length], Matrix4x4.identity)) {
                var socket = serialized.targetObject as VRCFuryHapticSocket;
                var path = socket != null && socket.guidedPathStops.Any(s => s?.transform != null)
                    ? new SocketDepthPath(socket, center, rotation) : null;
                var axis = rotation * Vector3.forward;
                var position = center + axis * depth;
                if (path != null) (position, axis) = path.Sample(depth);
                var size = HandleUtility.GetHandleSize(position) * 0.1f;
                Handles.DrawWireDisc(position, axis, size);
                if (minimum && offDepth.HasValue) {
                    using (new Handles.DrawingScope(AnimationColors[actionIndex % AnimationColors.Length])) {
                        if (path == null) Handles.DrawLine(position, center + axis * offDepth.Value);
                        else Handles.DrawAAPolyLine(path.RangePoints(depth, offDepth.Value));
                    }
                }
                EditorGUI.BeginChangeCheck();
                var moved = path != null ? Handles.FreeMoveHandle(
                    position,
                    size * 0.4f,
                    Vector3.zero,
                    Handles.DotHandleCap
                ) : Handles.Slider(
                    position,
                    axis,
                    size,
                    Handles.ArrowHandleCap,
                    0
                );
                VRCFuryGizmoUtils.DrawText(
                    position,
                    $"D{actionIndex + 1} {(minimum ? "Fully On" : "Off")}",
                    Handles.color,
                    left: true
                );
                Vector3 ProjectToGui(Vector3 point) {
                    var gui = HandleUtility.WorldToGUIPoint(point);
                    var camera = Camera.current;
                    return new Vector3(gui.x, gui.y, camera.orthographic ? 1 : camera.WorldToViewportPoint(point).z);
                }
                return EditorGUI.EndChangeCheck()
                    ? path != null ? path.Project(Event.current.mousePosition, ProjectToGui) : Vector3.Dot(moved - center, axis)
                    : depth;
            }
        }

        /**
         * This is an exitor approximation of the socket guided path, which takes a bunch of samples
         * and can then find the nearest point on the curve to the user's mouse cursor.
         * I would highly recommend treating this as a black box and not looking in here for your own sanity.
         */
        internal sealed class SocketDepthPath {
            private readonly List<Vector3> points = new();
            private readonly List<float> distances = new();
            private readonly Vector3 entranceForward;
            private readonly Vector3 terminalForward;

            internal SocketDepthPath(VRCFuryHapticSocket socket, Vector3 center, Quaternion rotation) {
                var frame = new TransformData(socket.owner()) * VRCFuryHapticSocketEditor.GetSocketTransformLocal(socket).Item2;
                var offset = center - frame.position;
                var previous = new TransformData(center, rotation);
                entranceForward = rotation * Vector3.forward;
                points.Add(center);
                distances.Add(0);
                foreach (var stop in socket.guidedPathStops.Take(SpsTags.MaxUserPathStops)) {
                    if (stop?.transform == null) continue;
                    var next = new TransformData(stop.transform).WithPosition(stop.transform.position + offset);
                    var samples = Handles.MakeBezierPoints(
                        previous.position,
                        next.position,
                        VRCFuryHapticSocketGizmo.GetDefaultTangentOut(previous, next),
                        VRCFuryHapticSocketGizmo.GetDefaultTangentIn(previous, next),
                        33
                    );
                    foreach (var point in samples.Skip(1)) {
                        var length = Vector3.Distance(points[points.Count - 1], point);
                        if (length <= 0.000001f) continue;
                        distances.Add(distances[distances.Count - 1] + length);
                        points.Add(point);
                    }
                    previous = next;
                }
                terminalForward = previous.TransformDirection(Vector3.forward);
            }

            internal (Vector3 position, Vector3 outward) Sample(float depth) {
                if (depth >= 0) return (points[0] + entranceForward * depth, entranceForward);
                var distance = -depth;
                for (var i = 1; i < points.Count; i++) {
                    if (distance > distances[i]) continue;
                    var t = (distance - distances[i - 1]) / (distances[i] - distances[i - 1]);
                    return (Vector3.Lerp(points[i - 1], points[i], t), (points[i - 1] - points[i]).normalized);
                }
                var last = points.Count - 1;
                return (points[last] - terminalForward * (distance - distances[last]), terminalForward);
            }

            internal Vector3[] RangePoints(float minimum, float maximum) {
                var output = new List<Vector3> { Sample(maximum).position };
                for (var i = 0; i < points.Count; i++) {
                    if (-distances[i] > minimum && -distances[i] < maximum) output.Add(points[i]);
                }
                output.Add(Sample(minimum).position);
                return output.ToArray();
            }

            // The projection's Z is perspective depth, or 1 for an orthographic view.
            internal float Project(Vector2 mouse, Func<Vector3, Vector3> project) {
                var depth = 0f;
                var best = float.PositiveInfinity;
                void Consider(Vector3 start, Vector3 end, float startDepth, float endDepth, bool ray = false) {
                    var a = project(start);
                    var b = project(end);
                    if (a.z <= 0) return;
                    if (b.z <= 0) {
                        // Keep the projected segment in front of the camera, including extension rays.
                        var visible = a.z / (a.z - b.z) * 0.5f;
                        end = Vector3.Lerp(start, end, visible);
                        endDepth = Mathf.Lerp(startDepth, endDepth, visible);
                        b = project(end);
                    }
                    var segment = (Vector2)(b - a);
                    var t = segment.sqrMagnitude > 0
                        ? Vector2.Dot(mouse - (Vector2)a, segment) / segment.sqrMagnitude : 0;
                    t = ray ? Mathf.Max(0, t) : Mathf.Clamp01(t);
                    // Screen-space interpolation is nonlinear along a perspective-projected segment.
                    var denominator = (1 - t) * b.z + t * a.z;
                    if (denominator <= 0) return;
                    t = t * a.z / denominator;
                    var point = Vector3.LerpUnclamped(start, end, t);
                    var error = (mouse - (Vector2)project(point)).sqrMagnitude;
                    if (error >= best) return;
                    best = error;
                    depth = Mathf.LerpUnclamped(startDepth, endDepth, t);
                }
                Consider(points[0], points[0] + entranceForward, 0, 1, ray: true);
                for (var i = 1; i < points.Count; i++) {
                    Consider(points[i - 1], points[i], -distances[i - 1], -distances[i]);
                }
                var last = points.Count - 1;
                Consider(points[last], points[last] - terminalForward, -distances[last], -distances[last] - 1, ray: true);
                return depth;
            }
        }

        private void UpdateMeters() {
            if (minMeters == null || owner == null) return;
            if (separateValues && serialized.FindProperty(rangePath) == null) {
                maxMeters.text = "";
                return;
            }
            var scale = owner.transform.lossyScale.x;
            minMeters.text = $"{value.x * scale:G4} m";
            maxMeters.text = $"{value.y * scale:G4} m";
        }

        private void Changed(Vector2 newValue) {
            newValue.x = Mathf.Min(newValue.x, newValue.y);
            newValue.y = Mathf.Max(newValue.x, newValue.y);
            if (radius) {
                newValue.x = Mathf.Max(0, newValue.x);
                newValue.y = Mathf.Max(0, newValue.y);
            }
            value = newValue;
            // This usually happens automatically, but might not if the user sets the float
            // to something invalid
            SetValueWithoutNotify(value);
        }

        public override void SetValueWithoutNotify(Vector2 newValue) {
            base.SetValueWithoutNotify(newValue);
            if (!radius) slider.lowLimit = Mathf.Min(slider.lowLimit, newValue.x);
            slider.highLimit = Mathf.Max(slider.highLimit, newValue.y);
            slider.SetValueWithoutNotify(newValue);
            xField.SetValueWithoutNotify(newValue.x);
            yField.SetValueWithoutNotify(newValue.y);
            UpdateMeters();
        }

        private Vector2 ReadRange(SerializedProperty range) {
            return separateValues
                ? new Vector2(serialized.FindProperty(minimumPath)?.floatValue ?? 0, range.floatValue)
                : range.vector2Value;
        }

        private void WriteRange(SerializedProperty range, Vector2 value) {
            if (separateValues) {
                var minimum = serialized.FindProperty(minimumPath);
                if (minimum == null) return;
                minimum.floatValue = value.x;
                range.floatValue = value.y;
            } else {
                range.vector2Value = value;
            }
        }
    }
}
