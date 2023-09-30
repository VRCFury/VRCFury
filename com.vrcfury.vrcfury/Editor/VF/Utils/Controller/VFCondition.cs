using System.Collections.Generic;
using UnityEditor.Animations;
using VF.Builder;

namespace VF.Utils.Controller {
    public class VFCondition {
        internal IEnumerable<IEnumerable<AnimatorCondition>> transitions;
        private VFCondition() {
            transitions = new List<List<AnimatorCondition>>();
        }
        public VFCondition(AnimatorCondition cond) {
            var transition = new List<AnimatorCondition> { cond };
            transitions = new List<List<AnimatorCondition>> { transition };
        }
        public VFCondition(IEnumerable<IEnumerable<AnimatorCondition>> transitions) {
            this.transitions = transitions;
        }
        public VFCondition And(VFCondition other) {
            return new VFCondition(AnimatorConditionLogic.And(transitions, other.transitions));
        }
        public VFCondition Or(VFCondition other) {
            return new VFCondition(AnimatorConditionLogic.Or(transitions, other.transitions));
        }
        public VFCondition Not() {
            return new VFCondition(AnimatorConditionLogic.Not(transitions));
        }

        public static VFCondition Never() {
            return new VFCondition();
        }
    }
}