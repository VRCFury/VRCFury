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

namespace VF.Tests {
    [Category("VRCFury")]
    internal class EditorChangeTrackingTest {
        [TestCase(false)]
        [TestCase(true)]
        public void DebugRefreshesAfterChangesButDoesNotRebuildWhileIdle(bool element) {
            var owner = ScriptableObject.CreateInstance<Fields>();
            try {
                using (var panel = new Panel(owner)) {
                    var calls = 0;
                    var debug = element
                        ? VRCFuryEditorUtils.Debug(refreshElement: () => { calls++; return new Label("Debug"); })
                        : VRCFuryEditorUtils.Debug(refreshMessage: () => { calls++; return "Debug"; });
                    panel.root.Add(debug);
                    for (var i = 0; i < 30; i++) panel.Pump();
                    Assert.That(calls, Is.EqualTo(1), "An unchanged inspector must not rebuild once per second");
                    VF.Utils.EditorChanges.Changed();
                    VF.Utils.EditorChanges.Changed();
                    for (var i = 0; i < 15; i++) panel.Pump();
                    Assert.That(calls, Is.EqualTo(2), "Changes are coalesced into one refresh");
                    for (var i = 0; i < 15; i++) panel.Pump();
                    Assert.That(calls, Is.EqualTo(2));
                    debug.RemoveFromHierarchy();
                    VF.Utils.EditorChanges.Changed();
                    for (var i = 0; i < 15; i++) panel.Pump();
                    Assert.That(calls, Is.EqualTo(2), "Detached inspectors must stop updating");
                    panel.root.Add(debug);
                    for (var i = 0; i < 15; i++) panel.Pump();
                    Assert.That(calls, Is.EqualTo(3), "Reattached inspectors must see changes made while detached");
                }
            } finally {
                Object.DestroyImmediate(owner);
            }
        }

        [Test]
        public void ScheduledExceptionStopsFailingTimerWithoutBlockingOtherTimersOrBindings() {
            var owner = ScriptableObject.CreateInstance<Fields>();
            try {
                using (var serialized = new SerializedObject(owner))
                using (var panel = new Panel(owner)) {
                    var failingCalls = 0;
                    var healthyCalls = 0;
                    var bindingCalls = 0;
                    var property = serialized.FindProperty("number");
                    panel.root.Add(VRCFuryEditorUtils.OnChange(property, () => bindingCalls++));
                    panel.Pump();
                    panel.root.ScheduleSafe(() => {
                        failingCalls++;
                        throw new InvalidOperationException("Scheduled test failure");
                    }).Every(100);
                    panel.root.ScheduleSafe(() => healthyCalls++).Every(100);
                    UnityEngine.TestTools.LogAssert.Expect(
                        LogType.Exception,
                        "InvalidOperationException: Scheduled test failure"
                    );
                    property.intValue = 42;
                    serialized.ApplyModifiedProperties();
                    panel.Pump();
                    Assert.That(failingCalls, Is.EqualTo(1));
                    Assert.That(healthyCalls, Is.GreaterThan(0));
                    Assert.That(bindingCalls, Is.EqualTo(1));
                    var previousHealthyCalls = healthyCalls;
                    panel.Pump();
                    Assert.That(failingCalls, Is.EqualTo(1), "A failing timer must stay paused.");
                    Assert.That(healthyCalls, Is.GreaterThan(previousHealthyCalls));
                }
            } finally {
                Object.DestroyImmediate(owner);
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
        public void SocketPlugRadiusRangeStoresLocalUnits() {
            var panelOwner = ScriptableObject.CreateInstance<Fields>();
            var obj = new GameObject("Weighted socket");
            obj.transform.localScale = Vector3.one * 2;
            try {
                var socket = obj.AddComponent<VRCFuryHapticSocket>();
                socket.depthActions2.Add(new VRCFuryHapticSocket.DepthActionNew());
                using (var serialized = new SerializedObject(socket))
                using (var panel = new Panel(panelOwner)) {
                    var action = serialized.FindProperty("depthActions2").GetArrayElementAtIndex(0);
                    var editor = new VRCFuryHapticSocketEditor.DepthActionDrawer().CreatePropertyGUI(action);
                    panel.root.Add(editor);
                    panel.root.Bind(serialized);
                    panel.Pump();
                    var toggle = editor.Q<UnityEngine.UIElements.Toggle>("multiplyPlugRadius");
                    var field = editor.Q<FloatField>("plugRadiusForFullWeight");
                    Assert.That(editor.Q("plugRadiusControls").Query<UnityEngine.UIElements.Slider>().ToList(), Is.Empty);
                    Assert.That(editor.Q("plugRadiusControls").Query<MinMaxSlider>().ToList(), Has.Count.EqualTo(1));
                    Assert.That(editor.Q("plugRadiusControls").Query<FloatField>().ToList(), Has.Count.EqualTo(2));
                    var minimumField = editor.Q<FloatField>("plugRadiusForZeroWeight");
                    var minimumRadius = action.FindPropertyRelative("plugRadiusForZeroWeight");
                    Assert.That(field.isDelayed, Is.True);
                    var radius = action.FindPropertyRelative("plugRadiusForFullWeight");
                    Assert.That(toggle.value, Is.False);
                    toggle.value = true;
                    panel.Pump();
                    Assert.That(radius.floatValue, Is.EqualTo(0.125f));
                    Assert.That(field.value, Is.EqualTo(0.125f));
                    Assert.That(editor.Q<Label>("plugRadiusMeters").text, Is.EqualTo("0.25 m"));
                    obj.transform.localScale = Vector3.one * 4;
                    panel.Pump();
                    Assert.That(radius.floatValue, Is.EqualTo(0.125f));
                    Assert.That(field.value, Is.EqualTo(0.125f), "Scale changes must not modify the local radius");
                    Assert.That(editor.Q<Label>("plugRadiusMeters").text, Is.EqualTo("0.5 m"));
                    field.value = 2;
                    panel.Pump();
                    Assert.That(radius.floatValue, Is.EqualTo(2), "Typing must not clamp to the old slider range");
                    Assert.That(editor.Q<Label>("plugRadiusMeters").text, Is.EqualTo("8 m"));
                    minimumField.value = 0.05f;
                    panel.Pump();
                    Assert.That(minimumRadius.floatValue, Is.EqualTo(0.05f));
                    Assert.That(editor.Q("plugRadiusControls").Q<Label>("rangeMetersMin").text, Is.EqualTo("0.2 m"));
                    Undo.IncrementCurrentGroup();
                    editor.Q("plugRadiusControls").Q<MinMaxSlider>().value = new Vector2(0.1f, 0.3f);
                    panel.Pump();
                    Assert.That(minimumRadius.floatValue, Is.EqualTo(0.1f));
                    Assert.That(radius.floatValue, Is.EqualTo(0.3f));
                    Undo.PerformUndo();
                    serialized.Update();
                    panel.Pump();
                    Assert.That(minimumRadius.floatValue, Is.EqualTo(0.05f));
                    Assert.That(radius.floatValue, Is.EqualTo(2f));
                    field.value = -1;
                    panel.Pump();
                    Assert.That(radius.floatValue, Is.Zero);
                    Assert.That(minimumRadius.floatValue, Is.Zero);
                    Assert.That(field.value, Is.Zero);
                    Assert.That(toggle.value, Is.False);
                    toggle.value = true;
                    panel.Pump();
                    Assert.That(radius.floatValue, Is.EqualTo(0.0625f));
                    toggle.value = false;
                    panel.Pump();
                    Assert.That(radius.floatValue, Is.Zero);
                    panel.root.Unbind();
                    panel.root.Bind(serialized);
                    panel.Pump();
                    Undo.IncrementCurrentGroup();
                    toggle.value = true;
                    panel.Pump();
                    Assert.That(radius.floatValue, Is.EqualTo(0.0625f));
                    Undo.PerformUndo();
                    serialized.Update();
                    panel.Pump();
                    Assert.That(radius.floatValue, Is.Zero);
                    Assert.That(toggle.value, Is.False);
                    Assert.That(editor.Q("plugRadiusControls").resolvedStyle.display, Is.EqualTo(DisplayStyle.None));
                    Undo.PerformRedo();
                    serialized.Update();
                    panel.Pump();
                    Assert.That(radius.floatValue, Is.EqualTo(0.0625f));
                    Assert.That(toggle.value, Is.True);
                    Assert.That(field.value, Is.EqualTo(0.0625f));
                    Assert.That(editor.Q("plugRadiusControls").resolvedStyle.display, Is.EqualTo(DisplayStyle.Flex));
                    Undo.IncrementCurrentGroup();
                    field.value = 0.125f;
                    panel.Pump();
                    Assert.That(radius.floatValue, Is.EqualTo(0.125f));
                    Undo.PerformUndo();
                    serialized.Update();
                    panel.Pump();
                    Assert.That(radius.floatValue, Is.EqualTo(0.0625f), "One numeric edit must undo in one step");
                    Assert.That(field.value, Is.EqualTo(0.0625f));
                }
            } finally {
                Object.DestroyImmediate(obj);
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

        [Test]
        public void SocketMenuToggleClearsAdditionalActionsInTheSameUndoOperation() {
            var panelOwner = ScriptableObject.CreateInstance<Fields>();
            var obj = new GameObject("Socket menu actions");
            UnityEditor.Editor editor = null;
            try {
                var socket = obj.AddComponent<VRCFuryHapticSocket>();
                socket.activeActions.actions.Add(new VF.Model.StateAction.FxFloatAction { name = "Test", value = 1 });
                editor = UnityEditor.Editor.CreateEditor(socket);
                using (var panel = new Panel(panelOwner)) {
                    var gui = editor.CreateInspectorGUI();
                    panel.root.Add(gui);
                    panel.root.Bind(editor.serializedObject);
                    panel.Pump();
                    var labels = gui.Query<Label>().ToList();
                    var menuSection = labels.Single(label => label.text == "Menu Toggle").parent.parent;
                    Assert.That(menuSection.Query<Label>().ToList().Any(label =>
                        label.text == "Additional Actions While Menu Item is Active"), Is.True);
                    var checkbox = labels.Single(label => label.text == "Enable Menu Toggle").parent.parent
                        .Q<UnityEngine.UIElements.Toggle>();
                    Undo.IncrementCurrentGroup();
                    checkbox.value = false;
                    panel.Pump();
                    Assert.That(socket.addMenuItem, Is.False);
                    Assert.That(socket.activeActions.actions, Is.Empty);
                    Undo.PerformUndo();
                    panel.Pump();
                    Assert.That(socket.addMenuItem, Is.True);
                    Assert.That(socket.activeActions.actions, Has.Count.EqualTo(1));
                    Assert.That(checkbox.value, Is.True);
                    Undo.PerformRedo();
                    panel.Pump();
                    Assert.That(socket.addMenuItem, Is.False);
                    Assert.That(socket.activeActions.actions, Is.Empty);
                    Assert.That(checkbox.value, Is.False);
                }
            } finally {
                if (editor != null) Object.DestroyImmediate(editor);
                Object.DestroyImmediate(obj);
                Object.DestroyImmediate(panelOwner);
            }
        }

        [TestCase("depthActions2")]
        [TestCase("plugEntryRadiusActions")]
        [TestCase("activeActions.actions")]
        public void ActionListCheckboxFollowsUndoRedoAndExternalChanges(string path) {
            var panelOwner = ScriptableObject.CreateInstance<Fields>();
            var obj = new GameObject("Action list");
            try {
                var socket = obj.AddComponent<VRCFuryHapticSocket>();
                using (var serialized = new SerializedObject(socket))
                using (var panel = new Panel(panelOwner)) {
                    var property = serialized.FindProperty(path);
                    property.arraySize = 1;
                    serialized.ApplyModifiedPropertiesWithoutUndo();
                    var gui = VRCFuryEditorUtils.CheckboxList(property, "Enable actions", "", "Actions");
                    panel.root.Add(gui);
                    panel.root.Bind(serialized);
                    panel.Pump();
                    var checkbox = gui.Q<UnityEngine.UIElements.Toggle>();
                    var section = gui.Children().ElementAt(1);
                    Assert.That(checkbox.value, Is.True);
                    Undo.IncrementCurrentGroup();
                    checkbox.value = false;
                    panel.Pump();
                    Assert.That(property.arraySize, Is.Zero);
                    Undo.PerformUndo();
                    panel.Pump();
                    Assert.That(property.arraySize, Is.EqualTo(1));
                    Assert.That(checkbox.value, Is.True, "Undo must re-enable the checkbox when it restores actions");
                    Assert.That(section.resolvedStyle.display, Is.EqualTo(DisplayStyle.Flex));
                    Undo.PerformRedo();
                    panel.Pump();
                    Assert.That(checkbox.value, Is.False);
                    Assert.That(section.resolvedStyle.display, Is.EqualTo(DisplayStyle.None));
                    using (var external = new SerializedObject(socket)) {
                        external.FindProperty(path).arraySize = 1;
                        external.ApplyModifiedProperties();
                    }
                    panel.Pump();
                    Assert.That(checkbox.value, Is.True, "Edits through another SerializedObject must update the checkbox");
                    Assert.That(section.resolvedStyle.display, Is.EqualTo(DisplayStyle.Flex));
                }
            } finally {
                Object.DestroyImmediate(obj);
                Object.DestroyImmediate(panelOwner);
            }
        }

        [Test]
        public void DepthRadiusMeterTimerSurvivesUndoRemovingItsAction() {
            var panelOwner = ScriptableObject.CreateInstance<Fields>();
            var obj = new GameObject("Removed depth action");
            try {
                var socket = obj.AddComponent<VRCFuryHapticSocket>();
                using (var serialized = new SerializedObject(socket))
                using (var panel = new Panel(panelOwner)) {
                    var list = serialized.FindProperty("depthActions2");
                    Undo.IncrementCurrentGroup();
                    var action = VRCFuryEditorUtils.AddToList(list);
                    var gui = new VRCFuryHapticSocketEditor.DepthActionDrawer().CreatePropertyGUI(action);
                    panel.root.Add(gui);
                    panel.root.Bind(serialized);
                    panel.Pump();
                    Assert.That(gui.Q<Label>("plugRadiusMeters").text, Is.EqualTo("0 m"));
                    Undo.PerformUndo();
                    serialized.Update();
                    // The list may not detach its old row until after scheduled callbacks have run.
                    panel.Pump();
                    Assert.That(list.arraySize, Is.Zero);
                    Assert.That(gui.Q<Label>("plugRadiusMeters").text, Is.Empty);
                    Undo.PerformRedo();
                    serialized.Update();
                    panel.Pump();
                    Assert.That(list.arraySize, Is.EqualTo(1));
                    Assert.That(gui.Q<Label>("plugRadiusMeters").text, Is.EqualTo("0 m"));
                }
            } finally {
                Object.DestroyImmediate(obj);
                Object.DestroyImmediate(panelOwner);
            }
        }

        private class Fields : ScriptableObject {
            public int number;
            public bool flag;
            public string text = "";
            public Object reference;
            public HumanBodyBones bone;
            public List<int> numbers = new() { 1 };
        }

        [TestCase("number")]
        [TestCase("flag")]
        [TestCase("text")]
        [TestCase("reference")]
        [TestCase("bone")]
        public void TracksScalarChangesWithoutInitialOrDuplicateCallbacks(string field) {
            var owner = ScriptableObject.CreateInstance<Fields>();
            try {
                using (var serialized = new SerializedObject(owner))
                using (var panel = new Panel(owner)) {
                    var calls = 0;
                    var property = serialized.FindProperty(field);
                    panel.root.Add(VRCFuryEditorUtils.OnChange(property, () => calls++));
                    panel.Pump();
                    Assert.That(calls, Is.Zero);
                    switch (property.propertyType) {
                        case SerializedPropertyType.Integer: property.intValue = 2; break;
                        case SerializedPropertyType.Boolean: property.boolValue = true; break;
                        case SerializedPropertyType.String: property.stringValue = "changed"; break;
                        case SerializedPropertyType.ObjectReference: property.objectReferenceValue = owner; break;
                        case SerializedPropertyType.Enum: property.enumValueIndex = 1; break;
                    }
                    serialized.ApplyModifiedProperties();
                    panel.Pump();
                    Assert.That(calls, Is.EqualTo(1));
                    panel.root.Bind(serialized);
                    panel.Pump();
                    Assert.That(calls, Is.EqualTo(1));
                }
            } finally { Object.DestroyImmediate(owner); }
        }

        [Test]
        public void ManualFieldTrackingSurvivesRebindingAndReattachment() {
            var owner = ScriptableObject.CreateInstance<Fields>();
            try {
                using (var serialized = new SerializedObject(owner))
                using (var panel = new Panel(owner)) {
                    var property = serialized.FindProperty("number");
                    var field = new IntegerField();
                    field.TrackProperty(property, p => field.SetValueWithoutNotify(p.intValue));
                    panel.root.Add(field);
                    panel.root.Bind(serialized);
                    panel.Pump();
                    for (var value = 1; value <= 3; value++) {
                        if (value == 2) {
                            panel.root.Unbind();
                            panel.root.Bind(serialized);
                        }
                        if (value == 3) {
                            field.RemoveFromHierarchy();
                            panel.root.Add(field);
                        }
                        panel.Pump();
                        property.intValue = value;
                        serialized.ApplyModifiedProperties();
                        panel.Pump();
                        Assert.That(field.value, Is.EqualTo(value));
                    }
                }
            } finally { Object.DestroyImmediate(owner); }
        }

        // A detached editor panel exercises real bindings without opening a window.
        private sealed class Panel : IDisposable {
            public readonly IPanel panel;
            private readonly PropertyInfo clock;
            private readonly object previousClock;
            private long time;
            private long GetTime() => time;
            public VisualElement root => panel.visualTree;
            public Panel(ScriptableObject owner) {
                var type = typeof(VisualElement).Assembly.GetType("UnityEngine.UIElements.Panel");
                panel = (IPanel)type.GetMethod("CreateEditorPanel", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
                    .Invoke(null, new object[] { owner });
                clock = type.GetProperty("TimeSinceStartup", BindingFlags.Static | BindingFlags.NonPublic);
                previousClock = clock.GetValue(null);
                var getTime = type.GetMethod("TimeSinceStartupMs");
                time = (long)getTime.Invoke(getTime.IsStatic ? null : panel, null);
                clock.SetValue(null, Delegate.CreateDelegate(clock.PropertyType, this,
                    GetType().GetMethod(nameof(GetTime), BindingFlags.Instance | BindingFlags.NonPublic)));
            }
            public void Pump() {
                var update = panel.GetType().GetMethod("UpdateBindings", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                var updateData = panel.GetType().GetMethod("UpdateDataBinding", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                var scheduler = panel.GetType().GetProperty("scheduler", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).GetValue(panel);
                var tick = scheduler.GetType().GetMethod("UpdateScheduledEvents", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                // Advance Unity's UI test clock instead of sleeping through binding throttles.
                for (var i = 0; i < 3; i++) {
                    time += 110;
                    tick.Invoke(scheduler, null);
                    update.Invoke(panel, null);
                    // Unity 6 advances the binding frame in a separate data-binding phase.
                    updateData?.Invoke(panel, null);
                }
            }
            public void Dispose() {
                try { ((IDisposable)panel).Dispose(); }
                finally { clock.SetValue(null, previousClock); }
            }
        }

        [Test]
        public void PropertyTrackingSurvivesRefreshAndReattachment() {
            var owner = ScriptableObject.CreateInstance<Fields>();
            try {
                using (var serialized = new SerializedObject(owner))
                using (var panel = new Panel(owner)) {
                    var calls = 0;
                    var property = serialized.FindProperty("number");
                    var gui = VRCFuryEditorUtils.RefreshOnChange(() => {
                        calls++;
                        return VRCFuryEditorUtils.OnChange(property, () => { });
                    }, property);
                    Assert.That(gui.Query<PropertyField>().ToList(), Is.Empty);
                    Assert.That(gui.Query<IntegerField>().ToList(), Is.Empty);
                    panel.root.Add(gui);
                    panel.root.Bind(serialized);
                    panel.Pump();
                    var baseline = calls;
                    for (var i = 1; i <= 2; i++) {
                        property.intValue = i;
                        serialized.ApplyModifiedProperties();
                        panel.Pump();
                        Assert.That(calls, Is.EqualTo(baseline + i));
                    }
                    gui.RemoveFromHierarchy();
                    property.intValue = 10;
                    serialized.ApplyModifiedProperties();
                    panel.Pump();
                    Assert.That(calls, Is.EqualTo(baseline + 2), "Detached watchers must stop tracking.");
                    panel.root.Add(gui);
                    panel.Pump();
                    baseline = calls;
                    property.intValue = 3;
                    serialized.ApplyModifiedProperties();
                    panel.Pump();
                    Assert.That(calls, Is.EqualTo(baseline + 1));
                    panel.root.Unbind();
                    panel.root.Bind(serialized);
                    panel.Pump();
                    baseline = calls;
                    property.intValue = 4;
                    serialized.ApplyModifiedProperties();
                    panel.Pump();
                    Assert.That(calls, Is.EqualTo(baseline + 1), "Tracking must survive rebinding without detaching.");
                }
            } finally { Object.DestroyImmediate(owner); }
        }

        [Test]
        public void ListsTrackOnlySizeChanges() {
            var owner = ScriptableObject.CreateInstance<Fields>();
            try {
                using (var serialized = new SerializedObject(owner))
                using (var panel = new Panel(owner)) {
                    var calls = 0;
                    var property = serialized.FindProperty("numbers");
                    panel.root.Add(VRCFuryEditorUtils.OnChange(property, () => calls++));
                    panel.Pump();
                    var baseline = calls;
                    property.GetArrayElementAtIndex(0).intValue = 2;
                    serialized.ApplyModifiedProperties();
                    panel.Pump();
                    Assert.That(calls, Is.EqualTo(baseline));
                    property.arraySize = 2;
                    serialized.ApplyModifiedProperties();
                    panel.Pump();
                    Assert.That(calls, Is.EqualTo(baseline + 1));
                }
            } finally { Object.DestroyImmediate(owner); }
        }
    }
}
