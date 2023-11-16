using UnityEditor.Animations;
using UnityEngine;

namespace VF.Utils.Controller {
    public class VFAInteger : VFAParam {
        public VFAInteger(AnimatorControllerParameter param) : base(param) {}

        public VFCondition IsEqualTo(float num) {
            return new VFCondition(new AnimatorCondition { mode = AnimatorConditionMode.Equals, parameter = Name(), threshold = num });
        }
        public VFCondition IsNotEqualTo(float num) {
            return new VFCondition(new AnimatorCondition { mode = AnimatorConditionMode.NotEqual, parameter = Name(), threshold = num });
        }
        public VFCondition IsGreaterThan(float num) {
            return new VFCondition(new AnimatorCondition { mode = AnimatorConditionMode.Greater, parameter = Name(), threshold = num });
        }
        public VFCondition IsLessThan(float num) {
            return new VFCondition(new AnimatorCondition { mode = AnimatorConditionMode.Less, parameter = Name(), threshold = num });
        }

        public override VFCondition IsFalse() {
            return IsEqualTo(0);
        }
    }
}
