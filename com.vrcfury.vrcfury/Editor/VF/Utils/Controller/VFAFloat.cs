using UnityEditor.Animations;
using UnityEngine;

namespace VF.Utils.Controller {
    internal class VFAFloat : VFAParam {
        private float def;

        public VFAFloat(string name, float def) : base(name) {
            this.def = def;
        }

        public VFCondition IsGreaterThan(float num) {
            return new VFCondition(new AnimatorCondition { mode = AnimatorConditionMode.Greater, parameter = Name(), threshold = num });
        }
        public VFCondition IsLessThan(float num) {
            return new VFCondition(new AnimatorCondition { mode = AnimatorConditionMode.Less, parameter = Name(), threshold = num });
        }
        public VFCondition IsGreaterThanOrEquals(float num) {
            return IsLessThan(num).Not();
        }
        public VFCondition IsLessThanOrEquals(float num) {
            return IsGreaterThan(num).Not();
        }

        public float GetDefault() {
            return def;
        }
    }
}
