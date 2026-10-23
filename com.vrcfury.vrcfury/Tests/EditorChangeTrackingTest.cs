using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using VF.Inspector;
using Object = UnityEngine.Object;

namespace VF.Tests {
    [Category("VRCFury")]
    internal class EditorChangeTrackingTest {
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
