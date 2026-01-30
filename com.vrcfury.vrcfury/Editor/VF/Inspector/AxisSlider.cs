using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using VF.Utils;

namespace VF.Inspector {
    
    internal class AxisSlider : BaseField<float> {
        private readonly Slider slider;
        private readonly FloatField text;
        private readonly float minValue;
        private readonly float maxValue;

        public AxisSlider(SerializedProperty prop, float min, float max) : this(min, max) {
            bindingPath = prop.propertyPath;
        }

        public AxisSlider(float min, float max) : base(null, null) {
            Clear();
            minValue = min;
            maxValue = max;

            if (maxValue < minValue) {
                Add(new Label("min is greater than max, please use less than or equal to min. (Only devs should see this)"));
                return;
            }
            
            style.flexDirection = FlexDirection.Row;
            slider = new Slider(minValue, maxValue).Margin(0).FlexShrink(1);
            slider.style.marginRight = 5;
            slider.RegisterValueChangedCallback(e => {
                // When using slider, store in steps of 1%
                Changed((float)Math.Round(e.newValue, 2));
            });
            Add(slider);
            text = new FloatField().Margin(0).FlexBasis(35);
            //text.formatString = "0.#";
            text.isDelayed = true;
            text.RegisterValueChangedCallback(e => {
                // When using text box, store in steps of 0.1%
                Changed(e.newValue);
            });
            Add(text);
        }

        private void Changed(float newValue) {
            newValue = Mathf.Clamp(newValue, minValue, maxValue);
            value = newValue;
            // This usually happens automatically, but might not if the user (for example) changes
            // the text box from -1 to -2, because it got clamped back above
            SetValueWithoutNotify(value);
        }

        public override void SetValueWithoutNotify(float newValue) {
            base.SetValueWithoutNotify(newValue);
            slider.SetValueWithoutNotify(newValue);
            text.SetValueWithoutNotify(newValue);
        }
    }
}