using System;
using System.Linq;
using UnityEditor.Animations;

namespace VF.Utils.Controller {
    internal class VFEntryTransition {
        private readonly Func<AnimatorTransition> transitionProvider;
        public VFEntryTransition(Func<AnimatorTransition> transitionProvider) {
            this.transitionProvider = transitionProvider;
        }

        public VFEntryTransition When() {
            var transition = transitionProvider();
            return this;
        }
        public VFEntryTransition When(VFCondition cond) {
            foreach (var t in cond.transitions) {
                var transition = transitionProvider();
                transition.conditions = t.ToArray();
            }
            return this;
        }
    }
}
