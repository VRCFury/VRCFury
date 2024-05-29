using UnityEditor.Animations;
using UnityEngine;

namespace VF.Utils.Controller {
    internal class VFABool : VFAParam {
        private bool def;

        public VFABool(string name, bool def) : base(name) {
            this.def = def;
        }

        public VFCondition IsTrue() {
            return new VFCondition(new AnimatorCondition { mode = AnimatorConditionMode.If, parameter = Name(), threshold = 0 });
        }
        public VFCondition IsFalse() {
            return new VFCondition(new AnimatorCondition { mode = AnimatorConditionMode.IfNot, parameter = Name(), threshold = 0 });
        }

        public VFAFloat AsFloat() {
            return new VFAFloat(name, def ? 1 : 0);
        }
    }
}
