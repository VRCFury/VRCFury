using System;
using System.Linq;
using UnityEditor.Animations;
using UnityEngine;
using VRC.SDK3.Avatars.Components;
using VRC.SDKBase;

namespace VF.Utils.Controller {
    internal class VFState : VFBehaviourContainer {
        private ChildAnimatorState node;
        private readonly AnimatorState state;
        private readonly AnimatorStateMachine stateMachine;

        private static readonly float X_OFFSET = 250;
        private static readonly float Y_OFFSET = 80;

        public VFState(ChildAnimatorState node, AnimatorStateMachine stateMachine) {
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

        private VRCAvatarParameterDriver GetDriver() {
            var exists = state.behaviours
                .OfType<VRCAvatarParameterDriver>()
                .FirstOrDefault(b => !b.localOnly);
            if (exists != null) {
                return exists;
            }
            var driver = this.AddBehaviour<VRCAvatarParameterDriver>();
            driver.localOnly = false;
            return driver;
        }
        private VRC_AvatarParameterDriver.Parameter Drives(string param) {
            var driver = GetDriver();
            var p = new VRC_AvatarParameterDriver.Parameter();
            p.name = param;
            p.type = VRC_AvatarParameterDriver.ChangeType.Set;
            driver.parameters.Add(p);
            return p;
        }
        public VFState Drives(VFABool param, bool value) {
            Drives(param).value = value ? 1 : 0;
            return this;
        }
        public VFState Drives(VFAParam param, float value) {
            Drives(param).value = value;
            return this;
        }
        public VFState Drives(string param, float value) {
            Drives(param).value = value;
            return this;
        }
        public VFState DrivesRandom(VFAInteger param, float min, float max) {
            var p = Drives(param);
            p.type = VRC_AvatarParameterDriver.ChangeType.Random;
            p.valueMin = min;
            p.valueMax = max;
            return this;
        }
        public VFState DrivesDelta(VFAInteger param, float delta) {
            var p = Drives(param);
            p.type = VRC_AvatarParameterDriver.ChangeType.Add;
            p.value = delta;
            return this;
        }
        public VFState DrivesCopy(string from, string to, float fromMin = 0, float fromMax = 0, float toMin = 0, float toMax = 0) {
#if ! VRCSDK_HAS_DRIVER_COPY
            throw new Exception("VRCFury feature failed to build because VRCSDK is outdated");
#else
            var driver = GetDriver();
            var p = new VRC_AvatarParameterDriver.Parameter {
                name = to,
                source = from
            };

            if (fromMin != 0 || fromMax != 0) {
                p.sourceMin = fromMin;
                p.sourceMax = fromMax;
                p.destMin = toMin;
                p.destMax = toMax;
                p.convertRange = true;
            }

            p.type = VRC_AvatarParameterDriver.ChangeType.Copy;
            driver.parameters.Add(p);
            return this;
#endif
        }

        public VFEntryTransition TransitionsFromEntry() {
            return new VFEntryTransition(() => VrcfObjectFactory.Register(stateMachine.AddEntryTransition(state)));
        }
        public VFTransition TransitionsFromAny() {
            return new VFTransition(() => VrcfObjectFactory.Register(stateMachine.AddAnyStateTransition(state)));
        }
        public VFTransition TransitionsTo(VFState other) {
            return new VFTransition(() => VrcfObjectFactory.Register(state.AddTransition(other.state)));
        }
        public VFTransition TransitionsTo(AnimatorState other) {
            return new VFTransition(() => VrcfObjectFactory.Register(state.AddTransition(other)));
        }
        public VFTransition TransitionsToExit() {
            return new VFTransition(() => VrcfObjectFactory.Register(state.AddExitTransition()));
        }

        public AnimatorState GetRaw() {
            return state;
        }

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

        public StateMachineBehaviour[] behaviours {
            get => state.behaviours;
            set => state.behaviours = value;
        }
    }
}
