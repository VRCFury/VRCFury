using System;
using System.Linq;
using UnityEditor.Animations;
using UnityEngine;
using Object = UnityEngine.Object;

namespace VF.Utils.Controller {
    internal class VFState : VFBehaviourContainer {
        private VFLayer layer;
        private ChildAnimatorState node;
        private readonly AnimatorState state;
        private readonly AnimatorStateMachine stateMachine;

        private static readonly float X_OFFSET = 250;
        private static readonly float Y_OFFSET = 80;

        public VFState(VFLayer layer, ChildAnimatorState node, AnimatorStateMachine stateMachine) {
            this.layer = layer;
            this.node = node;
            this.state = node.state;
            this.stateMachine = stateMachine;
        }

        public static Vector3 CalculateOffsetPosition(Vector3 basis, float x, float y) {
            var pos = basis;
            pos.x += x * X_OFFSET;
            pos.y += y * Y_OFFSET;
            return pos;
        }
        public VFState Move(Vector3 orig, float x, float y) {
            SetRawPosition(CalculateOffsetPosition(orig, x, y));
            return this;
        }
        public VFState SetRawPosition(Vector2 v) {
            var pos = node.position;
            pos.x = v.x;
            pos.y = v.y;
            node.position = pos;
            var states = stateMachine.states;
            var index = Array.FindIndex(states, n => n.state == state);
            if (index >= 0) {
                states[index] = node;
                stateMachine.states = states;
            }
            return this;
        }
        public VFState Move(VFState other, float x, float y) {
            Move(other.node.position, x, y);
            return this;
        }
        public VFState Move(float x, float y) {
            Move(this, x, y);
            return this;
        }

        public VFState WithAnimation(Motion motion) {
            state.motion = motion;
            return this;
        }
        public VFState MotionTime(VFAFloat param) {
            state.timeParameterActive = true;
            state.timeParameter = param;
            return this;
        }
        public VFState Speed(float speed) {
            state.speed = speed;
            return this;
        }

        public VFEntryTransition TransitionsFromEntry() {
            return new VFEntryTransition(() => {
                var transition = VrcfObjectFactory.Create<AnimatorTransition>();
                transition.destinationState = state;
                stateMachine.entryTransitions = stateMachine.entryTransitions
                    .Concat(new[] { transition })
                    .ToArray();
                return transition;
            });
        }
        public VFTransition TransitionsFromAny() {
            return new VFTransition(() => {
                var transition = VrcfObjectFactory.Create<AnimatorStateTransition>();
                transition.hasFixedDuration = true;
                transition.destinationState = state;
                stateMachine.anyStateTransitions = stateMachine.anyStateTransitions
                    .Concat(new[] { transition })
                    .ToArray();
                return transition;
            });
        }
        public VFTransition TransitionsTo(VFState other) {
            return TransitionsTo(other.state);
        }
        public VFTransition TransitionsTo(AnimatorState other) {
            return new VFTransition(() => {
                var transition = VrcfObjectFactory.Create<AnimatorStateTransition>();
                transition.hasFixedDuration = true;
                transition.destinationState = other;
                state.transitions = state.transitions
                    .Concat(new[] { transition })
                    .ToArray();
                return transition;
            });
        }
        public VFTransition TransitionsToExit() {
            return new VFTransition(() => {
                var transition = VrcfObjectFactory.Create<AnimatorStateTransition>();
                transition.hasFixedDuration = true;
                transition.isExit = true;
                state.transitions = state.transitions
                    .Concat(new[] { transition })
                    .ToArray();
                return transition;
            });
        }

        //public AnimatorState GetRaw() {
        //    return state;
        //}

        public static void FakeAnyState(params (VFState,VFCondition)[] states) {
            if (states.Length <= 1) return;
        
            VFCondition above = null;
            foreach (var (state, when) in states) {
                VFCondition myWhen;
                if (state == states[states.Length - 1].Item1) {
                    myWhen = above.Not();
                } else if (above == null) {
                    above = myWhen = when;
                } else {
                    myWhen = when.And(above.Not());
                    above = above.Or(when);
                }
                foreach (var (other,_) in states) {
                    if (other == state) continue;
                    other.TransitionsTo(state).When(myWhen);
                }
            }
        }

        public void SetAsDefaultState() {
            stateMachine.defaultState = state;
        }

        public StateMachineBehaviour[] behaviours {
            get => state.behaviours;
            set => state.behaviours = value;
        }

        public string prettyName => $"{layer.prettyName} State {state.name}";
        public Object behaviourContainer => state;
    }
}
