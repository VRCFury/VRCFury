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
    [Category("VRCFury")]
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

        private EditorChangeTrackingTest.Panel CreatePanel() {
            var panel = new EditorChangeTrackingTest.Panel(editor);
            typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.UIElements.UIElementsEditorUtility")
                .GetMethod("AddDefaultEditorStyleSheets", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
                .Invoke(null, new object[] { panel.root });
            panel.root.style.width = 600;
            panel.root.style.height = 2000;
            return panel;
        }

        private static void Pump(EditorChangeTrackingTest.Panel panel) {
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
