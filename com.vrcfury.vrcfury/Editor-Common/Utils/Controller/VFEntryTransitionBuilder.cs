using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor.Animations;

namespace VF.Utils.Controller {
    internal sealed class VFEntryTransitionBuilder {
        private readonly IList<VFEntryTransition> ownerTransitions;
        private readonly List<VFEntryTransition> activeTransitions = new List<VFEntryTransition>();

        internal VFEntryTransitionBuilder(IList<VFEntryTransition> ownerTransitions, VFEntryTransition transition) {
            this.ownerTransitions = ownerTransitions ?? throw new ArgumentNullException(nameof(ownerTransitions));
            if (transition == null) throw new ArgumentNullException(nameof(transition));
            activeTransitions.Add(transition);
        }

        public VFEntryTransitionBuilder When() {
            return this;
        }

        public VFEntryTransitionBuilder When(VFCondition cond) {
            if (cond == null) {
                return this;
            }

            var branches = cond.transitions ?? Array.Empty<AnimatorCondition[]>();
            if (branches.Length == 0) {
                foreach (var transition in activeTransitions) {
                    transition.conditions = Array.Empty<AnimatorCondition>();
                }
                return this;
            }

            var seed = activeTransitions.First();
            TrimToSeed(seed);
            seed.conditions = branches[0].ToArray();
            for (var i = 1; i < branches.Length; i++) {
                var clone = (VFEntryTransition)seed.Clone(
                    new Dictionary<VFState, VFState>(),
                    new Dictionary<VFStateMachine, VFStateMachine>()
                );
                if (clone.destinationState == null) clone.destinationState = seed.destinationState;
                if (clone.destinationStateMachine == null) clone.destinationStateMachine = seed.destinationStateMachine;
                clone.isExit = seed.isExit;
                clone.conditions = branches[i].ToArray();
                ownerTransitions.Add(clone);
                activeTransitions.Add(clone);
            }
            return this;
        }

        private void TrimToSeed(VFEntryTransition seed) {
            for (var i = activeTransitions.Count - 1; i >= 1; i--) {
                ownerTransitions.Remove(activeTransitions[i]);
                activeTransitions.RemoveAt(i);
            }
            if (!ReferenceEquals(activeTransitions[0], seed)) {
                activeTransitions.Clear();
                activeTransitions.Add(seed);
            }
        }
    }
}
