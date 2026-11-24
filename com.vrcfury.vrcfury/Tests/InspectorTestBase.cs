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
    internal abstract class InspectorTestBase {
        protected class Fields : ScriptableObject {
            public int number;
            public bool flag;
            public string text = "";
            public Object reference;
            public HumanBodyBones bone;
            public List<int> numbers = new() { 1 };
        }

        // A detached editor panel exercises real bindings without opening a window.
        internal sealed class Panel : IDisposable {
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
    }
}
