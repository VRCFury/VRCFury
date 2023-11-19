using System;
using UnityEditor.Animations;
using UnityEngine;
using VF.Builder;
using VF.Builder.Exceptions;
using VRC.SDK3.Avatars.Components;
using VRC.SDKBase;

namespace VF.Utils.Controller {
    public class VFState {
        private ChildAnimatorState node;
        private readonly AnimatorStateMachine stateMachine;

        private static readonly float X_OFFSET = 250;
        private static readonly float Y_OFFSET = 80;

        public VFState(ChildAnimatorState node, AnimatorStateMachine stateMachine) {
            this.node = node;
            this.stateMachine = stateMachine;
        }

        public static Vector3 MovePos(Vector3 orig, float x, float y) {
            var pos = orig;
            pos.x += x * X_OFFSET;
            pos.y += y * Y_OFFSET;
            return pos;
        }
        public VFState Move(Vector3 orig, float x, float y) {
            node.position = MovePos(orig, x, y);
            var states = stateMachine.states;
            var index = Array.FindIndex(states, n => n.state == node.state);
            if (index < 0) return this;
            states[index] = node;
            stateMachine.states = states;
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
            node.state.motion = motion;
            return this;
        }
        public VFState MotionTime(VFAFloat param) {
            node.state.timeParameterActive = true;
            node.state.timeParameter = param.Name();
            return this;
        }
        public VFState Speed(float speed) {
            node.state.speed = speed;
            return this;
        }

        public VRCAvatarParameterDriver GetDriver(bool local = false) {
            foreach (var b in node.state.behaviours) {
                var d = b as VRCAvatarParameterDriver;
                if (d && d.localOnly == local) return d;
            }
            var driver = node.state.VAddStateMachineBehaviour<VRCAvatarParameterDriver>();
            driver.localOnly = local;
            return driver;
        }
        private VRC_AvatarParameterDriver.Parameter Drives(string param, bool local = false) {
            var driver = GetDriver(local);
            var p = new VRC_AvatarParameterDriver.Parameter
            {
                name = param,
                type = VRC_AvatarParameterDriver.ChangeType.Set
            };
            driver.parameters.Add(p);
            return p;
        }
        public VFState Drives(VFABool param, bool value, bool local = false) {
            Drives(param.Name(), local).value = value ? 1 : 0;
            return this;
        }
        public VFState Drives(VFAParam param, float value, bool local = false) {
            Drives(param.Name(), local).value = value;
            return this;
        }
        public VFState DrivesRandom(VFAInteger param, float min, float max) {
            var p = Drives(param.Name(), true);
            p.type = VRC_AvatarParameterDriver.ChangeType.Random;
            p.valueMin = min;
            p.valueMax = max;
            return this;
        }
        public VFState DrivesDelta(VFAInteger param, float delta) {
            var p = Drives(param.Name(), true);
            p.type = VRC_AvatarParameterDriver.ChangeType.Add;
            p.value = delta;
            return this;
        }
        public VFState DrivesCopy(VFAInteger param, VFAInteger source) {
            var driver = GetDriver(true);
            var p = new VRC_AvatarParameterDriver.Parameter
            {
                name = param.Name()
            };
            var sourceField = p.GetType().GetField("source");
            if (sourceField == null) throw new VRCFBuilderException("VRCFury feature failed to build because VRCSDK is outdated");
            sourceField.SetValue(p, source.Name());
            // We cast rather than use Copy directly so it doesn't fail to compile on old VRCSDK
            p.type = (VRC_AvatarParameterDriver.ChangeType)3; //VRC_AvatarParameterDriver.ChangeType.Copy;
            driver.parameters.Add(p);
            return this;
        }

        public VFEntryTransition TransitionsFromEntry() {
            return new VFEntryTransition(() => stateMachine.AddEntryTransition(node.state));
        }
        public VFTransition TransitionsFromAny() {
            return new VFTransition(() => stateMachine.AddAnyStateTransition(node.state));
        }
        public VFTransition TransitionsTo(VFState other) {
            return new VFTransition(() => node.state.AddTransition(other.node.state));
        }
        public VFTransition TransitionsTo(AnimatorState other) {
            return new VFTransition(() => node.state.AddTransition(other));
        }
        public VFTransition TransitionsToExit() {
            return new VFTransition(() => node.state.AddExitTransition());
        }

        public AnimatorState GetRaw() {
            return node.state;
        }

        public static void FakeAnyState(params (VFState,VFCondition)[] states) {
            if (states.Length <= 1) return;
        
            VFCondition above = null;
            foreach (var (state, when) in states) {
                VFCondition myWhen;
                if (state == states[states.Length - 1].Item1) {
                    myWhen = above?.Not();
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
    }
}