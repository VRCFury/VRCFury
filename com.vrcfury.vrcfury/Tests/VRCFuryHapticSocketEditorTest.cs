using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using VF.Builder.Haptics;
using VF.Component;
using VF.Inspector;
using Object = UnityEngine.Object;
using Mode = VF.Component.VRCFuryHapticSocket.AddLight;

namespace VF.Tests {
    internal class VRCFuryHapticSocketEditorTest {
        private GameObject obj;
        private VRCFuryHapticSocket socket;
        private UnityEditor.Editor editor;

        [SetUp]
        public void SetUp() {
            obj = new GameObject("Guided path editor");
            socket = obj.AddComponent<VRCFuryHapticSocket>();
            editor = UnityEditor.Editor.CreateEditor(socket);
        }

        [TearDown]
        public void TearDown() {
            Object.DestroyImmediate(editor);
            Object.DestroyImmediate(obj);
        }

        [Test]
        public void GuidedPathListIsReorderableLimitedAndRestoresModeWhenEmptied() {
            using (var panel = CreatePanel()) {
                var gui = editor.CreateInspectorGUI();
                panel.root.Add(gui);
                panel.root.Bind(editor.serializedObject);
                Pump(panel);
                Assert.That(gui.Query<ListView>().ToList().Any(view => view.bindingPath == "guidedPathStops"), Is.False);
                Assert.That(gui.Query<Label>().ToList().Any(label => label.text == "Entrance"), Is.False);
                Assert.That(gui.Q("guidedPathEntrance").ClassListContains("unity-collection-view--with-border"), Is.False);
                Assert.That(ModeFields(gui), Is.EqualTo(1));
                Assert.That(ExitCheckbox(gui), Is.Null);
                for (var i = 0; i < SpsTags.MaxUserPathStops + 2; i++) {
                    var add = i == 0
                        ? gui.Query<Button>().ToList().Single(button => button.text == "Add Guided Path Node")
                        : gui.Query<ListView>().ToList().Single(view => view.bindingPath == "guidedPathStops")
                            .parent.Q<Button>(BaseListView.footerAddButtonName);
                    using (var evt = NavigationSubmitEvent.GetPooled()) {
                        evt.target = add;
                        add.SendEvent(evt);
                    }
                    Pump(panel);
                    Assert.That(socket.guidedPathStops.Count, Is.EqualTo(Mathf.Min(i + 1, SpsTags.MaxUserPathStops)));
                    Assert.That(gui.Query<ListView>().ToList().Single(view => view.bindingPath == "guidedPathStops").reorderable, Is.True);
                    AssertExitInLastStop(gui, socket.guidedPathStops.Count - 1);
                    AssertCollapseBindings(gui);
                }
                Assert.That(ModeFields(gui), Is.Zero);
                Assert.That(ExitCheckbox(gui).value, Is.False, "Auto paths must not collapse by default.");
                Assert.That(socket.addLight, Is.EqualTo(Mode.Auto));

                var stops = editor.serializedObject.FindProperty("guidedPathStops");
                Undo.IncrementCurrentGroup();
                stops.ClearArray();
                editor.serializedObject.ApplyModifiedProperties();
                Pump(panel);
                Assert.That(ModeFields(gui), Is.EqualTo(1));
                Assert.That(ExitCheckbox(gui), Is.Null);
                Assert.That(gui.Query<ListView>().ToList().Any(view => view.bindingPath == "guidedPathStops"), Is.False);
                Assert.That(gui.Query<Label>().ToList().Any(label => label.text == "Entrance"), Is.False);
                Assert.That(gui.Query<Button>().ToList().Any(button => button.text == "Add Guided Path Node"), Is.True);
                Undo.PerformUndo();
                Pump(panel);
                Assert.That(ModeFields(gui), Is.Zero);
                Assert.That(ExitCheckbox(gui), Is.Not.Null);
                Assert.That(socket.guidedPathStops, Has.Count.EqualTo(SpsTags.MaxUserPathStops));
            }
        }

        [Test]
        public void EntranceSettingsStayFixedAndExitCollapseFollowsReordering() {
            socket.addLight = Mode.Hole;
            socket.useRadiusOffset = true;
            socket.maxRadiusOffset = .1f;
            for (var i = 0; i < 2; i++) {
                socket.guidedPathStops.Add(new VRCFuryHapticSocket.GuidedPathStop {
                    transform = VF.Utils.GameObjects.Create($"Stop {i}", obj),
                    useRadiusOffset = i == 0,
                    shrink = i == 1
                });
            }
            var first = socket.guidedPathStops[0].transform;
            var second = socket.guidedPathStops[1].transform;
            editor.serializedObject.Update();
            using (var panel = CreatePanel()) {
                var gui = editor.CreateInspectorGUI();
                panel.root.Add(gui);
                panel.root.Bind(editor.serializedObject);
                Pump(panel);
                var list = gui.Query<ListView>().ToList().Single(view => view.bindingPath == "guidedPathStops");
                var entrance = gui.Q("guidedPathEntrance");
                Assert.That(entrance.parent, Is.SameAs(list.parent), "Entrance must be outside the draggable/removable rows.");
                Assert.That(list.parent[0], Is.SameAs(entrance));
                Assert.That(entrance.Q<Label>().worldBound.x, Is.EqualTo(StopRow(gui, 0)[0].worldBound.x).Within(.5f));
                Assert.That(entrance.Query<UnityEngine.UIElements.Toggle>().ToList()
                    .Any(field => field.bindingPath == "useRadiusOffset"), Is.True);
                Assert.That(entrance.Q("limitRadiusOffset"), Is.Not.Null);
                Assert.That(entrance.Q("maxRadiusOffsetControls"), Is.Not.Null);
                AssertExitInLastStop(gui, 1);
                Assert.That(ExitCheckbox(gui).value, Is.True);
                AssertCollapseBindings(gui);

                Undo.IncrementCurrentGroup();
                var stops = editor.serializedObject.FindProperty("guidedPathStops");
                stops.MoveArrayElement(0, 1);
                editor.serializedObject.ApplyModifiedProperties();
                Pump(panel);
                Assert.That(socket.guidedPathStops.Select(stop => stop.transform), Is.EqualTo(new[] { second, first }));
                Assert.That(socket.guidedPathStops.Select(stop => stop.shrink), Is.EqualTo(new[] { true, false }));
                Assert.That(gui.Q("guidedPathEntrance"), Is.SameAs(entrance));
                AssertExitInLastStop(gui, 1);
                Assert.That(ExitCheckbox(gui).value, Is.True);
                AssertCollapseBindings(gui);

                Undo.IncrementCurrentGroup();
                CollapseCheckbox(gui, 1).value = true;
                Pump(panel);
                Assert.That(socket.guidedPathStops[0].shrink, Is.True);
                Assert.That(socket.guidedPathStops[1].shrink, Is.True, "Node 2 must control Node 3's collapse property.");
                Undo.PerformUndo();
                Pump(panel);
                Assert.That(socket.guidedPathStops[0].shrink, Is.True);
                Assert.That(socket.guidedPathStops[1].shrink, Is.False);
                AssertCollapseBindings(gui);

                stops.DeleteArrayElementAtIndex(1);
                editor.serializedObject.ApplyModifiedProperties();
                Pump(panel);
                AssertExitInLastStop(gui, 0);
                Assert.That(ExitCheckbox(gui).value, Is.True);
                AssertCollapseBindings(gui);
            }
        }

        [Test]
        public void NodeRadiusLimitSharesCheckboxBehaviorAndUsesItsOwnTransform() {
            obj.transform.localScale = Vector3.one * 2;
            var guide = VF.Utils.GameObjects.Create("Guide", obj);
            guide.localPosition = new Vector3(.1f, .2f, .3f);
            guide.localRotation = Quaternion.Euler(20, 30, 40);
            guide.localScale = Vector3.one * 3;
            var other = VF.Utils.GameObjects.Create("Other guide", obj);
            other.localScale = Vector3.one * 4;
            var node = new VRCFuryHapticSocket.GuidedPathStop {
                transform = guide,
                useRadiusOffset = true
            };
            socket.guidedPathStops.Add(node);
            editor.serializedObject.Update();
            using (var panel = CreatePanel()) {
                var gui = editor.CreateInspectorGUI();
                panel.root.Add(gui);
                panel.root.Bind(editor.serializedObject);
                Pump(panel);
                VisualElement Field() => StopRow(gui, 0).Q("maxRadiusOffsetControls");
                UnityEngine.UIElements.Toggle Checkbox() => StopRow(gui, 0).Q<UnityEngine.UIElements.Toggle>("limitRadiusOffset");
                Assert.That(Checkbox().value, Is.False);
                Assert.That(Field().resolvedStyle.display, Is.EqualTo(DisplayStyle.None));
                Checkbox().value = true;
                Pump(panel);
                Assert.That(node.maxRadiusOffset, Is.Zero, "Checking the limit must not assign a radius.");
                Assert.That(Field().resolvedStyle.display, Is.EqualTo(DisplayStyle.Flex));
                Field().Q<FloatField>().value = .1f;
                Pump(panel);
                Assert.That(node.maxRadiusOffset, Is.EqualTo(.1f));
                Assert.That(socket.maxRadiusOffset, Is.Zero);
                Assert.That(Field().Q<Label>("rangeMetersMax").text, Is.EqualTo("0.6 m"));
                Undo.IncrementCurrentGroup();
                Field().Q<DepthActionSlider>().EditRangeVisually((center, rotation, radius, minimum) => {
                    Assert.That(Vector3.Distance(center, guide.worldPosition), Is.LessThan(.00001f));
                    Assert.That(Quaternion.Angle(rotation, guide.worldRotation), Is.LessThan(.001f));
                    Assert.That(radius, Is.EqualTo(.6f).Within(.000001f));
                    Assert.That(minimum, Is.False);
                    return 1.2f;
                });
                Pump(panel);
                Assert.That(socket.guidedPathStops[0].maxRadiusOffset, Is.EqualTo(.2f).Within(.000001f));
                Undo.PerformUndo();
                Pump(panel);
                Assert.That(socket.guidedPathStops[0].maxRadiusOffset, Is.EqualTo(.1f));
                Assert.That(Checkbox().value, Is.True);

                var transform = editor.serializedObject.FindProperty("guidedPathStops.Array.data[0].transform");
                transform.objectReferenceValue = (Transform)other;
                editor.serializedObject.ApplyModifiedProperties();
                Pump(panel);
                Assert.That(Field().Q<Label>("rangeMetersMax").text, Is.EqualTo("0.8 m"));
                Undo.IncrementCurrentGroup();
                Checkbox().value = false;
                Pump(panel);
                Assert.That(socket.guidedPathStops[0].maxRadiusOffset, Is.Zero);
                Assert.That(Field().resolvedStyle.display, Is.EqualTo(DisplayStyle.None));
                Undo.PerformUndo();
                Pump(panel);
                Assert.That(socket.guidedPathStops[0].maxRadiusOffset, Is.EqualTo(.1f));
                Assert.That(Checkbox().value, Is.True);
                Assert.That(Field().resolvedStyle.display, Is.EqualTo(DisplayStyle.Flex));
            }
        }

        [TestCase((int)Mode.Auto)]
        [TestCase((int)Mode.Hole)]
        [TestCase((int)Mode.Ring)]
        [TestCase((int)Mode.RingOneWay)]
        public void ExitCollapseUsesModeAndTracksUndo(int initialMode) {
            socket.addLight = (Mode)initialMode;
            socket.guidedPathStops.Add(new VRCFuryHapticSocket.GuidedPathStop { transform = obj.transform });
            editor.serializedObject.Update();
            using (var panel = CreatePanel()) {
                var gui = editor.CreateInspectorGUI();
                panel.root.Add(gui);
                panel.root.Bind(editor.serializedObject);
                Pump(panel);
                var initiallyCollapsed = socket.addLight == Mode.Hole;
                Assert.That(ModeFields(gui), Is.Zero);
                Assert.That(ExitCheckbox(gui).value, Is.EqualTo(initiallyCollapsed));
                Undo.IncrementCurrentGroup();
                ExitCheckbox(gui).value = !initiallyCollapsed;
                Pump(panel);
                Assert.That(socket.addLight, Is.EqualTo(initiallyCollapsed ? Mode.Ring : Mode.Hole));
                Assert.That(ExitCheckbox(gui).value, Is.EqualTo(!initiallyCollapsed));
                Undo.PerformUndo();
                Pump(panel);
                Assert.That(socket.addLight, Is.EqualTo((Mode)initialMode));
                Assert.That(ExitCheckbox(gui).value, Is.EqualTo(initiallyCollapsed));
                Undo.PerformRedo();
                Pump(panel);
                Assert.That(socket.addLight, Is.EqualTo(initiallyCollapsed ? Mode.Ring : Mode.Hole));
                Assert.That(ExitCheckbox(gui).value, Is.EqualTo(!initiallyCollapsed));
                ExitCheckbox(gui).value = initiallyCollapsed;
                Pump(panel);
                Assert.That(socket.addLight, Is.EqualTo(initiallyCollapsed ? Mode.Hole : Mode.Ring));
            }
        }

        private InspectorTestBase.Panel CreatePanel() {
            var panel = new InspectorTestBase.Panel(editor);
            typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.UIElements.UIElementsEditorUtility")
                .GetMethod("AddDefaultEditorStyleSheets", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
                .Invoke(null, new object[] { panel.root });
            panel.root.style.width = 600;
            panel.root.style.height = 2000;
            return panel;
        }

        private static void Pump(InspectorTestBase.Panel panel) {
            for (var i = 0; i < 3; i++) {
                panel.Pump();
                panel.panel.GetType().GetMethod("ValidateLayout", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                    .Invoke(panel.panel, null);
            }
        }

        private static int ModeFields(VisualElement gui) => gui.Query<PopupField<string>>().ToList()
            .Count(field => field.choices.Contains("One-Way Ring (Uncommon)"));

        private static void AssertExitInLastStop(VisualElement gui, int index) {
            Assert.That(StopRow(gui, index).Contains(ExitCheckbox(gui)), Is.True);
        }

        private void AssertCollapseBindings(VisualElement gui) {
            for (var i = 0; i < socket.guidedPathStops.Count; i++) {
                var row = i == 0 ? gui.Q("guidedPathEntrance") : StopRow(gui, i - 1);
                var checkbox = CollapseCheckbox(gui, i);
                Assert.That(row.Contains(checkbox), Is.True);
                Assert.That(checkbox.value, Is.EqualTo(socket.guidedPathStops[i].shrink));
                var stop = StopRow(gui, i);
                Assert.That(stop[0], Is.TypeOf<Label>());
                Assert.That(((Label)stop[0]).text, Is.EqualTo($"Node {i + 2}"));
                Assert.That(stop[0].style.unityFontStyleAndWeight.value, Is.EqualTo(FontStyle.Bold));
                Assert.That(stop.Q<ObjectField>().label, Is.Null.Or.Empty);
                var radiusOffset = stop.Query<UnityEngine.UIElements.Toggle>().ToList().Single(field =>
                    field.bindingPath == $"guidedPathStops.Array.data[{i}].useRadiusOffset");
                Assert.That(radiusOffset.value, Is.EqualTo(socket.guidedPathStops[i].useRadiusOffset));
            }
            Assert.That(StopRow(gui, socket.guidedPathStops.Count - 1).Query<UnityEngine.UIElements.Toggle>().ToList()
                .Any(field => field.bindingPath?.EndsWith(".shrink") == true), Is.False);
        }

        private static VisualElement StopRow(VisualElement gui, int index) => gui.Query<ObjectField>().ToList()
            .Single(field => field.bindingPath == $"guidedPathStops.Array.data[{index}].transform").parent.parent;

        private static UnityEngine.UIElements.Toggle CollapseCheckbox(VisualElement gui, int targetStop) =>
            gui.Query<UnityEngine.UIElements.Toggle>().ToList().Single(field =>
                field.bindingPath == $"guidedPathStops.Array.data[{targetStop}].shrink");

        private static UnityEngine.UIElements.Toggle ExitCheckbox(VisualElement gui) => gui.Query<Label>().ToList()
            .SingleOrDefault(label => label.text == "Collapse plug at exit")?.parent.parent.Q<UnityEngine.UIElements.Toggle>();
    }
}

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

    internal class VRCFuryHapticSocketEditorRangesTest : InspectorTestBase {
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
                    VF.Utils.EditorChanges.Changed();
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
        public void SocketRadiusCapCheckboxKeepsZeroUntilEditedAndFollowsUndo() {
            var panelOwner = ScriptableObject.CreateInstance<Fields>();
            var obj = new GameObject("Socket radius cap");
            UnityEditor.Editor editor = null;
            try {
                var socket = obj.AddComponent<VRCFuryHapticSocket>();
                socket.useRadiusOffset = true;
                Assert.That(socket.maxRadiusOffset, Is.Zero);
                editor = UnityEditor.Editor.CreateEditor(socket);
                using (var panel = new Panel(panelOwner)) {
                    UnityEngine.UIElements.Toggle checkbox = null;
                    VisualElement field = null;
                    void Render() {
                        panel.root.Unbind();
                        panel.root.Clear();
                        var gui = editor.CreateInspectorGUI();
                        panel.root.Add(gui);
                        panel.root.Bind(editor.serializedObject);
                        panel.Pump();
                        checkbox = gui.Q<UnityEngine.UIElements.Toggle>("limitRadiusOffset");
                        field = gui.Q("maxRadiusOffsetControls");
                    }
                    Render();
                    Assert.That(checkbox.value, Is.False);
                    Assert.That(field.resolvedStyle.display, Is.EqualTo(DisplayStyle.None));
                    checkbox.value = true;
                    for (var i = 0; i < 5; i++) panel.Pump();
                    Assert.That(socket.maxRadiusOffset, Is.Zero, "Enabling the editor must not assign a radius.");
                    Assert.That(checkbox.value, Is.True);
                    Assert.That(field.resolvedStyle.display, Is.EqualTo(DisplayStyle.Flex));
                    Render();
                    Assert.That(checkbox.value, Is.False, "Zero must reopen unchecked.");
                    Assert.That(field.resolvedStyle.display, Is.EqualTo(DisplayStyle.None));
                    checkbox.value = true;
                    panel.Pump();
                    field.Q<FloatField>().value = .025f;
                    panel.Pump();
                    Assert.That(socket.maxRadiusOffset, Is.EqualTo(.025f));
                    Render();
                    Assert.That(checkbox.value, Is.True, "A nonzero cap must reopen checked.");
                    Assert.That(field.resolvedStyle.display, Is.EqualTo(DisplayStyle.Flex));
                    Undo.IncrementCurrentGroup();
                    checkbox.value = false;
                    panel.Pump();
                    Assert.That(socket.maxRadiusOffset, Is.Zero);
                    Assert.That(field.resolvedStyle.display, Is.EqualTo(DisplayStyle.None));
                    Undo.PerformUndo();
                    panel.Pump();
                    Assert.That(socket.maxRadiusOffset, Is.EqualTo(.025f));
                    Assert.That(checkbox.value, Is.True);
                    Assert.That(field.resolvedStyle.display, Is.EqualTo(DisplayStyle.Flex));
                    Undo.PerformRedo();
                    panel.Pump();
                    Assert.That(socket.maxRadiusOffset, Is.Zero);
                    Assert.That(checkbox.value, Is.False);
                    Assert.That(field.resolvedStyle.display, Is.EqualTo(DisplayStyle.None));
                }
            } finally {
                if (editor != null) Object.DestroyImmediate(editor);
                Object.DestroyImmediate(obj);
                Object.DestroyImmediate(panelOwner);
            }
        }

        [Test]
        public void SocketRadiusCapUsesSingleSliderAndGizmoAtTheSocket(
            [Values(.5f, 2f)] float scale,
            [Values(0f, .025f)] float initialRadius
        ) {
            var panelOwner = ScriptableObject.CreateInstance<Fields>();
            var obj = new GameObject("Socket radius cap gizmo");
            UnityEditor.Editor editor = null;
            try {
                obj.transform.SetPositionAndRotation(new Vector3(1, 2, 3), Quaternion.Euler(20, 30, 40));
                obj.transform.localScale = Vector3.one * scale;
                var socket = obj.AddComponent<VRCFuryHapticSocket>();
                socket.useRadiusOffset = true;
                socket.maxRadiusOffset = initialRadius;
                socket.position = new Vector3(.1f, .2f, .3f);
                socket.rotation = new Vector3(10, 20, 30);
                editor = UnityEditor.Editor.CreateEditor(socket);
                using (var panel = new Panel(panelOwner)) {
                    var gui = editor.CreateInspectorGUI();
                    panel.root.Add(gui);
                    panel.root.Bind(editor.serializedObject);
                    panel.Pump();
                    var checkbox = gui.Q<UnityEngine.UIElements.Toggle>("limitRadiusOffset");
                    var field = gui.Q("maxRadiusOffsetControls");
                    var control = field.Q<DepthActionSlider>();
                    Assert.That(field.Query<FloatField>().ToList(), Has.Count.EqualTo(1));
                    Assert.That(field.Query<UnityEngine.UIElements.Slider>().ToList(), Has.Count.EqualTo(1));
                    Assert.That(field.Query<MinMaxSlider>().ToList(), Is.Empty);
                    checkbox.value = true;
                    panel.Pump();
                    Assert.That(socket.maxRadiusOffset, Is.EqualTo(initialRadius));
                    var expectedRotation = obj.transform.rotation * Quaternion.Euler(socket.rotation);
                    var expectedCenter = obj.transform.TransformPoint(socket.position);
                    var calls = 0;
                    Undo.IncrementCurrentGroup();
                    control.EditRangeVisually((center, rotation, radius, minimum) => {
                        Assert.That(minimum, Is.False);
                        Assert.That(Vector3.Distance(center, expectedCenter), Is.LessThan(.00001f));
                        Assert.That(Quaternion.Angle(rotation, expectedRotation), Is.LessThan(.01f));
                        Assert.That(radius, Is.EqualTo(initialRadius * scale).Within(.000001f));
                        calls++;
                        return .1f * scale;
                    });
                    panel.Pump();
                    Assert.That(calls, Is.EqualTo(1));
                    Assert.That(socket.maxRadiusOffset, Is.EqualTo(.1f).Within(.000001f));
                    Assert.That(field.Q<Label>("rangeMetersMax").text, Is.EqualTo($"{.1f * scale:G4} m"));
                    Assert.That(field.Q<Label>("rangeMetersMin"), Is.Null);
                    Undo.PerformUndo();
                    editor.serializedObject.Update();
                    panel.Pump();
                    Assert.That(socket.maxRadiusOffset, Is.EqualTo(initialRadius));
                    Assert.That(control.value.y, Is.EqualTo(initialRadius));
                    checkbox.value = true;
                    panel.Pump();
                    field.Q<UnityEngine.UIElements.Slider>().value = .125f;
                    panel.Pump();
                    Assert.That(socket.maxRadiusOffset, Is.EqualTo(.125f));
                    field.Q<FloatField>().value = 2;
                    panel.Pump();
                    Assert.That(socket.maxRadiusOffset, Is.EqualTo(2), "The text field must allow values above the default slider range.");
                    checkbox.value = false;
                    panel.Pump();
                    control.EditRangeVisually((center, rotation, radius, minimum) => {
                        Assert.Fail("An unchecked cap must not draw a handle.");
                        return radius;
                    });
                }
            } finally {
                if (editor != null) Object.DestroyImmediate(editor);
                Object.DestroyImmediate(obj);
                Object.DestroyImmediate(panelOwner);
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
    }
}
