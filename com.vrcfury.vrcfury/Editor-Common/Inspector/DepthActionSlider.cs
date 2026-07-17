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
        
        public DepthActionSlider(SerializedProperty prop, VRCFuryHapticSocket.DepthActionUnits units) : base(null,null) {
            bindingPath = prop.propertyPath;
            Clear();

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

            slider = new MinMaxSlider {
                highLimit = 3,
                lowLimit = -1
            };
            slider.RegisterValueChangedCallback(e => {
                // When using slider, store in steps of 0.2
                Changed(new Vector2(
                    (float)Math.Round(e.newValue.x, 2),
                    (float)Math.Round(e.newValue.y, 2)
                ));
            });
            c.Add(slider);

            output.style.marginTop = 20;

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
        }

        private void Changed(Vector2 newValue) {
            newValue.x = Mathf.Min(newValue.x, newValue.y);
            newValue.y = Mathf.Max(newValue.x, newValue.y);
            newValue.x = Mathf.Clamp(newValue.x, -1, 3);
            newValue.y = Mathf.Clamp(newValue.y, -1, 3);
            value = newValue;
            // This usually happens automatically, but might not if the user sets the float
            // to something invalid
            SetValueWithoutNotify(value);
        }

        public override void SetValueWithoutNotify(Vector2 newValue) {
            base.SetValueWithoutNotify(newValue);
            slider.SetValueWithoutNotify(newValue);
            xField.SetValueWithoutNotify(newValue.x);
            yField.SetValueWithoutNotify(newValue.y);
        }
    }
}
