using UnityEngine;

namespace VF.Utils.Controller {
    public abstract class VFAParam {
        public static implicit operator string(VFAParam d) => d.Name();
        
        protected readonly AnimatorControllerParameter param;
        protected VFAParam(AnimatorControllerParameter param) {
            this.param = param;
        }
        public string Name() {
            return param.name;
        }
        public abstract VFCondition IsFalse();
    }
}
