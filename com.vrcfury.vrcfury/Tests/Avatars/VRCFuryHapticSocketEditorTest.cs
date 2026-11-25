namespace VF.Tests {
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using NUnit.Framework;
    using UnityEngine;
    using UnityEngine.UIElements;
    using VF.Builder;
    using VF.Builder.Haptics;
    using VF.Component;
    using VF.Inspector;
    using VF.Feature.Base;
    using VF.Injector;
    using VF.Model.Feature;
    using VF.Service;
    using VF.Utils;
    using VRC.SDK3.Avatars.Components;
    using VRC.SDK3.Dynamics.Contact.Components;
    using Object = UnityEngine.Object;

    internal class VRCFuryHapticSocketEditorTest : SpsOptionsTestBase {
        [TestCase(false, false)]
        [TestCase(true, false)]
        [TestCase(false, true)]
        public void AnimationFilterEditorKeepsEachPairEnabled(bool plug, bool radius) {
            var root = new GameObject("Animation filters");
            try {
                UnityEngine.Component owner;
                if (plug) {
                    var component = root.AddComponent<VRCFuryHapticPlug>();
                    component.depthActions2.Add(new VRCFuryHapticSocket.DepthActionNew());
                    owner = component;
                } else {
                    var component = root.AddComponent<VRCFuryHapticSocket>();
                    component.depthActions2.Add(new VRCFuryHapticSocket.DepthActionNew());
                    component.plugEntryRadiusActions.Add(new VRCFuryHapticSocket.PlugEntryRadiusAction());
                    owner = component;
                }
                using var serialized = new UnityEditor.SerializedObject(owner);
                var action = serialized.FindProperty(radius ? "plugEntryRadiusActions" : "depthActions2").GetArrayElementAtIndex(0);
                var editor = radius ? new VRCFuryHapticSocketEditor.PlugEntryRadiusActionDrawer().CreatePropertyGUI(action)
                    : new VRCFuryHapticSocketEditor.DepthActionDrawer().CreatePropertyGUI(action);
                var toggles = editor.Query<UnityEngine.UIElements.Toggle>().ToList();
                foreach (var (first, second) in new[] { ("Allow Self", "Allow Others"), ("Allow Hole", "Allow Ring") }) {
                    var a = toggles.SingleOrDefault(t => t.name == first);
                    var b = toggles.SingleOrDefault(t => t.name == second);
                    if (first != "Allow Self" && !plug) {
                        Assert.That(a, Is.Null);
                        Assert.That(b, Is.Null);
                        continue;
                    }
                    Assert.That(a.value && b.value, Is.True);
                    a.value = false;
                    b.value = false;
                    var field = first == "Allow Self" ? "enableSelf" : "allowHole";
                    Assert.That(action.FindPropertyRelative(field).boolValue, Is.True, "Disabling the last option must enable its partner");
                }
            } finally { Object.DestroyImmediate(root); }
        }

        [TestCase(false)]
        [TestCase(true)]
        public void PlugEntryRadiusEditorRendersSliderAndPreservesEditableRange(bool existingPlugUnits) {
            var root = new GameObject("Radius editor");
            try {
                root.transform.localScale = Vector3.one * 2;
                var socket = root.AddComponent<VRCFuryHapticSocket>();
                socket.plugEntryRadiusActions.Add(new VRCFuryHapticSocket.PlugEntryRadiusAction());
                if (existingPlugUnits) socket.plugEntryRadiusActions[0].units = VRCFuryHapticSocket.DepthActionUnits.Plugs;
                using var serialized = new UnityEditor.SerializedObject(socket);
                var action = serialized.FindProperty("plugEntryRadiusActions").GetArrayElementAtIndex(0);
                var editor = new VRCFuryHapticSocketEditor.PlugEntryRadiusActionDrawer().CreatePropertyGUI(action);
                Assert.That(editor.Query<Label>().ToList().Any(label => label.text == "Reverse clip (Deprecated, Do not use)"), Is.False);
                var unitField = editor.Query<PopupField<string>>().ToList()
                    .Single(field => field.choices.SequenceEqual(new[] { "Local", "Meters" }));
                Assert.That(unitField.value, Is.EqualTo(existingPlugUnits ? "Plugs" : "Local"));
                Assert.That(socket.plugEntryRadiusActions[0].units, Is.EqualTo(existingPlugUnits
                    ? VRCFuryHapticSocket.DepthActionUnits.Plugs : VRCFuryHapticSocket.DepthActionUnits.Local));
                var control = editor.Q<DepthActionSlider>();
                Assert.That(control, Is.Not.Null);
                var slider = control.Q<MinMaxSlider>();
                Assert.That(slider, Is.Not.Null);
                Assert.That(slider.lowLimit, Is.Zero);
                control.SetValueWithoutNotify(new Vector2(0.02f, 0.5f));
                Assert.That(slider.value, Is.EqualTo(control.value));
                control.SetValueWithoutNotify(new Vector2(0.025f, 0.075f));
                Assert.That(slider.value, Is.EqualTo(control.value));
                Assert.That(control.Q<Label>("rangeMetersMin")?.text, Is.EqualTo(existingPlugUnits ? null : $"{0.05f:G4} m"));
                Assert.That(control.Q<Label>("rangeMetersMax")?.text, Is.EqualTo(existingPlugUnits ? null : $"{0.15f:G4} m"));
            } finally {
                Object.DestroyImmediate(root);
            }
        }
    }
}
