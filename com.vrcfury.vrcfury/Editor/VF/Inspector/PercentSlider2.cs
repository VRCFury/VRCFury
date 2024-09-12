using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using VF.Utils;

namespace VF.Inspector {
    internal class PercentSlider2 : BaseField<float> {
        private readonly Slider slider;
        private readonly FloatField text;

        public PercentSlider2(SerializedProperty prop) : this() {
            bindingPath = prop.propertyPath;
        }

        public PercentSlider2() : base(null, null) {
            Clear();

            style.flexDirection = FlexDirection.Row;
            slider = new Slider(0, 1).Margin(0).FlexShrink(1);
            slider.style.marginRight = 5;
            slider.RegisterValueChangedCallback(e => {
                // When using slider, store in steps of 1%
                Changed((float)Math.Round(e.newValue, 2));
            });
            Add(slider);
            text = new FloatField().Margin(0).FlexBasis(35);
            text.formatString = "0.#";
            text.isDelayed = true;
            text.RegisterValueChangedCallback(e => {
                // When using text box, store in steps of 0.1%
                Changed(e.newValue * 0.01f);
            });
            Add(text);
        }

        private void Changed(float newValue) {
            newValue = Mathf.Clamp(newValue, 0, 1);
            value = newValue;
            // This usually happens automatically, but might not if the user (for example) changes
            // the text box from 0 to -1, because it got clamped back above
            SetValueWithoutNotify(value);
        }

        public override void SetValueWithoutNotify(float newValue) {
            base.SetValueWithoutNotify(newValue);
            slider.SetValueWithoutNotify(newValue);
            text.SetValueWithoutNotify(newValue * 100);
        }
    }
}
