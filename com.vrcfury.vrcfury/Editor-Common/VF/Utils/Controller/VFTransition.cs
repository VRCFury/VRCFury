using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor.Animations;

namespace VF.Utils.Controller {
    internal class VFTransition {
        private readonly List<AnimatorStateTransition> createdTransitions = new List<AnimatorStateTransition>();
        private Func<AnimatorStateTransition> transitionProvider;
        public VFTransition(Func<AnimatorStateTransition> transitionProvider) {
            this.transitionProvider = () => {
                var trans = transitionProvider();
                trans.duration = 0;
                trans.canTransitionToSelf = false;
                trans.hasExitTime = false;
                createdTransitions.Add(trans);
                return trans;
            };
        }
        public VFTransition When() {
            var transition = transitionProvider();
            return this;
        }
        public VFTransition When(VFCondition cond) {
            foreach (var t in cond.transitions) {
                var transition = transitionProvider();
                transition.conditions = t.ToArray();
            }
            return this;
        }
        public VFTransition WithTransitionToSelf() {
            foreach (var t in createdTransitions) {
                t.canTransitionToSelf = true;
            }
            var oldProvider = transitionProvider;
            transitionProvider = () => {
                var trans = oldProvider();
                trans.canTransitionToSelf = true;
                return trans;
            };
            return this;
        }
        public VFTransition Interruptable() {
            foreach (var t in createdTransitions) {
                t.interruptionSource = TransitionInterruptionSource.Destination;
            }
            var oldProvider = transitionProvider;
            transitionProvider = () => {
                var t = oldProvider();
                t.interruptionSource = TransitionInterruptionSource.Destination;
                return t;
            };
            return this;
        }
        public VFTransition WithTransitionDurationSeconds(float time) {
            if (time < 0f) return this; // don't even bother
            foreach (var t in createdTransitions) {
                t.duration = time;
            }
            var oldProvider = transitionProvider;
            transitionProvider = () => {
                var trans = oldProvider();
                trans.duration = time;
                return trans;
            };
            return this;
        }
        public VFTransition WithTransitionExitTime(float time) {
            if (time < 0f) return this; // don't even bother
            foreach (var t in createdTransitions) {
                t.hasExitTime = true;
                t.exitTime = time;
            }
            var oldProvider = transitionProvider;
            transitionProvider = () => {
                var trans = oldProvider();
                trans.hasExitTime = true;
                trans.exitTime = time;
                return trans;
            };
            return this;
        }

        public void AddCondition(VFCondition c) {
            if (c.transitions.Count() != 1) {
                throw new Exception("Cannot add 'or' conditions to an existing baked transition");
            }
            foreach (var t in createdTransitions) {
                t.conditions = t.conditions.Concat(c.transitions.First()).ToArray();
            }
        }
    }
}
