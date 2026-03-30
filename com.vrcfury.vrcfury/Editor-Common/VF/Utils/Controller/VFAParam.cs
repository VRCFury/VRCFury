using UnityEngine;

namespace VF.Utils.Controller {
    internal abstract class VFAParam {
        public static implicit operator string(VFAParam d) => d.Name();
        
        protected readonly string name;
        protected VFAParam(string name) {
            this.name = name;
        }
        public string Name() {
            return name;
        }

        public override string ToString() {
            return Name();
        }
    }
}
