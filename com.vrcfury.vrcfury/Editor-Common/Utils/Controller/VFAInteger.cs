using UnityEditor.Animations;
using UnityEngine;

namespace VF.Utils.Controller {
    internal class VFAInteger : VFAParam {
        private int def;

        public VFAInteger(string name, int def) : base(name) {
            this.def = def;
        }

        public VFCondition IsEqualTo(float num) {
            return new VFCondition(new AnimatorCondition { mode = AnimatorConditionMode.Equals, parameter = this, threshold = num });
        }
        public VFCondition IsNotEqualTo(float num) {
            return new VFCondition(new AnimatorCondition { mode = AnimatorConditionMode.NotEqual, parameter = this, threshold = num });
        }
        public VFCondition IsGreaterThan(float num) {
            return new VFCondition(new AnimatorCondition { mode = AnimatorConditionMode.Greater, parameter = this, threshold = num });
        }
        public VFCondition IsLessThan(float num) {
            return new VFCondition(new AnimatorCondition { mode = AnimatorConditionMode.Less, parameter = this, threshold = num });
        }
    }
}
