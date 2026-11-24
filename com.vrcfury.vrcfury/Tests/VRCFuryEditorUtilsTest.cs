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

    internal class VRCFuryEditorUtilsTest : InspectorTestBase {
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

        [Test]
        public void ArrayElementTrackingCanDetachAfterDeletionAndReattachAfterUndo() {
            var owner = ScriptableObject.CreateInstance<Fields>();
            try {
                using (var serialized = new SerializedObject(owner))
                using (var panel = new Panel(owner)) {
                    var calls = 0;
                    var list = serialized.FindProperty("numbers");
                    var tracker = VRCFuryEditorUtils.OnChange(list.GetArrayElementAtIndex(0), () => calls++);
                    panel.root.Add(tracker);
                    panel.Pump();
                    serialized.FindProperty("number").intValue = 10;
                    serialized.ApplyModifiedProperties();
                    panel.Pump();
                    Assert.That(calls, Is.Zero, "Other fields must not trigger the array element callback.");
                    list.GetArrayElementAtIndex(0).intValue = 2;
                    serialized.ApplyModifiedProperties();
                    panel.Pump();
                    Assert.That(calls, Is.EqualTo(1));
                    Undo.IncrementCurrentGroup();
                    list.ClearArray();
                    serialized.ApplyModifiedProperties();
                    panel.Pump();
                    Assert.That(calls, Is.EqualTo(1), "Deleted properties must not invoke callbacks with invalid handles.");
                    Assert.DoesNotThrow(() => tracker.RemoveFromHierarchy());
                    Undo.PerformUndo();
                    serialized.Update();
                    panel.root.Add(tracker);
                    panel.Pump();
                    list.GetArrayElementAtIndex(0).intValue = 3;
                    serialized.ApplyModifiedProperties();
                    panel.Pump();
                    Assert.That(calls, Is.EqualTo(2));
                }
            } finally { Object.DestroyImmediate(owner); }
        }
    }
}
