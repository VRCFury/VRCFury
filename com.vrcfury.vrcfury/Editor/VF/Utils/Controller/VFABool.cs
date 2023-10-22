using UnityEditor.Animations;
using UnityEngine;

namespace VF.Utils.Controller {
    public class VFABool : VFAParam {
        public VFABool(AnimatorControllerParameter param) : base(param) {}

        public VFCondition IsTrue() {
            return new VFCondition(new AnimatorCondition { mode = AnimatorConditionMode.If, parameter = Name(), threshold = 0 });
        }
        public VFCondition IsFalse() {
            return new VFCondition(new AnimatorCondition { mode = AnimatorConditionMode.IfNot, parameter = Name(), threshold = 0 });
        }
    }
}
