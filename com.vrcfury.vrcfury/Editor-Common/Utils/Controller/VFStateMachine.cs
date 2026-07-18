using System.Collections.Generic;
using System.Linq;
using UnityEditor.Animations;
using UnityEngine;
using VF.Builder;
using VF.Utils;

namespace VF.Utils.Controller {
    internal class VFStateMachine {
        private readonly VFLayer layer;
        private AnimatorStateMachine sourceRaw;
        private readonly List<VFState> statesValue = new List<VFState>();
        private readonly List<VFStateMachineChild> childStateMachines = new List<VFStateMachineChild>();
        private readonly List<VFEntryTransition> entryTransitionsValue = new List<VFEntryTransition>();
        private readonly List<VFTransition> anyStateTransitionsValue = new List<VFTransition>();
        public VFBehaviourContainer behaviours { get; private set; } = new VFBehaviourContainer();

        private VFStateMachine(VFLayer layer, AnimatorStateMachine sourceRaw) {
            this.layer = layer;
            this.sourceRaw = sourceRaw;
        }

        internal static VFStateMachine Create(VFLayer layer, string name) {
            return new VFStateMachine(layer, null) {
                thisName = name
            };
        }

        public static VFStateMachine Load(
            VFLayer layer,
            AnimatorStateMachine raw,
            VFLoadContext context
        ) {
            if (raw == null) return null;
            var output = LoadRecursive(layer, raw, context);
            LinkRecursive(output, raw, context, new HashSet<VFStateMachine>());
            return output;
        }

        private static VFStateMachine LoadRecursive(
            VFLayer layer,
            AnimatorStateMachine raw,
            VFLoadContext context
        ) {
            var sourceRaw = raw;
            if (context.StateMachines.TryGetValue(sourceRaw, out var existing)) {
                return existing;
            }

            var output = new VFStateMachine(layer, sourceRaw) {
                thisName = raw.name,
                behaviours = VFBehaviourContainer.Load(raw, context),
                entryPosition = raw.entryPosition,
                anyStatePosition = raw.anyStatePosition,
                exitPosition = raw.exitPosition,
                parentStateMachinePosition = raw.parentStateMachinePosition
            };
            context.StateMachines[sourceRaw] = output;

            foreach (var child in raw.stateMachines) {
                var childSource = child.stateMachine;
                var childStateMachine = LoadRecursive(
                    layer,
                    child.stateMachine,
                    context
                );
                output.childStateMachines.Add(new VFStateMachineChild {
                    stateMachine = childStateMachine,
                    position = child.position
                });
                context.StateMachines[childSource] = childStateMachine;
            }

            foreach (var child in raw.states) {
                var stateSource = child.state;
                var state = VFState.Load(
                    layer,
                    output,
                    child.state,
                    child.position,
                    context
                );
                output.statesValue.Add(state);
                context.States[stateSource] = state;
            }

            return output;
        }

        private static void LinkRecursive(
            VFStateMachine output,
            AnimatorStateMachine raw,
            VFLoadContext context,
            ISet<VFStateMachine> linked
        ) {
            if (output == null || raw == null || !linked.Add(output)) {
                return;
            }

            output.defaultState = raw.defaultState != null
                ? context.States.GetOrDefault(raw.defaultState)
                : null;

            output.entryTransitionsValue.AddRange(
                raw.entryTransitions
                    .Select(t => VFEntryTransition.Load(t, context.States, context.StateMachines))
                    .Where(t => t != null)
            );
            output.anyStateTransitionsValue.AddRange(
                raw.anyStateTransitions
                    .Select(t => VFTransition.Load(t, context.States, context.StateMachines))
                    .Where(t => t != null)
            );

            for (var i = 0; i < raw.stateMachines.Length; i++) {
                var rawChild = raw.stateMachines[i];
                var childStateMachine = output.childStateMachines[i];
                childStateMachine.transitions.AddRange(
                    raw.GetStateMachineTransitions(rawChild.stateMachine)
                        .Select(t => VFEntryTransition.Load(t, context.States, context.StateMachines))
                        .Where(t => t != null)
                );
                LinkRecursive(childStateMachine.stateMachine, rawChild.stateMachine, context, linked);
            }

            for (var i = 0; i < raw.states.Length; i++) {
                output.statesValue[i].transitions.AddRange(
                    raw.states[i].state.transitions
                        .Select(t => VFTransition.Load(t, context.States, context.StateMachines))
                        .Where(t => t != null)
                );
            }
        }

        internal VFStateMachine Clone(VFLayer newLayer, VFMotionCloneContext cloneContext, out Dictionary<VFState, VFState> stateMap) {
            stateMap = new Dictionary<VFState, VFState>();
            var stateMachineMap = new Dictionary<VFStateMachine, VFStateMachine>();
            return CloneRecursive(newLayer, stateMap, stateMachineMap, cloneContext);
        }

        private VFStateMachine CloneRecursive(
            VFLayer newLayer,
            Dictionary<VFState, VFState> stateMap,
            Dictionary<VFStateMachine, VFStateMachine> stateMachineMap,
            VFMotionCloneContext cloneContext
        ) {
            var clone = new VFStateMachine(newLayer, null) {
                thisName = thisName,
                behaviours = behaviours.Clone(),
                entryPosition = entryPosition,
                anyStatePosition = anyStatePosition,
                exitPosition = exitPosition,
                parentStateMachinePosition = parentStateMachinePosition
            };
            stateMachineMap[this] = clone;

            foreach (var state in statesValue) {
                clone.statesValue.Add(state.Clone(newLayer, clone, stateMap, cloneContext));
            }
            foreach (var child in childStateMachines) {
                clone.childStateMachines.Add(new VFStateMachineChild {
                    stateMachine = child.stateMachine.CloneRecursive(newLayer, stateMap, stateMachineMap, cloneContext),
                    position = child.position
                });
            }

            clone.defaultState = defaultState != null ? stateMap.GetOrDefault(defaultState) : null;
            clone.entryTransitionsValue.AddRange(entryTransitionsValue.Select(t => (VFEntryTransition)t.Clone(stateMap, stateMachineMap)));
            clone.anyStateTransitionsValue.AddRange(anyStateTransitionsValue.Select(t => (VFTransition)t.Clone(stateMap, stateMachineMap)));

            for (var i = 0; i < statesValue.Count; i++) {
                clone.statesValue[i].transitions.AddRange(
                    statesValue[i].transitions.Select(t => (VFTransition)t.Clone(stateMap, stateMachineMap))
                );
            }
            for (var i = 0; i < childStateMachines.Count; i++) {
                clone.childStateMachines[i].transitions.AddRange(
                    childStateMachines[i].transitions.Select(t => (VFEntryTransition)t.Clone(stateMap, stateMachineMap))
                );
            }

            return clone;
        }

        internal AnimatorStateMachine Save(VFSaveContext saveContext) {
            return Save(
                new Dictionary<VFStateMachine, AnimatorStateMachine>(),
                new Dictionary<VFState, AnimatorState>(),
                saveContext
            );
        }

        private AnimatorStateMachine Save(
            Dictionary<VFStateMachine, AnimatorStateMachine> stateMachineMap,
            Dictionary<VFState, AnimatorState> stateMap,
            VFSaveContext saveContext
        ) {
            var raw = VrcfObjectFactory.Create<AnimatorStateMachine>();
            stateMachineMap[this] = raw;

            raw.name = thisName;
            raw.behaviours = behaviours.Select(behaviour => behaviour.Save(saveContext)).ToArray();
            raw.entryPosition = entryPosition;
            raw.anyStatePosition = anyStatePosition;
            raw.exitPosition = exitPosition;
            raw.parentStateMachinePosition = parentStateMachinePosition;

            foreach (var state in statesValue) {
                state.Save(stateMap, saveContext);
                var child = state.ToChildAnimatorState(stateMap);
                raw.AddState(child.state, child.position);
            }

            foreach (var child in childStateMachines) {
                var childRaw = child.stateMachine.Save(stateMachineMap, stateMap, saveContext);
                raw.AddStateMachine(childRaw, child.position);
            }

            raw.defaultState = defaultState != null ? stateMap.GetOrDefault(defaultState) : null;
            raw.entryTransitions = entryTransitionsValue
                .Select(t => (AnimatorTransition)t.Save(stateMap, stateMachineMap))
                .Where(t => t != null)
                .ToArray();
            raw.anyStateTransitions = anyStateTransitionsValue
                .Select(t => (AnimatorStateTransition)t.Save(stateMap, stateMachineMap))
                .Where(t => t != null)
                .ToArray();

            foreach (var child in childStateMachines) {
                raw.SetStateMachineTransitions(
                    stateMachineMap[child.stateMachine],
                    child.transitions
                        .Select(t => (AnimatorTransition)t.Save(stateMap, stateMachineMap))
                        .Where(t => t != null)
                        .ToArray()
                );
            }
            foreach (var state in statesValue) {
                stateMap[state].transitions = state.transitions
                    .Select(t => (AnimatorStateTransition)t.Save(stateMap, stateMachineMap))
                    .Where(t => t != null)
                    .ToArray();
            }

            return raw;
        }

        internal VFEntryTransition CreateEntryTransition(VFState destination) {
            var transition = new VFEntryTransition {
                destinationState = destination
            };
            entryTransitionsValue.Add(transition);
            return transition;
        }

        internal VFTransition CreateAnyStateTransition(VFState destination) {
            var transition = new VFTransition {
                destinationState = destination,
                hasFixedDuration = true
            };
            anyStateTransitionsValue.Add(transition);
            return transition;
        }

        internal void AddState(VFState state) {
            statesValue.Add(state);
            state.ReassignStateMachine(this);
            if (defaultState == null) {
                defaultState = state;
            }
        }

        internal IEnumerable<VFStateMachine> GetAllStateMachines() {
            yield return this;
            foreach (var child in childStateMachines) {
                foreach (var sub in child.stateMachine.GetAllStateMachines()) {
                    yield return sub;
                }
            }
        }

        internal IEnumerable<VFTransitionBase> GetAllTransitions() {
            foreach (var t in entryTransitionsValue) yield return t;
            foreach (var t in anyStateTransitionsValue) yield return t;
            foreach (var child in childStateMachines) {
                foreach (var t in child.transitions) yield return t;
            }
            foreach (var state in statesValue) {
                foreach (var t in state.transitions) yield return t;
            }
            foreach (var child in childStateMachines) {
                foreach (var t in child.stateMachine.GetAllTransitions()) yield return t;
            }
        }

        internal void RewriteTransitionLists(System.Func<VFTransitionBase, OneOrMany<VFTransitionBase>> action) {
            var rewrittenEntryTransitions = entryTransitionsValue
                .SelectMany(t => action(t).Get())
                .OfType<VFEntryTransition>()
                .Where(t => t != null)
                .ToList();
            entryTransitionsValue.Clear();
            entryTransitionsValue.AddRange(rewrittenEntryTransitions);

            var rewrittenAnyStateTransitions = anyStateTransitionsValue
                .SelectMany(t => action(t).Get())
                .OfType<VFTransition>()
                .Where(t => t != null)
                .ToList();
            anyStateTransitionsValue.Clear();
            anyStateTransitionsValue.AddRange(rewrittenAnyStateTransitions);

            foreach (var child in childStateMachines) {
                var rewrittenChildTransitions = child.transitions
                    .SelectMany(t => action(t).Get())
                    .OfType<VFEntryTransition>()
                    .Where(t => t != null)
                    .ToList();
                child.transitions.Clear();
                child.transitions.AddRange(rewrittenChildTransitions);
                child.stateMachine.RewriteTransitionLists(action);
            }
            foreach (var state in statesValue) {
                var rewritten = state.transitions
                    .SelectMany(t => action(t).Get())
                    .OfType<VFTransition>()
                    .Where(t => t != null)
                    .ToList();
                state.transitions.Clear();
                state.transitions.AddRange(rewritten);
            }
        }
        internal AnimatorStateMachine GetSourceAsset() => sourceRaw;

        private string thisName;
        public string name {
            get => thisName;
            set => thisName = value;
        }

        public Vector3 entryPosition { get; set; }
        public Vector3 anyStatePosition { get; set; }
        public Vector3 exitPosition { get; set; }
        public Vector3 parentStateMachinePosition { get; set; }
        public VFState defaultState { get; set; }
        public IList<VFEntryTransition> entryTransitions => entryTransitionsValue;
        public IList<VFTransition> anyStateTransitions => anyStateTransitionsValue;
        public IReadOnlyList<VFState> states => statesValue;
        public string prettyName => $"{layer.prettyName} StateMachine {thisName}";
    }
}
