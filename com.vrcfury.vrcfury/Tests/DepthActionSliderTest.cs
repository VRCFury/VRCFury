namespace VF.Tests {
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;
    using NUnit.Framework;
    using UnityEditor;
    using UnityEditor.UIElements;
    using UnityEngine;
    using UnityEngine.UIElements;
    using VF.Inspector;
    using VF.Component;
    using Object = UnityEngine.Object;

    internal class DepthActionSliderTest : InspectorTestBase {
        [Test]
        public void AnimationMeterLabelsRefreshFromEditorChangesInsteadOfPolling() {
            var panelOwner = ScriptableObject.CreateInstance<Fields>();
            var parent = new GameObject("Scale parent");
            try {
                var obj = new GameObject("Socket");
                obj.transform.SetParent(parent.transform, false);
                var socket = obj.AddComponent<VRCFuryHapticSocket>();
                socket.maxRadiusOffset = .1f;
                using (var serialized = new SerializedObject(socket))
                using (var panel = new Panel(panelOwner)) {
                    var control = new DepthActionSlider(
                        serialized.FindProperty("maxRadiusOffset"),
                        VRCFuryHapticSocket.DepthActionUnits.Local,
                        radius: true
                    );
                    panel.root.Add(control);
                    panel.root.Bind(serialized);
                    panel.Pump();
                    var meters = control.Q<Label>("rangeMetersMax");
                    Assert.That(meters.text, Is.EqualTo("0.1 m"));
                    parent.transform.localScale = Vector3.one * 2;
                    for (var i = 0; i < 20; i++) panel.Pump();
                    Assert.That(meters.text, Is.EqualTo("0.1 m"), "There must be no periodic refresh without a notification.");
                    VF.Utils.EditorChanges.Changed();
                    panel.Pump();
                    Assert.That(meters.text, Is.EqualTo("0.2 m"));
                    control.RemoveFromHierarchy();
                    parent.transform.localScale = Vector3.one * 3;
                    VF.Utils.EditorChanges.Changed();
                    panel.Pump();
                    Assert.That(meters.text, Is.EqualTo("0.2 m"));
                    panel.root.Add(control);
                    panel.Pump();
                    Assert.That(meters.text, Is.EqualTo("0.3 m"));
                }
            } finally {
                Object.DestroyImmediate(parent);
                Object.DestroyImmediate(panelOwner);
            }
        }

        [TestCase(false)]
        [TestCase(true)]
        public void TypedAnimationRangesAreUnboundedExceptForRadiusMinimum(bool radius) {
            var panelOwner = ScriptableObject.CreateInstance<Fields>();
            var obj = new GameObject("Range owner");
            try {
                var socket = obj.AddComponent<VRCFuryHapticSocket>();
                socket.depthActions2.Add(new VRCFuryHapticSocket.DepthActionNew());
                using (var serialized = new SerializedObject(socket))
                using (var panel = new Panel(panelOwner)) {
                    var range = serialized.FindProperty("depthActions2").GetArrayElementAtIndex(0).FindPropertyRelative("range");
                    var control = new DepthActionSlider(range, VRCFuryHapticSocket.DepthActionUnits.Local, radius);
                    panel.root.Add(control);
                    panel.root.Bind(serialized);
                    panel.Pump();
                    var fields = control.Query<FloatField>().ToList();
                    fields[0].value = -10;
                    fields[1].value = 25;
                    panel.Pump();
                    Assert.That(control.value, Is.EqualTo(new Vector2(radius ? 0 : -10, 25)));
                    Assert.That(range.vector2Value, Is.EqualTo(control.value));
                    var slider = control.Q<MinMaxSlider>();
                    Assert.That(slider.value, Is.EqualTo(control.value));
                    Assert.That(slider.lowLimit, Is.EqualTo(radius ? 0 : -10));
                    Assert.That(slider.highLimit, Is.EqualTo(25));
                    Assert.That(control.Q<Label>("rangeMetersMin").text, Is.EqualTo(radius ? "0 m" : "-10 m"));
                    Assert.That(control.Q<Label>("rangeMetersMax").text, Is.EqualTo("25 m"));
                    Assert.That(control.Q<Label>("rangeMetersMax").resolvedStyle.unityTextAlign, Is.EqualTo(TextAnchor.UpperRight));
                }
            } finally {
                Object.DestroyImmediate(obj);
                Object.DestroyImmediate(panelOwner);
            }
        }

        [TestCase("socket", 0.5f)]
        [TestCase("socket", 2f)]
        [TestCase("plug", 0.5f)]
        [TestCase("plug", 2f)]
        [TestCase("radius", 0.5f)]
        [TestCase("radius", 2f)]
        public void AnimationUnitChangesPreservePhysicalRange(string kind, float scale) {
            var panelOwner = ScriptableObject.CreateInstance<Fields>();
            var parent = new GameObject("Scaled parent");
            var obj = new GameObject("Animation owner");
            obj.transform.SetParent(parent.transform, false);
            parent.transform.localScale = Vector3.one * 3;
            obj.transform.localScale = Vector3.one * scale;
            try {
                UnityEngine.Component component;
                var original = kind == "radius" ? new Vector2(0.02f, 0.1f) : new Vector2(-0.25f, 0.1f);
                string list;
                if (kind == "plug") {
                    var plug = obj.AddComponent<VRCFuryHapticPlug>();
                    plug.depthActions2.Add(new VRCFuryHapticSocket.DepthActionNew { range = original });
                    component = plug;
                    list = "depthActions2";
                } else {
                    var socket = obj.AddComponent<VRCFuryHapticSocket>();
                    if (kind == "radius") socket.plugEntryRadiusActions.Add(new VRCFuryHapticSocket.PlugEntryRadiusAction { range = original });
                    else socket.depthActions2.Add(new VRCFuryHapticSocket.DepthActionNew { range = original });
                    component = socket;
                    list = kind == "radius" ? "plugEntryRadiusActions" : "depthActions2";
                }
                using (var serialized = new SerializedObject(component))
                using (var panel = new Panel(panelOwner)) {
                    var action = serialized.FindProperty(list).GetArrayElementAtIndex(0);
                    var drawer = kind == "radius"
                        ? new VRCFuryHapticSocketEditor.PlugEntryRadiusActionDrawer()
                        : new VRCFuryHapticSocketEditor.DepthActionDrawer();
                    var editor = drawer.CreatePropertyGUI(action);
                    panel.root.Add(editor);
                    panel.root.Bind(serialized);
                    panel.Pump();
                    var units = editor.Query<PopupField<string>>().ToList().Single(field => field.choices.Contains("Meters"));
                    units.value = "Meters";
                    panel.Pump();
                    Assert.That(action.FindPropertyRelative("range").vector2Value, Is.EqualTo(original * (scale * 3)));
                    parent.transform.localScale = Vector3.one * 6;
                    units.value = "Local";
                    panel.Pump();
                    Assert.That(action.FindPropertyRelative("range").vector2Value, Is.EqualTo(original / 2),
                        "Meters to Local must use the current scale, including ancestors");
                    if (kind != "radius") {
                        units.value = "Plugs";
                        panel.Pump();
                        Assert.That(action.FindPropertyRelative("range").vector2Value, Is.EqualTo(original / 2));
                    }
                }
            } finally {
                Object.DestroyImmediate(parent);
                Object.DestroyImmediate(panelOwner);
            }
        }

        [Test]
        public void RadiusMultiplierVisualEditorEditsRangeAtDeepestPoint(
            [Values(0.5f, 2f)] float scale,
            [Values] bool meters,
            [Values] bool curved,
            [Values(0f, 0.025f)] float zeroRadius
        ) {
            var obj = new GameObject("Radius multiplier socket");
            try {
                obj.transform.localScale = Vector3.one * scale;
                var socket = obj.AddComponent<VRCFuryHapticSocket>();
                socket.depthActions2.Add(new VRCFuryHapticSocket.DepthActionNew {
                    plugRadiusForFullWeight = 0.1f,
                    plugRadiusForZeroWeight = zeroRadius,
                    range = new Vector2(-0.15f, 0),
                    units = meters ? VRCFuryHapticSocket.DepthActionUnits.Meters : VRCFuryHapticSocket.DepthActionUnits.Local
                });
                if (curved) {
                    var stop = new GameObject("Stop").transform;
                    stop.SetParent(obj.transform, false);
                    stop.localPosition = new Vector3(0.2f, 0, -0.3f);
                    stop.localRotation = Quaternion.Euler(0, -45, 0);
                    socket.guidedPathStops.Add(new VRCFuryHapticSocket.GuidedPathStop { transform = stop });
                }
                var expected = new DepthActionSlider.SocketDepthPath(socket, obj.transform.position, obj.transform.rotation)
                    .Sample(-0.15f * (meters ? 1 : scale));
                using (var serialized = new SerializedObject(socket)) {
                    var radius = serialized.FindProperty("depthActions2").GetArrayElementAtIndex(0).FindPropertyRelative("plugRadiusForFullWeight");
                    var minimumRadius = serialized.FindProperty("depthActions2").GetArrayElementAtIndex(0).FindPropertyRelative("plugRadiusForZeroWeight");
                    var control = new DepthActionSlider(
                        radius,
                        VRCFuryHapticSocket.DepthActionUnits.Local,
                        radius: true,
                        minimum: minimumRadius
                    );
                    var calls = 0;
                    Undo.IncrementCurrentGroup();
                    control.EditRangeVisually((center, rotation, value, minimum) => {
                        Assert.That(Vector3.Distance(center, expected.position), Is.LessThan(0.00001f));
                        Assert.That(Vector3.Distance(rotation * Vector3.forward, expected.outward), Is.LessThan(0.00001f));
                        Assert.That(value, Is.EqualTo((minimum ? zeroRadius : 0.1f) * scale).Within(0.00001f));
                        calls++;
                        return minimum ? 0.01f : 0.2f;
                    });
                    Assert.That(calls, Is.EqualTo(zeroRadius > 0 ? 2 : 1));
                    Assert.That(radius.floatValue, Is.EqualTo(0.2f / scale).Within(0.00001f));
                    Assert.That(minimumRadius.floatValue, Is.EqualTo(zeroRadius > 0 ? 0.01f / scale : 0).Within(0.00001f));
                    Undo.PerformUndo();
                    serialized.Update();
                    Assert.That(radius.floatValue, Is.EqualTo(0.1f));
                    Assert.That(minimumRadius.floatValue, Is.EqualTo(zeroRadius));
                    radius.floatValue = 0;
                    serialized.ApplyModifiedProperties();
                    control.EditRangeVisually((center, rotation, value, minimum) => {
                        Assert.Fail("A disabled multiplier must not draw a handle.");
                        return value;
                    });
                }
            } finally {
                Object.DestroyImmediate(obj);
            }
        }

        [Test]
        public void MouseProjectionHandlesExtensionTowardTheCamera() {
            var obj = new GameObject("Socket near camera");
            try {
                var socket = obj.AddComponent<VRCFuryHapticSocket>();
                var path = new DepthActionSlider.SocketDepthPath(socket, Vector3.zero, Quaternion.identity);
                Vector3 Screen(Vector3 point) {
                    var w = 0.1f - point.z;
                    return new Vector3((point.x + 0.03f) / w, point.y / w, w);
                }
                var mouse = (Vector2)Screen(new Vector3(0, 0, 0.05f));
                // Off-curve mouse movement must still pick the closest visible point in screen space.
                mouse.y += 10;
                Assert.That(path.Project(mouse, Screen), Is.EqualTo(0.05f).Within(0.00001f));
                Assert.That(path.Project(Screen(new Vector3(0, 0, -2)), Screen), Is.EqualTo(-2).Within(0.00001f));
            } finally {
                Object.DestroyImmediate(obj);
            }
        }

        [Test]
        public void OffsetDepthEndpointsStayInTheRadiusRingPlane() {
            var obj = new GameObject("Curved socket");
            try {
                var socket = obj.AddComponent<VRCFuryHapticSocket>();
                var stop = new GameObject("Stop").transform;
                stop.SetParent(obj.transform, false);
                stop.localPosition = new Vector3(0.2f, 0, -0.3f);
                stop.localRotation = Quaternion.Euler(0, -45, 0);
                socket.guidedPathStops.Add(new VRCFuryHapticSocket.GuidedPathStop { transform = stop });
                var path = new DepthActionSlider.SocketDepthPath(socket, Vector3.zero, Quaternion.identity);
                for (var index = 0; index < 4; index++) {
                    var offset = Quaternion.Euler(0, 0, 45 + index * 90) * Vector3.right * 0.02f;
                    var shifted = new DepthActionSlider.SocketDepthPath(socket, Vector3.zero, Quaternion.identity, offset);
                    foreach (var depth in new[] { 0.1f, -0.05f, -0.15f, -0.3f, -0.6f }) {
                        var ring = path.Sample(depth);
                        var endpoint = shifted.Sample(depth);
                        var delta = endpoint.position - ring.position;
                        Assert.That(Vector3.Dot(delta, ring.outward), Is.Zero.Within(0.000001f));
                        Assert.That(delta.magnitude, Is.EqualTo(0.02f).Within(0.000001f));
                        Assert.That(Vector3.Distance(endpoint.outward, ring.outward), Is.LessThan(0.000001f));
                        Assert.That(Vector3.Distance(shifted.RangePoints(depth - 0.01f, depth)[0], endpoint.position), Is.LessThan(0.000001f));
                    }
                }
            } finally {
                Object.DestroyImmediate(obj);
            }
        }

        [Test]
        public void GuidedDepthHandlesFollowCurveAndProjectBackToDepth(
            [Values] bool curved,
            [Values(0.5f, 2f)] float scale,
            [Values] bool perspective
        ) {
            var obj = new GameObject("Guided socket");
            try {
                obj.transform.SetPositionAndRotation(new Vector3(1, 2, 3), Quaternion.Euler(20, 30, 40));
                obj.transform.localScale = Vector3.one * scale;
                var socket = obj.AddComponent<VRCFuryHapticSocket>();
                var stop = new GameObject("Stop").transform;
                stop.SetParent(obj.transform, false);
                stop.localPosition = new Vector3(curved ? 0.2f : 0, 0, -0.3f);
                stop.localRotation = Quaternion.Euler(0, curved ? -45 : 0, 0);
                socket.guidedPathStops.Add(new VRCFuryHapticSocket.GuidedPathStop());
                socket.guidedPathStops.Add(new VRCFuryHapticSocket.GuidedPathStop { transform = stop });
                var offset = obj.transform.right * (0.05f * scale);
                var center = obj.transform.position + offset;
                var path = new DepthActionSlider.SocketDepthPath(socket, center, obj.transform.rotation);
                Vector3 Screen(Vector3 point) {
                    var local = obj.transform.InverseTransformPoint(point);
                    var w = perspective ? 2 + local.z * 0.5f : 1;
                    return new Vector3(local.x / w, local.z / w, w);
                }
                var distance = Vector3.Distance(obj.transform.position, stop.position);
                var midpoint = Handles.MakeBezierPoints(
                    center,
                    stop.position + offset,
                    center - obj.transform.forward * distance * 0.5f,
                    stop.position + offset + stop.forward * distance * 0.5f,
                    33
                )[16];
                var points = path.RangePoints(-100, 0);
                Assert.That(Vector3.Distance(points[16], midpoint), Is.LessThan(0.00001f));
                Assert.That(Vector3.Distance(path.Sample(0.1f * scale).position, center + obj.transform.forward * (0.1f * scale)), Is.LessThan(0.00001f));
                var arcLength = 0f;
                for (var i = 1; i < points.Length - 1; i++) {
                    arcLength += Vector3.Distance(points[i - 1], points[i]);
                }
                Assert.That(Vector3.Distance(path.Sample(-arcLength).position, stop.position + offset), Is.LessThan(0.00001f));
                var extended = path.Sample(-arcLength - 0.2f * scale);
                Assert.That(Vector3.Distance(extended.position, stop.position + offset - stop.forward * (0.2f * scale)), Is.LessThan(0.00001f));
                foreach (var range in new[] {
                    new Vector2(0.1f, 0.2f),
                    new Vector2(-0.2f, -0.1f),
                    new Vector2(-0.15f, 0.1f),
                    new Vector2(-2, -1)
                }) {
                    var line = path.RangePoints(range.x * scale, range.y * scale);
                    Assert.That(line[0], Is.EqualTo(path.Sample(range.y * scale).position));
                    Assert.That(line[line.Length - 1], Is.EqualTo(path.Sample(range.x * scale).position));
                    var lineLength = 0f;
                    for (var i = 1; i < line.Length; i++) lineLength += Vector3.Distance(line[i - 1], line[i]);
                    Assert.That(lineLength, Is.EqualTo((range.y - range.x) * scale).Within(0.00001f));
                }
                foreach (var depth in new[] { 0.2f, 0, -0.05f, -0.15f, -0.3f, -0.6f }) {
                    var sample = path.Sample(depth * scale);
                    Assert.That(path.Project(Screen(sample.position), Screen), Is.EqualTo(depth * scale).Within(0.00001f));
                    Assert.That(sample.outward.magnitude, Is.EqualTo(1).Within(0.00001f));
                }
                if (curved) {
                    Assert.That(Vector3.Distance(path.Sample(-0.15f * scale).position, center - obj.transform.forward * (0.15f * scale)), Is.GreaterThan(0.005f * scale));
                }
                var secondStop = new GameObject("Second stop").transform;
                secondStop.SetParent(obj.transform, false);
                secondStop.localPosition = new Vector3(curved ? 0.5f : 0, 0, -0.6f);
                secondStop.localRotation = stop.localRotation;
                socket.guidedPathStops.Add(new VRCFuryHapticSocket.GuidedPathStop { transform = secondStop });
                var chain = new DepthActionSlider.SocketDepthPath(socket, center, obj.transform.rotation);
                var chainPoints = chain.RangePoints(-100, 0);
                var chainLength = 0f;
                for (var i = 1; i < chainPoints.Length - 1; i++) {
                    chainLength += Vector3.Distance(chainPoints[i - 1], chainPoints[i]);
                    Assert.That(chain.Project(Screen(chainPoints[i]), Screen), Is.EqualTo(-chainLength).Within(0.00001f));
                }
                Assert.That(Vector3.Distance(chain.Sample(-chainLength).position, secondStop.position + offset), Is.LessThan(0.00001f));
                socket.guidedPathStops.Clear();
                stop.position = obj.transform.position;
                socket.guidedPathStops.Add(new VRCFuryHapticSocket.GuidedPathStop { transform = stop });
                var coincident = new DepthActionSlider.SocketDepthPath(socket, center, obj.transform.rotation);
                Assert.That(coincident.Project(Screen(coincident.Sample(-0.1f).position), Screen), Is.EqualTo(-0.1f).Within(0.00001f));
            } finally {
                Object.DestroyImmediate(obj);
            }
        }

        [Test]
        public void RadiusRangesStayAtEntranceAndDepthRangesLeaveMainLineClear(
            [Values] bool radius,
            [Values(0.5f, 2f)] float scale
        ) {
            var obj = new GameObject("Socket with multiple ranges");
            try {
                obj.transform.SetPositionAndRotation(new Vector3(1, 2, 3), Quaternion.Euler(20, 30, 40));
                obj.transform.localScale = Vector3.one * scale;
                var socket = obj.AddComponent<VRCFuryHapticSocket>();
                socket.position = new Vector3(0.1f, 0.2f, 0.3f);
                socket.rotation = new Vector3(10, 20, 30);
                for (var i = 0; i < 8; i++) {
                    if (radius) socket.plugEntryRadiusActions.Add(new VRCFuryHapticSocket.PlugEntryRadiusAction());
                    else socket.depthActions2.Add(new VRCFuryHapticSocket.DepthActionNew());
                }
                using (var serialized = new SerializedObject(socket)) {
                    var actions = serialized.FindProperty(radius ? "plugEntryRadiusActions" : "depthActions2");
                    var rotation = obj.transform.rotation * Quaternion.Euler(socket.rotation);
                    var origin = obj.transform.TransformPoint(socket.position);
                    var directions = new[] {
                        new Vector3(1, 1, 0).normalized,
                        new Vector3(-1, 1, 0).normalized,
                        new Vector3(-1, -1, 0).normalized,
                        new Vector3(1, -1, 0).normalized
                    };
                    for (var i = 0; i < 8; i++) {
                        var range = actions.GetArrayElementAtIndex(i).FindPropertyRelative("range");
                        var original = range.vector2Value;
                        var control = new DepthActionSlider(range, VRCFuryHapticSocket.DepthActionUnits.Local, radius);
                        var calls = 0;
                        control.EditRangeVisually((center, actualRotation, value, minimum) => {
                            var expected = origin + rotation * directions[i % 4] * (radius ? 0 : 0.02f);
                            Assert.That(Vector3.Distance(center, expected), Is.LessThan(0.00001f));
                            Assert.That(Quaternion.Angle(actualRotation, rotation), Is.LessThan(0.01f));
                            calls++;
                            return value;
                        });
                        Assert.That(calls, Is.EqualTo(radius ? 1 : 2));
                        Assert.That(range.vector2Value, Is.EqualTo(original));
                    }
                    Assert.That(socket.position, Is.EqualTo(new Vector3(0.1f, 0.2f, 0.3f)));
                    Assert.That(socket.rotation, Is.EqualTo(new Vector3(10, 20, 30)));
                }
            } finally {
                Object.DestroyImmediate(obj);
            }
        }

        [TestCase(false, 0.5f)]
        [TestCase(false, 2f)]
        [TestCase(true, 0.5f)]
        [TestCase(true, 2f)]
        public void VisualRadiusEditingUsesSocketFrameAndUnitsAndSupportsUndo(bool meters, float scale) {
            var panelOwner = ScriptableObject.CreateInstance<Fields>();
            var parent = new GameObject("Radius parent");
            try {
                parent.transform.SetPositionAndRotation(new Vector3(1, 2, 3), Quaternion.Euler(20, 30, 40));
                parent.transform.localScale = Vector3.one * scale;
                var obj = new GameObject("Socket");
                obj.transform.SetParent(parent.transform, false);
                var socket = obj.AddComponent<VRCFuryHapticSocket>();
                socket.position = new Vector3(0.1f, 0.2f, 0.3f);
                socket.rotation = new Vector3(10, 20, 30);
                socket.plugEntryRadiusActions.Add(new VRCFuryHapticSocket.PlugEntryRadiusAction {
                    range = new Vector2(0.01f, 0.1f),
                    units = meters ? VRCFuryHapticSocket.DepthActionUnits.Meters : VRCFuryHapticSocket.DepthActionUnits.Local
                });
                using (var serialized = new SerializedObject(socket))
                using (var panel = new Panel(panelOwner)) {
                    var range = serialized.FindProperty("plugEntryRadiusActions").GetArrayElementAtIndex(0).FindPropertyRelative("range");
                    var units = socket.plugEntryRadiusActions[0].units;
                    var control = new DepthActionSlider(range, units, radius: true);
                    panel.root.Add(control);
                    panel.root.Bind(serialized);
                    panel.Pump();
                    Assert.That(control.Q<UnityEngine.UIElements.Toggle>("editRadiusVisually"), Is.Null);
                    var multiplier = meters ? 1 : scale;
                    var calls = 0;
                    Undo.IncrementCurrentGroup();
                    control.EditRangeVisually((center, rotation, radius, minimum) => {
                        Assert.That(Vector3.Distance(center, obj.transform.TransformPoint(socket.position)), Is.LessThan(0.00001f));
                        Assert.That(Quaternion.Angle(rotation, obj.transform.rotation * Quaternion.Euler(socket.rotation)), Is.LessThan(0.01f));
                        Assert.That(radius, Is.EqualTo((minimum ? 0.01f : 0.1f) * multiplier).Within(0.000001f));
                        calls++;
                        return minimum ? 0.02f : 0.2f;
                    });
                    panel.Pump();
                    Assert.That(calls, Is.EqualTo(2));
                    Assert.That(range.vector2Value.x, Is.EqualTo(0.02f / multiplier).Within(0.000001f));
                    Assert.That(range.vector2Value.y, Is.EqualTo(0.2f / multiplier).Within(0.000001f));
                    Assert.That(control.value, Is.EqualTo(range.vector2Value));
                    Undo.PerformUndo();
                    serialized.Update();
                    panel.Pump();
                    Assert.That(range.vector2Value, Is.EqualTo(new Vector2(0.01f, 0.1f)));
                    Assert.That(control.value, Is.EqualTo(range.vector2Value));
                    Undo.PerformRedo();
                    serialized.Update();
                    panel.Pump();
                    Assert.That(range.vector2Value.y, Is.EqualTo(0.2f / multiplier).Within(0.000001f));
                    control.EditRangeVisually((center, rotation, radius, minimum) => -1);
                    panel.Pump();
                    Assert.That(range.vector2Value, Is.EqualTo(Vector2.zero));
                }
            } finally {
                Object.DestroyImmediate(parent);
                Object.DestroyImmediate(panelOwner);
            }
        }

        [Test]
        public void EntranceRadiusRingsRespectTheCapAndProjectDragsAcrossIt(
            [Values(.5f, 2f)] float scale,
            [Values] bool meters,
            [Values(0f, .02f)] float cap
        ) {
            var obj = new GameObject("Capped socket radius rings");
            try {
                obj.transform.SetPositionAndRotation(new Vector3(1, 2, 3), Quaternion.Euler(20, 30, 40));
                obj.transform.localScale = Vector3.one * scale;
                var socket = obj.AddComponent<VRCFuryHapticSocket>();
                socket.useRadiusOffset = true;
                socket.maxRadiusOffset = cap;
                socket.plugEntryRadiusActions.Add(new VRCFuryHapticSocket.PlugEntryRadiusAction());
                using (var serialized = new SerializedObject(socket)) {
                    var prop = serialized.FindProperty("plugEntryRadiusActions").GetArrayElementAtIndex(0).FindPropertyRelative("range");
                    var control = new DepthActionSlider(
                        prop,
                        meters ? VRCFuryHapticSocket.DepthActionUnits.Meters : VRCFuryHapticSocket.DepthActionUnits.Local,
                        radius: true
                    );
                    var center = obj.transform.position;
                    var rotation = obj.transform.rotation;
                    foreach (var minimum in new[] { false, true }) {
                        var ring = control.GetRadiusRing(center, rotation, minimum);
                        var handleRotation = rotation * Quaternion.Euler(0, 0, 10 + (minimum ? 45 : 0));
                        foreach (var input in new[] { .005f, .015f, .02f, .025f, .05f, .1f }) {
                            var radius = input * (meters ? 1 : scale);
                            var offset = cap > 0 ? Mathf.Min(radius, cap * scale) : radius;
                            var expectedCenter = center + rotation * Vector3.up * offset;
                            Assert.That(Vector3.Distance(ring.Center(radius), expectedCenter), Is.LessThan(.00001f));
                            foreach (var axis in new[] { Vector3.right, Vector3.up, Vector3.left, Vector3.down }) {
                                var point = ring.HandlePosition(axis, radius);
                                Assert.That(Vector3.Distance(point, expectedCenter + handleRotation * axis * radius), Is.LessThan(.00001f));
                                Assert.That(ring.Project(axis, point), Is.EqualTo(radius).Within(.00001f),
                                    $"cap={cap}, input={input}, minimum={minimum}, axis={axis}");
                            }
                        }
                    }
                }
            } finally {
                Object.DestroyImmediate(obj);
            }
        }

        [Test]
        public void MaximumRadiusRingCanBeDraggedBeyondItsPreviousValue() {
            var obj = new GameObject("Maximum socket radius ring");
            try {
                obj.transform.localScale = Vector3.one * 2;
                var socket = obj.AddComponent<VRCFuryHapticSocket>();
                socket.useRadiusOffset = true;
                socket.maxRadiusOffset = .02f;
                using (var serialized = new SerializedObject(socket)) {
                    var control = new DepthActionSlider(
                        serialized.FindProperty("maxRadiusOffset"),
                        VRCFuryHapticSocket.DepthActionUnits.Local,
                        radius: true
                    );
                    var ring = control.GetRadiusRing(Vector3.zero, Quaternion.identity, false);
                    Assert.That(ring.Center(.1f), Is.EqualTo(Vector3.up * .1f));
                    Assert.That(ring.Project(Vector3.right, ring.HandlePosition(Vector3.right, .1f)), Is.EqualTo(.1f).Within(.000001f));
                }
            } finally {
                Object.DestroyImmediate(obj);
            }
        }

        [Test]
        public void VisualDepthEditingUsesEntranceOrPlugTipAndRespectsUnits(
            [Values] bool isSocket,
            [Values] VRCFuryHapticSocket.DepthActionUnits units,
            [Values(0.5f, 2f)] float scale
        ) {
            var panelOwner = ScriptableObject.CreateInstance<Fields>();
            var parent = new GameObject("Depth parent");
            try {
                parent.transform.SetPositionAndRotation(new Vector3(1, 2, 3), Quaternion.Euler(20, 30, 40));
                parent.transform.localScale = Vector3.one * scale;
                var obj = new GameObject("Depth owner");
                obj.transform.SetParent(parent.transform, false);
                var offset = new Vector3(0.1f, 0.2f, 0.3f);
                var rotation = new Vector3(10, 20, 30);
                var action = new VRCFuryHapticSocket.DepthActionNew {
                    range = new Vector2(-0.1f, 0.2f),
                    units = units
                };
                UnityEngine.Component component;
                if (isSocket) {
                    var socket = obj.AddComponent<VRCFuryHapticSocket>();
                    socket.position = offset;
                    socket.rotation = rotation;
                    socket.depthActions2.Add(action);
                    component = socket;
                } else {
                    var plug = obj.AddComponent<VRCFuryHapticPlug>();
                    plug.position = offset;
                    plug.rotation = rotation;
                    plug.autoRenderer = plug.autoLength = plug.autoRadius = false;
                    plug.length = 0.4f;
                    plug.depthActions2.Add(action);
                    component = plug;
                }
                using (var serialized = new SerializedObject(component))
                using (var panel = new Panel(panelOwner)) {
                    var range = serialized.FindProperty("depthActions2").GetArrayElementAtIndex(0).FindPropertyRelative("range");
                    var control = new DepthActionSlider(range, units);
                    panel.root.Add(control);
                    panel.root.Bind(serialized);
                    panel.Pump();
                    var supported = !isSocket || units != VRCFuryHapticSocket.DepthActionUnits.Plugs;
                    Assert.That(control.Q<UnityEngine.UIElements.Toggle>("editDepthVisually"), Is.Null);
                    var worldRotation = obj.transform.rotation * Quaternion.Euler(rotation);
                    var center = obj.transform.TransformPoint(offset);
                    if (!isSocket) center += worldRotation * Vector3.forward * (0.4f * scale);
                    center += worldRotation * new Vector3(1, 1, 0).normalized * 0.02f;
                    var multiplier = units == VRCFuryHapticSocket.DepthActionUnits.Local ? scale
                        : units == VRCFuryHapticSocket.DepthActionUnits.Plugs ? 0.4f * scale : 1;
                    var calls = 0;
                    Undo.IncrementCurrentGroup();
                    control.EditRangeVisually((actualCenter, actualRotation, depth, minimum) => {
                        Assert.That(Vector3.Distance(actualCenter, center), Is.LessThan(0.00001f));
                        Assert.That(Quaternion.Angle(actualRotation, worldRotation), Is.LessThan(0.01f));
                        Assert.That(depth, Is.EqualTo((minimum ? -0.1f : 0.2f) * multiplier).Within(0.000001f));
                        calls++;
                        return minimum ? -0.3f : 0.4f;
                    });
                    panel.Pump();
                    Assert.That(calls, Is.EqualTo(supported ? 2 : 0));
                    if (!supported) {
                        Assert.That(range.vector2Value, Is.EqualTo(action.range));
                        return;
                    }
                    Assert.That(range.vector2Value.x, Is.EqualTo(-0.3f / multiplier).Within(0.000001f));
                    Assert.That(range.vector2Value.y, Is.EqualTo(0.4f / multiplier).Within(0.000001f));
                    Assert.That(control.value, Is.EqualTo(range.vector2Value));
                    Undo.PerformUndo();
                    serialized.Update();
                    panel.Pump();
                    Assert.That(range.vector2Value, Is.EqualTo(new Vector2(-0.1f, 0.2f)));
                    Undo.PerformRedo();
                    serialized.Update();
                    panel.Pump();
                    Assert.That(range.vector2Value.x, Is.EqualTo(-0.3f / multiplier).Within(0.000001f));
                }
            } finally {
                Object.DestroyImmediate(parent);
                Object.DestroyImmediate(panelOwner);
            }
        }

        [Test]
        public void VisualPlugDepthUsesAutomaticallyMeasuredLength() {
            var obj = new GameObject("Measured plug");
            var mesh = new Mesh {
                vertices = new[] {
                    new Vector3(-0.05f, 0, 0),
                    new Vector3(0.05f, 0, 0),
                    new Vector3(-0.05f, 0, 0.6f),
                    new Vector3(0.05f, 0, 0.6f)
                },
                triangles = new[] { 0, 2, 1, 1, 2, 3 }
            };
            try {
                mesh.RecalculateNormals();
                obj.transform.localScale = Vector3.one * 2;
                obj.AddComponent<MeshFilter>().sharedMesh = mesh;
                var renderer = obj.AddComponent<MeshRenderer>();
                var plug = obj.AddComponent<VRCFuryHapticPlug>();
                plug.autoRenderer = plug.autoRadius = plug.useBoneMask = false;
                plug.autoLength = true;
                plug.radius = 0.05f;
                plug.configureTpsMesh.Add(renderer);
                plug.depthActions2.Add(new VRCFuryHapticSocket.DepthActionNew {
                    range = new Vector2(-1, 0.5f),
                    units = VRCFuryHapticSocket.DepthActionUnits.Plugs
                });
                using (var serialized = new SerializedObject(plug)) {
                    var range = serialized.FindProperty("depthActions2").GetArrayElementAtIndex(0).FindPropertyRelative("range");
                    var control = new DepthActionSlider(range, VRCFuryHapticSocket.DepthActionUnits.Plugs);
                    var calls = 0;
                    control.EditRangeVisually((center, rotation, depth, minimum) => {
                        var offset = new Vector3(1, 1, 0).normalized * 0.02f;
                        Assert.That(Vector3.Distance(center, new Vector3(0, 0, 1.2f) + offset), Is.LessThan(0.00001f));
                        Assert.That(depth, Is.EqualTo(minimum ? -1.2f : 0.6f).Within(0.00001f));
                        calls++;
                        return depth;
                    });
                    Assert.That(calls, Is.EqualTo(2));
                    Assert.That(range.vector2Value, Is.EqualTo(new Vector2(-1, 0.5f)));
                }
            } finally {
                Object.DestroyImmediate(obj);
                Object.DestroyImmediate(mesh);
            }
        }
    }
}
