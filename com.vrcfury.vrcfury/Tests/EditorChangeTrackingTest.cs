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
                // Advance Unity's UI test clock instead of sleeping through binding throttles.
                for (var i = 0; i < 3; i++) {
                    time += 110;
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
