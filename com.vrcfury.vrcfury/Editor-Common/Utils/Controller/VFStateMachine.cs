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
            LoadTransitionsRecursive(output, raw, context);
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
                var childStateMachine = LoadRecursive(
                    layer,
                    child.stateMachine,
                    context
                );
                output.childStateMachines.Add(new VFStateMachineChild {
                    stateMachine = childStateMachine,
                    position = child.position
                });
            }

            foreach (var child in raw.states) {
                var state = VFState.Load(
                    layer,
                    output,
                    child.state,
                    child.position,
                    context
                );
                output.statesValue.Add(state);
            }

            return output;
        }

        private static void LoadTransitionsRecursive(
            VFStateMachine output,
            AnimatorStateMachine raw,
            VFLoadContext context
        ) {
            if (output == null || raw == null || !context.LinkedStateMachines.Add(raw)) {
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
                LoadTransitionsRecursive(childStateMachine.stateMachine, rawChild.stateMachine, context);
            }

            for (var i = 0; i < raw.states.Length; i++) {
                output.statesValue[i].transitions.AddRange(
                    raw.states[i].state.transitions
                        .Select(t => VFTransition.Load(t, context.States, context.StateMachines))
                        .Where(t => t != null)
                );
            }
        }

        internal VFStateMachine Clone(VFLayer newLayer, VFCloneContext context) {
            var clone = CloneRecursive(newLayer, context);
            CloneTransitionsRecursive(clone, context);
            return clone;
        }

        private VFStateMachine CloneRecursive(
            VFLayer newLayer,
            VFCloneContext context
        ) {
            if (context.StateMachines.TryGetValue(this, out var existing)) {
                return existing;
            }
            var clone = new VFStateMachine(newLayer, null) {
                thisName = thisName,
                behaviours = behaviours.Clone(),
                entryPosition = entryPosition,
                anyStatePosition = anyStatePosition,
                exitPosition = exitPosition,
                parentStateMachinePosition = parentStateMachinePosition
            };
            context.StateMachines[this] = clone;

            foreach (var state in statesValue) {
                clone.statesValue.Add(state.Clone(newLayer, clone, context));
            }
            foreach (var child in childStateMachines) {
                clone.childStateMachines.Add(new VFStateMachineChild {
                    stateMachine = child.stateMachine.CloneRecursive(newLayer, context),
                    position = child.position
                });
            }

            return clone;
        }

        private void CloneTransitionsRecursive(
            VFStateMachine clone,
            VFCloneContext context
        ) {
            if (!context.LinkedStateMachines.Add(this)) return;
            clone.defaultState = defaultState != null ? context.States.GetOrDefault(defaultState) : null;
            clone.entryTransitionsValue.AddRange(entryTransitionsValue.Select(t => (VFEntryTransition)t.Clone(context.States, context.StateMachines)));
            clone.anyStateTransitionsValue.AddRange(anyStateTransitionsValue.Select(t => (VFTransition)t.Clone(context.States, context.StateMachines)));

            for (var i = 0; i < statesValue.Count; i++) {
                clone.statesValue[i].transitions.AddRange(
                    statesValue[i].transitions.Select(t => (VFTransition)t.Clone(context.States, context.StateMachines))
                );
            }
            for (var i = 0; i < childStateMachines.Count; i++) {
                clone.childStateMachines[i].transitions.AddRange(
                    childStateMachines[i].transitions.Select(t => (VFEntryTransition)t.Clone(context.States, context.StateMachines))
                );
                childStateMachines[i].stateMachine.CloneTransitionsRecursive(
                    clone.childStateMachines[i].stateMachine,
                    context
                );
            }
        }

        internal AnimatorStateMachine Save(VFSaveContext context) {
            var raw = SaveRecursive(context);
            SaveTransitionsRecursive(raw, context);
            return raw;
        }

        private AnimatorStateMachine SaveRecursive(VFSaveContext context) {
            if (context.StateMachines.TryGetValue(this, out var existing)) {
                return existing;
            }
            var raw = VrcfObjectFactory.Create<AnimatorStateMachine>();
            context.AddNewAsset(raw);
            context.StateMachines[this] = raw;

            raw.name = thisName;
            raw.behaviours = behaviours.Select(behaviour => behaviour.Save(context)).ToArray();
            raw.entryPosition = entryPosition;
            raw.anyStatePosition = anyStatePosition;
            raw.exitPosition = exitPosition;
            raw.parentStateMachinePosition = parentStateMachinePosition;

            foreach (var state in statesValue) {
                state.Save(context);
            }
            raw.states = statesValue
                .Select(state => state.ToChildAnimatorState(context.States))
                .ToArray();

            raw.stateMachines = childStateMachines
                .Select(child => new ChildAnimatorStateMachine {
                    stateMachine = child.stateMachine.SaveRecursive(context),
                    position = child.position
                })
                .ToArray();

            return raw;
        }

        private void SaveTransitionsRecursive(
            AnimatorStateMachine raw,
            VFSaveContext context
        ) {
            if (!context.LinkedStateMachines.Add(this)) return;
            raw.defaultState = defaultState != null ? context.States.GetOrDefault(defaultState) : null;
            raw.entryTransitions = entryTransitionsValue
                .Select(t => (AnimatorTransition)t.Save(context.States, context.StateMachines, context))
                .Where(t => t != null)
                .ToArray();
            raw.anyStateTransitions = anyStateTransitionsValue
                .Select(t => (AnimatorStateTransition)t.Save(context.States, context.StateMachines, context))
                .Where(t => t != null)
                .ToArray();

            foreach (var child in childStateMachines) {
                raw.SetStateMachineTransitions(
                    context.StateMachines[child.stateMachine],
                    child.transitions
                        .Select(t => (AnimatorTransition)t.Save(context.States, context.StateMachines, context))
                        .Where(t => t != null)
                        .ToArray()
                );
                child.stateMachine.SaveTransitionsRecursive(
                    context.StateMachines[child.stateMachine],
                    context
                );
            }
            foreach (var state in statesValue) {
                context.States[state].transitions = state.transitions
                    .Select(t => (AnimatorStateTransition)t.Save(context.States, context.StateMachines, context))
                    .Where(t => t != null)
                    .ToArray();
            }
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
