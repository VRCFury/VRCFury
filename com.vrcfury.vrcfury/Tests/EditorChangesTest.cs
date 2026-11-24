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

    internal class EditorChangesTest : InspectorTestBase {
        [Test]
        public void EditorChangeTrackingCoalescesNotificationsAndStopsWhenDetached() {
            var owner = ScriptableObject.CreateInstance<Fields>();
            try {
                using (var panel = new Panel(owner)) {
                    var element = new VisualElement();
                    var calls = 0;
                    element.TrackEditorChanges(() => calls++);
                    panel.root.Add(element);
                    for (var i = 0; i < 20; i++) panel.Pump();
                    Assert.That(calls, Is.Zero, "Idle panels must not poll.");
                    VF.Utils.EditorChanges.Changed();
                    VF.Utils.EditorChanges.Changed();
                    panel.Pump();
                    Assert.That(calls, Is.EqualTo(1));
                    for (var i = 0; i < 20; i++) panel.Pump();
                    Assert.That(calls, Is.EqualTo(1));
                    VF.Utils.EditorChanges.Changed();
                    element.RemoveFromHierarchy();
                    panel.Pump();
                    Assert.That(calls, Is.EqualTo(1), "Detaching must cancel pending refreshes.");
                    panel.root.Add(element);
                    panel.Pump();
                    Assert.That(calls, Is.EqualTo(2), "Reattachment must refresh a canceled change.");
                    element.RemoveFromHierarchy();
                    VF.Utils.EditorChanges.Changed();
                    panel.Pump();
                    Assert.That(calls, Is.EqualTo(2));
                    panel.root.Add(element);
                    panel.Pump();
                    Assert.That(calls, Is.EqualTo(3));
                }
            } finally {
                Object.DestroyImmediate(owner);
            }
        }
    }
}
