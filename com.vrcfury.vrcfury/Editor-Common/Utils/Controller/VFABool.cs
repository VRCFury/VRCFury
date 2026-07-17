using UnityEditor.Animations;
using UnityEngine;

namespace VF.Utils.Controller {
    internal class VFABool : VFAParam {
        private bool def;

        public VFABool(string name, bool def) : base(name) {
            this.def = def;
        }

        public VFCondition IsTrue() {
            return Is(true);
        }
        public VFCondition IsFalse() {
            return Is(false);
        }
        public VFCondition Is(bool state) {
            return new VFCondition(new AnimatorCondition { mode = state ? AnimatorConditionMode.If : AnimatorConditionMode.IfNot, parameter = this, threshold = 0 });
        }

        public VFAFloat AsFloat() {
            return new VFAFloat(name, def ? 1 : 0);
        }
    }
}
