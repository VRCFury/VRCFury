using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor.Animations;

namespace VF.Utils.Controller {
    internal sealed class VFTransitionBuilder {
        private readonly IList<VFTransition> ownerTransitions;
        private readonly List<VFTransition> activeTransitions = new List<VFTransition>();

        internal VFTransitionBuilder(IList<VFTransition> ownerTransitions, VFTransition transition) {
            this.ownerTransitions = ownerTransitions ?? throw new ArgumentNullException(nameof(ownerTransitions));
            if (transition == null) throw new ArgumentNullException(nameof(transition));
            activeTransitions.Add(transition);
        }

        public VFTransitionBuilder When() {
            return this;
        }

        public VFTransitionBuilder When(VFCondition cond) {
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
                var clone = (VFTransition)seed.Clone(
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

        public VFTransitionBuilder WithTransitionToSelf() {
            foreach (var transition in activeTransitions) {
                transition.canTransitionToSelf = true;
            }
            return this;
        }

        public VFTransitionBuilder Interruptable() {
            foreach (var transition in activeTransitions) {
                transition.interruptionSource = TransitionInterruptionSource.Destination;
            }
            return this;
        }

        public VFTransitionBuilder WithTransitionDurationSeconds(float time) {
            if (time < 0f) return this;
            foreach (var transition in activeTransitions) {
                transition.duration = time;
            }
            return this;
        }

        public VFTransitionBuilder WithTransitionExitTime(float time) {
            if (time < 0f) return this;
            foreach (var transition in activeTransitions) {
                transition.hasExitTime = true;
                transition.exitTime = time;
            }
            return this;
        }

        public void AddCondition(VFCondition condition) {
            if (condition.transitions.Count() != 1) {
                throw new Exception("Cannot add 'or' conditions to an existing baked transition");
            }
            var extraConditions = condition.transitions.First();
            foreach (var transition in activeTransitions) {
                transition.conditions = transition.conditions.Concat(extraConditions).ToArray();
            }
        }

        private void TrimToSeed(VFTransition seed) {
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
