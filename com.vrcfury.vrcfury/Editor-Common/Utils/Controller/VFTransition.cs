using System;
using System.Collections.Generic;
using UnityEditor.Animations;
using VF.Builder;
using VF.Utils;

namespace VF.Utils.Controller {
    internal class VFTransition : VFTransitionBase {
        private AnimatorStateTransition sourceRaw;
        public bool canTransitionToSelf { get; set; }
        public bool hasExitTime { get; set; }
        public bool hasFixedDuration { get; set; } = true;
        public float exitTime { get; set; }
        public float duration { get; set; }
        public TransitionInterruptionSource interruptionSource { get; set; }

        internal VFTransition() {
        }

        public static VFTransition Load(
            AnimatorStateTransition raw,
            IReadOnlyDictionary<AnimatorState, VFState> stateMap,
            IReadOnlyDictionary<AnimatorStateMachine, VFStateMachine> stateMachineMap
        ) {
            if (raw == null) return null;
            return new VFTransition {
                sourceRaw = raw,
                conditions = CloneConditions(raw.conditions),
                destinationState = raw.destinationState != null ? stateMap.GetOrDefault(raw.destinationState) : null,
                destinationStateMachine = raw.destinationStateMachine != null
                    ? stateMachineMap.GetOrDefault(raw.destinationStateMachine)
                    : null,
                isExit = raw.isExit,
                canTransitionToSelf = raw.canTransitionToSelf,
                hasExitTime = raw.hasExitTime,
                hasFixedDuration = raw.hasFixedDuration,
                exitTime = raw.exitTime,
                duration = raw.duration,
                interruptionSource = raw.interruptionSource
            };
        }

        public override AnimatorTransitionBase Save(
            IReadOnlyDictionary<VFState, AnimatorState> stateMap,
            IReadOnlyDictionary<VFStateMachine, AnimatorStateMachine> stateMachineMap,
            VFSaveContext context
        ) {
            if (!HasDestination(stateMap, stateMachineMap)) {
                return null;
            }
            var raw = sourceRaw != null
                ? sourceRaw.Clone()
                : VrcfObjectFactory.Create<AnimatorStateTransition>();
            raw.conditions = CloneConditions(conditions);
            raw.destinationState = destinationState != null ? stateMap.GetOrDefault(destinationState) : null;
            raw.destinationStateMachine = destinationStateMachine != null
                ? stateMachineMap.GetOrDefault(destinationStateMachine)
                : null;
            raw.isExit = isExit;
            raw.canTransitionToSelf = canTransitionToSelf;
            raw.hasExitTime = hasExitTime;
            raw.hasFixedDuration = hasFixedDuration;
            raw.exitTime = exitTime;
            raw.duration = duration;
            raw.interruptionSource = interruptionSource;
            context.AddNewAsset(raw);
            return raw;
        }

        public override VFTransitionBase Clone(
            IReadOnlyDictionary<VFState, VFState> stateMap,
            IReadOnlyDictionary<VFStateMachine, VFStateMachine> stateMachineMap
        ) {
            var clone = new VFTransition {
                sourceRaw = sourceRaw,
                canTransitionToSelf = canTransitionToSelf,
                hasExitTime = hasExitTime,
                hasFixedDuration = hasFixedDuration,
                exitTime = exitTime,
                duration = duration,
                interruptionSource = interruptionSource
            };
            CopyBaseTo(clone, stateMap, stateMachineMap);
            return clone;
        }
    }
}
