using UnityEditor.Animations;
using UnityEngine;

namespace VF.Utils.Controller {
    public class VFAFloat : VFAParam {
        public VFAFloat(AnimatorControllerParameter param) : base(param) {}

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

        public override VFCondition IsFalse() {
            return IsLessThanOrEquals(0);
        }

        public float GetDefault() {
            return param.defaultFloat;
        }
    }
}
