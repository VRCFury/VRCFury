using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor.Animations;
using VF.Utils;

namespace VF.Utils.Controller {
    internal abstract class VFTransitionBase {
        public AnimatorCondition[] conditions { get; set; } = Array.Empty<AnimatorCondition>();
        public VFState destinationState { get; set; }
        public VFStateMachine destinationStateMachine { get; set; }
        public bool isExit { get; set; }

        protected static AnimatorCondition[] CloneConditions(IEnumerable<AnimatorCondition> source) {
            return (source ?? Enumerable.Empty<AnimatorCondition>()).ToArray();
        }

        protected void CopyBaseTo(
            VFTransitionBase target,
            IReadOnlyDictionary<VFState, VFState> stateMap,
            IReadOnlyDictionary<VFStateMachine, VFStateMachine> stateMachineMap
        ) {
            target.conditions = CloneConditions(conditions);
            target.isExit = isExit;
            target.destinationState = destinationState != null ? stateMap.GetOrDefault(destinationState) : null;
            target.destinationStateMachine = destinationStateMachine != null ? stateMachineMap.GetOrDefault(destinationStateMachine) : null;
        }

        protected bool HasDestination(
            IReadOnlyDictionary<VFState, AnimatorState> stateMap,
            IReadOnlyDictionary<VFStateMachine, AnimatorStateMachine> stateMachineMap
        ) {
            if (isExit) return true;
            if (destinationState != null && stateMap.GetOrDefault(destinationState) != null) return true;
            if (destinationStateMachine != null && stateMachineMap.GetOrDefault(destinationStateMachine) != null) return true;
            return false;
        }

        public abstract AnimatorTransitionBase Save(
            IReadOnlyDictionary<VFState, AnimatorState> stateMap,
            IReadOnlyDictionary<VFStateMachine, AnimatorStateMachine> stateMachineMap,
            VFSaveContext saveContext
        );

        public abstract VFTransitionBase Clone(
            IReadOnlyDictionary<VFState, VFState> stateMap,
            IReadOnlyDictionary<VFStateMachine, VFStateMachine> stateMachineMap
        );
    }
}
