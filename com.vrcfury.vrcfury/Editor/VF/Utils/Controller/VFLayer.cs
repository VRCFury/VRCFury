using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor.Animations;
using UnityEngine;
using VF.Builder;

namespace VF.Utils.Controller {
    public class VFLayer {
        private readonly VFController ctrl;
        private readonly AnimatorStateMachine _stateMachine;
        
        public VFLayer(VFController ctrl, AnimatorStateMachine stateMachine) {
            this.ctrl = ctrl;
            this._stateMachine = stateMachine;
        }
        
        public static implicit operator AnimatorStateMachine(VFLayer d) => d?._stateMachine;
        public static bool operator ==(VFLayer a, VFLayer b) => a?._stateMachine == b?._stateMachine;
        public static bool operator !=(VFLayer a, VFLayer b) => !(a == b);
        public override bool Equals(object obj) => this == (VFLayer)obj;
        public override int GetHashCode() => _stateMachine.GetHashCode();

        public AnimatorStateMachine stateMachine => _stateMachine;

        public bool Exists() {
            return ctrl.ContainsLayer(_stateMachine);
        }

        public int GetLayerId() {
            return ctrl.GetLayerId(_stateMachine);
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

        public string debugName => $"Controller `{ctrl.GetRaw().name}` Layer `{name}`";

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
            return string.Join("\n", lines);
        }

        public VFState NewState(string name) {
            // Unity breaks if name contains .
            name = WrapStateName(name);
            name = name.Replace(".", "");

            var lastNode = GetLastNodeForPositioning();
            _stateMachine.AddState(name);
            var node = GetLastNode().Value;
            node.state.writeDefaultValues = true;

            var state = new VFState(node, _stateMachine);
            if (lastNode.HasValue) state.Move(lastNode.Value.position, 0, 1);
            else state.Move(_stateMachine.entryPosition, 1, 0);
            return state;
        }

        private ChildAnimatorState? GetLastNodeForPositioning() {
            var states = _stateMachine.states;
            var index = Array.FindLastIndex(states, state => !state.state.name.StartsWith("_"));
            if (index < 0) return null;
            return states[index];
        }

        private ChildAnimatorState? GetLastNode() {
            var states = _stateMachine.states;
            if (states.Length == 0) return null;
            return states[states.Length-1];
        }

        public AnimatorStateMachine GetRawStateMachine() {
            return _stateMachine;
        }

        public void Move(int newIndex) {
            var layers = ctrl.layers;
            var myLayer = layers
                .First(l => l.stateMachine == stateMachine);

            var newList = layers
                .Where(l => l.stateMachine != stateMachine)
                .ToList();
            newList.Insert(newIndex, myLayer);
            ctrl.layers = newList.ToArray();
        }

        public bool HasMuscles() {
            return new AnimatorIterator.Clips().From(this).Any(clip => clip.HasMuscles());
        }

        public void Remove() {
            ctrl.GetRaw().RemoveLayer(GetLayerId());
        }
    }
}
