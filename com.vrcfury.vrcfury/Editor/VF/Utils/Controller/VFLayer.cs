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
        private readonly AnimatorController ctrl;
        private readonly AnimatorStateMachine _stateMachine;

        private Vector2 nextOffset = new Vector2(1, 0);
        private VFState lastCreatedState;

        public VFLayer(AnimatorController ctrl, AnimatorStateMachine stateMachine) {
            this.ctrl = ctrl;
            this._stateMachine = stateMachine;
        }

        public static bool operator ==(VFLayer a, VFLayer b) => a?._stateMachine == b?._stateMachine;
        public static bool operator !=(VFLayer a, VFLayer b) => !(a == b);
        public override bool Equals(object obj) => this == (VFLayer)obj;
        public override int GetHashCode() => _stateMachine.GetHashCode();

        public bool Exists() {
            return ctrl.layers.Any(l => l.stateMachine == _stateMachine);
        }

        public int GetLayerId() {
            var id = ctrl.layers
                .Select((l, i) => (l, i))
                .Where(tuple => tuple.Item1.stateMachine == _stateMachine)
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

            var s = _stateMachine.AddState(name);
            VrcfObjectFactory.Register(s);
            var node = GetLastNode().Value;
            node.state.writeDefaultValues = true;

            var state = new VFState(node, _stateMachine);
            
            if (lastCreatedState != null) {
                state.Move(lastCreatedState, nextOffset.x, nextOffset.y);
            } else {
                state.Move(_stateMachine.entryPosition, nextOffset.x, nextOffset.y);
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
            var states = _stateMachine.states;
            if (states.Length == 0) return null;
            return states[states.Length-1];
        }

        public void Move(int newIndex) {
            var layers = ctrl.layers;
            var myLayer = layers
                .First(l => l.stateMachine == _stateMachine);

            var newList = layers
                .Where(l => l.stateMachine != _stateMachine)
                .ToList();
            newList.Insert(newIndex, myLayer);
            ctrl.layers = newList.ToArray();
        }

        public void Remove() {
            ctrl.RemoveLayer(GetLayerId());
        }
        
        public IImmutableSet<AnimatorStateMachine> allStateMachines =>
            AnimatorIterator.GetRecursive(_stateMachine, sm => sm.stateMachines.Select(c => c.stateMachine));

        public bool hasSubMachines => _stateMachine.stateMachines.Any();

        public bool hasDefaultState => _stateMachine.defaultState != null;

        public Vector2 entryPosition => _stateMachine.entryPosition;

        public IList<VFState> states => _stateMachine.states
            .Select(c => new VFState(c, _stateMachine))
            .ToArray();
    }
}
