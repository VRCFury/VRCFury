using System;
using System.Collections.Generic;
using UnityEditor.Animations;
using VF.Builder;
using VF.Utils;

namespace VF.Utils.Controller {
    internal class VFEntryTransition : VFTransitionBase {
        private AnimatorTransition sourceRaw;

        internal VFEntryTransition() {
        }

        public static VFEntryTransition Load(
            AnimatorTransition raw,
            IReadOnlyDictionary<AnimatorState, VFState> stateMap,
            IReadOnlyDictionary<AnimatorStateMachine, VFStateMachine> stateMachineMap
        ) {
            if (raw == null) return null;
            return new VFEntryTransition {
                sourceRaw = raw,
                conditions = CloneConditions(raw.conditions),
                destinationState = raw.destinationState != null ? stateMap.GetOrDefault(raw.destinationState) : null,
                destinationStateMachine = raw.destinationStateMachine != null
                    ? stateMachineMap.GetOrDefault(raw.destinationStateMachine)
                    : null,
                isExit = raw.isExit
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
                : VrcfObjectFactory.Create<AnimatorTransition>();
            raw.conditions = CloneConditions(conditions);
            raw.destinationState = destinationState != null ? stateMap.GetOrDefault(destinationState) : null;
            raw.destinationStateMachine = destinationStateMachine != null
                ? stateMachineMap.GetOrDefault(destinationStateMachine)
                : null;
            raw.isExit = isExit;
            context.AddNewAsset(raw);
            return raw;
        }

        public override VFTransitionBase Clone(
            IReadOnlyDictionary<VFState, VFState> stateMap,
            IReadOnlyDictionary<VFStateMachine, VFStateMachine> stateMachineMap
        ) {
            var clone = new VFEntryTransition {
                sourceRaw = sourceRaw
            };
            CopyBaseTo(clone, stateMap, stateMachineMap);
            return clone;
        }
    }
}
