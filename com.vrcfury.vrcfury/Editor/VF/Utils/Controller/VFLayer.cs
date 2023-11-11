using System;
using System.Collections.Generic;
using UnityEditor.Animations;
using UnityEngine;
using VF.Builder;

namespace VF.Utils.Controller {
    public class VFLayer {
        private VFController ctrl;
        private AnimatorStateMachine _stateMachine;
        
        public static implicit operator AnimatorStateMachine(VFLayer d) => d?._stateMachine;

        public VFLayer(VFController ctrl, AnimatorStateMachine stateMachine) {
            this.ctrl = ctrl;
            this._stateMachine = stateMachine;
        }

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
        
        public AnimatorLayerBlendingMode blendingMode {
            get => ctrl.layers[GetLayerId()].blendingMode;
            set { WithLayer(l => l.blendingMode = value); }
        }
        
        public AvatarMask mask {
            get => ctrl.layers[GetLayerId()].avatarMask;
            set { WithLayer(l => l.avatarMask = value); }
        }
        
        public static bool operator ==(VFLayer a, VFLayer b) {
            return a?._stateMachine == b?._stateMachine;
        }
        public static bool operator !=(VFLayer a, VFLayer b) {
            return !(a == b);
        }
        public override bool Equals(object obj) {
            return this == (VFLayer)obj;
        }
        public override int GetHashCode() {
            return _stateMachine.GetHashCode();
        }

        public AnimatorStateMachine stateMachine => _stateMachine;

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
    }
}