using System;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using VF.Component;
using VF.Utils;

namespace VF.Inspector {
    internal class DepthActionSlider : BaseField<Vector2> {
        private readonly MinMaxSlider slider;
        private readonly FloatField xField;
        private readonly FloatField yField;
        private readonly Label meters;
        private readonly UnityEngine.Component owner;
        private readonly bool radius;
        
        public DepthActionSlider(SerializedProperty prop, VRCFuryHapticSocket.DepthActionUnits units, bool radius = false) : base(null,null) {
            this.radius = radius;
            bindingPath = prop.propertyPath;
            Clear();
            style.flexDirection = FlexDirection.Column;

            var output = new VisualElement();
            output.Row();

            xField = new FloatField().FlexBasis(50);
            xField.isDelayed = true;
            xField.RegisterValueChangedCallback(e => {
                var v = e.newValue;
                Changed(new Vector2(
                    v,
                    Mathf.Max(v, value.y)
                ));
            });
            output.Add(xField);

            var c = new VisualElement();

            if (!radius) {
                var test = new Label(units == VRCFuryHapticSocket.DepthActionUnits.Plugs ? "Fully\n\u2193 Inserted" : units == VRCFuryHapticSocket.DepthActionUnits.Local ? "Tip inside\n\u2193 1 local-unit" : "Tip\n\u2193 inside 1m");
                test.style.position = Position.Absolute;
                test.style.bottom = 15;
                test.style.fontSize = 9;
                c.Add(test);

                var test2 = new Label("Tip at\n\u2193 Entrance");
                test2.style.position = Position.Absolute;
                test2.style.bottom = 15;
                test2.style.left = Length.Percent(25);
                test2.style.fontSize = 9;
                c.Add(test2);
        
                var test3 = new Label(units == VRCFuryHapticSocket.DepthActionUnits.Plugs ? "Tip 3 plug-lengths\naway \u2193" : units == VRCFuryHapticSocket.DepthActionUnits.Local ? "Tip 3 local-units\naway \u2193" : "Tip 3m\naway \u2193");
                test3.style.position = Position.Absolute;
                test3.style.bottom = 15;
                test3.style.right = 0;
                test3.style.fontSize = 9;
                test3.style.unityTextAlign = TextAnchor.UpperRight;
                c.Add(test3);
            }

            slider = new MinMaxSlider {
                highLimit = radius ? 0.5f : 3,
                lowLimit = radius ? 0 : -1
            };
            slider.RegisterValueChangedCallback(e => {
                // Keep more precision for the smaller radius ranges.
                Changed(new Vector2(
                    (float)Math.Round(e.newValue.x, radius ? 3 : 2),
                    (float)Math.Round(e.newValue.y, radius ? 3 : 2)
                ));
            });
            c.Add(slider);

            output.style.marginTop = radius ? 0 : 20;

            output.Add(c.FlexGrow(1).FlexBasis(0));

            yField = new FloatField().FlexBasis(50);
            yField.isDelayed = true;
            yField.RegisterValueChangedCallback(e => {
                var v = e.newValue;
                Changed(new Vector2(
                    Mathf.Min(v, value.x),
                    v
                ));
            });
            output.Add(yField);

            output.FlexGrow(1);
            Add(output);
            if (units == VRCFuryHapticSocket.DepthActionUnits.Local
                && prop.serializedObject.targetObject is UnityEngine.Component component) {
                owner = component;
                meters = VRCFuryEditorUtils.WrappedLabel("");
                Add(meters);
                meters.schedule.Execute(UpdateMeters).Every(100);
                UpdateMeters();
            }
        }

        private void UpdateMeters() {
            if (meters == null || owner == null) return;
            var scale = owner.transform.lossyScale.x;
            meters.text = radius
                ? $"Min: {value.x * scale:G4} m    Max: {value.y * scale:G4} m"
                : $"In: {value.x * scale:G4} m    Out: {value.y * scale:G4} m";
        }

        private void Changed(Vector2 newValue) {
            newValue.x = Mathf.Min(newValue.x, newValue.y);
            newValue.y = Mathf.Max(newValue.x, newValue.y);
            if (radius) {
                newValue.x = Mathf.Max(0, newValue.x);
                newValue.y = Mathf.Max(0, newValue.y);
            }
            value = newValue;
            // This usually happens automatically, but might not if the user sets the float
            // to something invalid
            SetValueWithoutNotify(value);
        }

        public override void SetValueWithoutNotify(Vector2 newValue) {
            base.SetValueWithoutNotify(newValue);
            if (!radius) slider.lowLimit = Mathf.Min(slider.lowLimit, newValue.x);
            slider.highLimit = Mathf.Max(slider.highLimit, newValue.y);
            slider.SetValueWithoutNotify(newValue);
            xField.SetValueWithoutNotify(newValue.x);
            yField.SetValueWithoutNotify(newValue.y);
            UpdateMeters();
        }
    }
}
