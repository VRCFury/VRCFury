using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor.Animations;
using UnityEngine;
using VF.Builder;
using VF.Utils;

namespace VF.Utils.Controller {
    internal class VFState {
        private readonly VFLayer layer;
        private AnimatorState sourceRaw;
        private VFStateMachine stateMachine;
        private VFMotion motionValue;
        private string nameValue;
        private Vector3 position;
        public List<VFTransition> transitions { get; } = new List<VFTransition>();
        public VFBehaviourContainer behaviours { get; private set; } = new VFBehaviourContainer();

        private static readonly float X_OFFSET = 250;
        private static readonly float Y_OFFSET = 80;

        private VFState(VFLayer layer, VFStateMachine stateMachine, AnimatorState sourceRaw) {
            this.layer = layer;
            this.stateMachine = stateMachine;
            this.sourceRaw = sourceRaw;
        }

        internal static VFState Create(VFLayer layer, VFStateMachine stateMachine, string name) {
            return new VFState(layer, stateMachine, null) {
                nameValue = name,
                writeDefaultValues = true,
                speed = 1
            };
        }

        internal static VFState Load(
            VFLayer layer,
            VFStateMachine stateMachine,
            AnimatorState raw,
            Vector3 rawPosition,
            VFLoadContext context
        ) {
            if (raw == null) return null;
            return new VFState(layer, stateMachine, raw) {
                nameValue = raw.name,
                motionValue = VFMotion.Load(raw.motion, context),
                writeDefaultValues = raw.writeDefaultValues,
                speed = raw.speed,
                cycleOffset = raw.cycleOffset,
                cycleOffsetParameterActive = raw.cycleOffsetParameterActive,
                cycleOffsetParameter = raw.cycleOffsetParameter,
                mirror = raw.mirror,
                mirrorParameterActive = raw.mirrorParameterActive,
                mirrorParameter = raw.mirrorParameter,
                speedParameterActive = raw.speedParameterActive,
                speedParameter = raw.speedParameter,
                timeParameterActive = raw.timeParameterActive,
                timeParameter = raw.timeParameter,
                tag = raw.tag,
                iKOnFeet = raw.iKOnFeet,
                behaviours = VFBehaviourContainer.Load(raw, context),
                position = rawPosition
            };
        }

        internal VFState Clone(
            VFLayer newLayer,
            VFStateMachine newStateMachine,
            Dictionary<VFState, VFState> stateMap,
            VFMotionCloneContext cloneContext
        ) {
            var clone = new VFState(newLayer, newStateMachine, null) {
                nameValue = nameValue,
                motionValue = motionValue?.Clone(cloneContext),
                writeDefaultValues = writeDefaultValues,
                speed = speed,
                cycleOffset = cycleOffset,
                cycleOffsetParameterActive = cycleOffsetParameterActive,
                cycleOffsetParameter = cycleOffsetParameter,
                mirror = mirror,
                mirrorParameterActive = mirrorParameterActive,
                mirrorParameter = mirrorParameter,
                speedParameterActive = speedParameterActive,
                speedParameter = speedParameter,
                timeParameterActive = timeParameterActive,
                timeParameter = timeParameter,
                tag = tag,
                iKOnFeet = iKOnFeet,
                behaviours = behaviours.Clone(),
                position = position
            };
            stateMap[this] = clone;
            return clone;
        }

        internal AnimatorState GetSourceAsset() {
            return sourceRaw;
        }

        internal void ReassignStateMachine(VFStateMachine newStateMachine) {
            stateMachine = newStateMachine;
        }

        internal AnimatorState Save(
            Dictionary<VFState, AnimatorState> stateMap,
            VFSaveContext saveContext
        ) {
            var raw = VrcfObjectFactory.Create<AnimatorState>();
            raw.name = nameValue;
            raw.motion = motionValue?.Save(saveContext);
            raw.writeDefaultValues = writeDefaultValues;
            raw.speed = speed;
            raw.cycleOffset = cycleOffset;
            raw.cycleOffsetParameterActive = cycleOffsetParameterActive;
            raw.cycleOffsetParameter = cycleOffsetParameter;
            raw.mirror = mirror;
            raw.mirrorParameterActive = mirrorParameterActive;
            raw.mirrorParameter = mirrorParameter;
            raw.speedParameterActive = speedParameterActive;
            raw.speedParameter = speedParameter;
            raw.timeParameterActive = timeParameterActive;
            raw.timeParameter = timeParameter;
            raw.tag = tag;
            raw.iKOnFeet = iKOnFeet;
            raw.behaviours = behaviours.Select(behaviour => behaviour.Save(saveContext)).ToArray();
            saveContext.AddNewAsset(raw);
            stateMap[this] = raw;
            return raw;
        }

        internal ChildAnimatorState ToChildAnimatorState(Dictionary<VFState, AnimatorState> stateMap) {
            return new ChildAnimatorState {
                state = stateMap[this],
                position = position
            };
        }

        public static Vector3 CalculateOffsetPosition(Vector3 basis, float x, float y) {
            var pos = basis;
            pos.x += x * X_OFFSET;
            pos.y += y * Y_OFFSET;
            return pos;
        }

        public VFState Move(Vector3 orig, float x, float y) {
            position = CalculateOffsetPosition(orig, x, y);
            return this;
        }

        public VFState SetRawPosition(Vector2 v) {
            position = new Vector3(v.x, v.y, 0);
            return this;
        }

        public VFState Move(VFState other, float x, float y) {
            return Move(other.position, x, y);
        }

        public VFState Move(float x, float y) {
            return Move(this, x, y);
        }

        public VFState WithAnimation(VFMotion motion) {
            motionValue = motion;
            return this;
        }

        public VFState MotionTime(VFAFloat param) {
            timeParameterActive = true;
            timeParameter = param;
            return this;
        }

        public VFState Speed(float speed) {
            this.speed = speed;
            return this;
        }

        public VFEntryTransitionBuilder TransitionsFromEntry() {
            return new VFEntryTransitionBuilder(stateMachine.entryTransitions, stateMachine.CreateEntryTransition(this));
        }

        public VFTransitionBuilder TransitionsFromAny() {
            return new VFTransitionBuilder(stateMachine.anyStateTransitions, stateMachine.CreateAnyStateTransition(this));
        }

        public VFTransitionBuilder TransitionsTo(VFState other) {
            return new VFTransitionBuilder(transitions, CreateStateTransition(other, null, false));
        }

        public VFTransitionBuilder TransitionsTo(AnimatorState other) {
            var destination = layer.FindStateBySource(other)
                ?? throw new Exception($"Could not find state for raw animator state `{other?.name}`");
            return TransitionsTo(destination);
        }

        public VFTransitionBuilder TransitionsToExit() {
            return new VFTransitionBuilder(transitions, CreateStateTransition(null, null, true));
        }

        private VFTransition CreateStateTransition(VFState destinationState, VFStateMachine destinationStateMachine, bool isExit) {
            var transition = new VFTransition {
                destinationState = destinationState,
                destinationStateMachine = destinationStateMachine,
                isExit = isExit,
                hasFixedDuration = true
            };
            transitions.Add(transition);
            return transition;
        }

        public static void FakeAnyState(params (VFState, VFCondition)[] states) {
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

                foreach (var (other, _) in states) {
                    if (other == state) continue;
                    other.TransitionsTo(state).When(myWhen);
                }
            }
        }

        public void SetAsDefaultState() {
            stateMachine.defaultState = this;
        }

        public string name {
            get => nameValue;
            set => nameValue = value;
        }

        internal VFMotion motion {
            get => motionValue;
            set => motionValue = value;
        }

        public bool writeDefaultValues { get; set; }
        public float speed { get; set; }
        public float cycleOffset { get; set; }
        public bool cycleOffsetParameterActive { get; set; }
        public string cycleOffsetParameter { get; set; }
        public bool mirror { get; set; }
        public bool mirrorParameterActive { get; set; }
        public string mirrorParameter { get; set; }
        public bool speedParameterActive { get; set; }
        public string speedParameter { get; set; }
        public bool timeParameterActive { get; set; }
        public string timeParameter { get; set; }
        public string tag { get; set; }
        public bool iKOnFeet { get; set; }
        public string prettyName => $"{layer.prettyName} State {nameValue}";
    }
}
