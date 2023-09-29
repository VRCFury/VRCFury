using UnityEngine;

namespace VF.Utils.Controller {
    public class VFAParam {
        protected readonly AnimatorControllerParameter param;
        protected VFAParam(AnimatorControllerParameter param) {
            this.param = param;
        }
        public string Name() {
            return param.name;
        }
    }
}