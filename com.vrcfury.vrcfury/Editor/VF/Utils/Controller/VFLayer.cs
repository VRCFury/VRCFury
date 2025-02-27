using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using VF.Builder;

namespace VF.Utils.Controller {
    internal class VFLayer {
        private readonly AnimatorStateMachine rootStateMachine;
        private readonly AnimatorController ctrl;

        private Vector2 nextOffset = new Vector2(1, 0);
        private VFState lastCreatedState;

        public VFLayer(AnimatorController ctrl, AnimatorStateMachine rootStateMachine) {
            this.ctrl = ctrl;
            this.rootStateMachine = rootStateMachine;
        }

        public static bool operator ==(VFLayer a, VFLayer b) => a?.rootStateMachine == b?.rootStateMachine;
        public static bool operator !=(VFLayer a, VFLayer b) => !(a == b);
        public override bool Equals(object obj) => this == (VFLayer)obj;
        public override int GetHashCode() => rootStateMachine.GetHashCode();

        public bool Exists() {
            return ctrl.layers.Any(l => l.stateMachine == rootStateMachine);
        }

        public int GetLayerId() {
            var id = ctrl.layers
                .Select((l, i) => (l, i))
                .Where(tuple => tuple.Item1.stateMachine == rootStateMachine)
                .Select(tuple => tuple.Item2)
                .DefaultIfEmpty(-1)
                .First();
            if (id == -1) {
                throw new Exception("Layer not found in controller. It may have been accessed after it was removed.");
            }
            return id;
        }

        private void WithLayer(Action<AnimatorControllerLayer> with) {
            var layers = ctrl.layers;
            with(layers[GetLayerId()]);
            ctrl.layers = layers;
        }

        public float weight {
            get => ctrl.layers[GetLayerId()].defaultWeight;
            set { WithLayer(l => l.defaultWeight = value); }
        }
        
        public string name {
            get => ctrl.layers[GetLayerId()].name;
            set { WithLayer(l => l.name = value); }
        }

        public string debugName => $"Controller `{ctrl.name}` Layer `{name}`";

        public AnimatorLayerBlendingMode blendingMode {
            get => ctrl.layers[GetLayerId()].blendingMode;
            set { WithLayer(l => l.blendingMode = value); }
        }
        
        public AvatarMask mask {
            get => ctrl.layers[GetLayerId()].avatarMask;
            set { WithLayer(l => l.avatarMask = value); }
        }

        private static string WrapStateName(string name, int attemptWrapAt = 35) {
            var lines = new List<string>();
            var currentLine = "";
            foreach (var c in name) {
                if (c == '\n' || (char.IsWhiteSpace(c) && currentLine.Length > attemptWrapAt)) {
                    lines.Add(currentLine);
                    currentLine = "";
                    continue;
                }
                if (char.IsWhiteSpace(c) && currentLine.Length == 0) {
                    continue;
                }
                currentLine += c;
            }
            if (!string.IsNullOrWhiteSpace(currentLine)) lines.Add(currentLine);
            return lines.Join('\n');
        }

        public void SetNextOffset(float x, float y) {
            nextOffset = new Vector2(x, y);
        }

        private static readonly HashSet<AnimatorState> createdStates = new HashSet<AnimatorState>();

        [InitializeOnLoadMethod]
        private static void ClearCreatedStates() {
            EditorApplication.update += () => createdStates.Clear();
        }

        public VFState NewState(string name) {
            // Unity breaks if name contains .
            name = WrapStateName(name);
            name = name.Replace(".", "");

            var s = rootStateMachine.AddState(name);
            VrcfObjectFactory.Register(s);
            var node = GetLastNode().Value;
            node.state.writeDefaultValues = true;

            var state = new VFState(this, node, rootStateMachine);
            
            if (lastCreatedState != null) {
                state.Move(lastCreatedState, nextOffset.x, nextOffset.y);
            } else {
                state.Move(rootStateMachine.entryPosition, nextOffset.x, nextOffset.y);
            }

            SetNextOffset(0, 1);
            lastCreatedState = state;
            createdStates.Add(node.state);
            return state;
        }

        public static bool Created(AnimatorState state) {
            return createdStates.Contains(state);
        }

        private ChildAnimatorState? GetLastNode() {
            var s = rootStateMachine.states;
            if (s.Length == 0) return null;
            return s[s.Length-1];
        }

        public void Move(int newIndex) {
            var layers = ctrl.layers;
            var myLayer = layers
                .First(l => l.stateMachine == rootStateMachine);

            var newList = layers
                .Where(l => l.stateMachine != rootStateMachine)
                .ToList();
            newList.Insert(newIndex, myLayer);
            ctrl.layers = newList.ToArray();
        }

        public void Remove() {
            ctrl.RemoveLayer(GetLayerId());
        }

        private IReadOnlyCollection<AnimatorStateMachine> allRawStateMachines =>
            AnimatorIterator.GetRecursive(rootStateMachine, s => s.stateMachines.Select(c => c.stateMachine));

        public IReadOnlyCollection<VFStateMachine> allStateMachines =>
            allRawStateMachines.Select(sm => new VFStateMachine(this, sm)).ToArray();

        public bool hasSubMachines => rootStateMachine.stateMachines.Any();
        public bool hasDefaultState => rootStateMachine.defaultState != null;
        public AnimatorState defaultState => rootStateMachine.defaultState;
        public AnimatorTransition[] entryTransitions => rootStateMachine.entryTransitions;
        public Vector2 entryPosition => rootStateMachine.entryPosition;

        public IImmutableSet<AnimatorState> allStates => allRawStateMachines
            .SelectMany(sm => sm.states)
            .Select(child => child.state)
            .NotNull()
            .ToImmutableHashSet();

        private IImmutableSet<VFBehaviourContainer> allBehaviourContainers => allStateMachines
            .SelectMany(sm => sm.states.OfType<VFBehaviourContainer>().Append(sm))
            .ToImmutableHashSet();

        public IImmutableSet<StateMachineBehaviour> allBehaviours => allBehaviourContainers
            .SelectMany(container => container.behaviours)
            .ToImmutableHashSet();

        public void RewriteBehaviours(Func<StateMachineBehaviour, OneOrMany<StateMachineBehaviour>> action) {
            RewriteBehaviours<StateMachineBehaviour>(action);
        }
        public void RewriteBehaviours<T>(Func<T, OneOrMany<StateMachineBehaviour>> action) where T : StateMachineBehaviour {
            foreach (var container in allBehaviourContainers) {
                container.behaviours = container.behaviours.SelectMany(b => b is T t ? action(t).Get() : new [] { b }).ToArray();
            }
        }

        public void RewriteConditions(Func<AnimatorCondition, AnimatorTransitionBaseExtensions.Rewritten> action) {
            RewriteTransitions(t => t.RewriteConditions(action));
        }

        public IImmutableSet<AnimatorTransitionBase> allTransitions => allRawStateMachines
            .SelectMany(sm =>
                sm.entryTransitions
                    .Concat<AnimatorTransitionBase>(sm.anyStateTransitions)
                    .Concat(sm.stateMachines.SelectMany(childSm => sm.GetStateMachineTransitions(childSm.stateMachine)))
                    .Concat(sm.states.SelectMany(child => child.state.transitions))
            )
            .NotNull()
            .ToImmutableHashSet();

        public void RewriteTransitions(Func<AnimatorTransitionBase, OneOrMany<AnimatorTransitionBase>> action) {
            foreach (var sm in allRawStateMachines) {
                RewriteTransitions(sm.entryTransitions, a => sm.entryTransitions = a, action);
                RewriteTransitions(sm.anyStateTransitions, a => sm.anyStateTransitions = a, action);
                foreach (var childSm in sm.stateMachines.Select(child => child.stateMachine)) {
                    RewriteTransitions(sm.GetStateMachineTransitions(childSm), a => sm.SetStateMachineTransitions(childSm, a), action);
                }
                foreach (var state in sm.states.Select(child => child.state)) {
                    RewriteTransitions(state.transitions, a => state.transitions = a, action);
                }
            }
        }

        private static void RewriteTransitions<T>(
            T[] input,
            Action<T[]> setter,
            Func<AnimatorTransitionBase, OneOrMany<AnimatorTransitionBase>> action
        ) where T : AnimatorTransitionBase {
            var changed = false;
            var output = input.SelectMany(oneTransition => {
                var result = action(oneTransition).Get();
                changed |= result.Count != 1 || result[0] != oneTransition;
                return result;
            }).OfType<T>().ToArray();
            if (changed) setter(output);
        }
    }
}
