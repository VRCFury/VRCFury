using System;
using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;
using UnityEditor.Animations;
using UnityEngine;
using VF.Builder;

namespace VF.Utils.Controller {
    public class VFController {
        private readonly AnimatorController ctrl;

        private VFController(AnimatorController ctrl) {
            this.ctrl = ctrl;
        }
    
        public static implicit operator VFController(AnimatorController d) => new VFController(d);
        public static implicit operator AnimatorController(VFController d) => d?.ctrl;
        public static implicit operator bool(VFController d) => d?.ctrl;
        public static bool operator ==(VFController a, VFController b) => a?.Equals(b) ?? b == null;
        public static bool operator !=(VFController a, VFController b) => !(a == b);
        public override bool Equals(object other) {
            return (other is VFController a && ctrl == a.ctrl)
                   || (other is AnimatorController b && ctrl == b)
                   || (other == null && ctrl == null);
        }
        public override int GetHashCode() {
            return Tuple.Create(ctrl).GetHashCode();
        }

        public VFLayer NewLayer(string name, int insertAt = -1) {
            // Unity breaks if name contains .
            name = name.Replace(".", "");

            ctrl.AddLayer(name);
            var layers = ctrl.layers;
            var layer = layers.Last();
            if (insertAt >= 0) {
                for (var i = layers.Length-1; i > insertAt; i--) {
                    layers[i] = layers[i - 1];
                }
                layers[insertAt] = layer;
            }
            layer.defaultWeight = 1;
            layer.stateMachine.anyStatePosition = VFState.MovePos(layer.stateMachine.entryPosition, 0, 1);
            ctrl.layers = layers;
            return new VFLayer(this, layer.stateMachine);
        }
    
        public void RemoveLayer(int i) {
            // Due to some unity bug, removing any layer from a controller
            // also removes ALL layers marked as synced for some reason.
            // VRChat synced layers are broken anyways, so we can just turn them off.
            ctrl.layers = ctrl.layers.Select(layer => {
                layer.syncedLayerIndex = -1;
                return layer;
            }).ToArray();
            ctrl.RemoveLayer(i);
        }

        public void RemoveParameter(int i) {
            ctrl.RemoveParameter(i);
        }

        public VFABool NewTrigger(string name) {
            return new VFABool(NewParam(name, AnimatorControllerParameterType.Trigger));
        }
        public VFABool NewBool(string name, bool def = false) {
            return new VFABool(NewParam(name, AnimatorControllerParameterType.Bool, param => param.defaultBool = def));
        }
        public VFAFloat NewFloat(string name, float def = 0) {
            return new VFAFloat(NewParam(name, AnimatorControllerParameterType.Float, param => param.defaultFloat = def));
        }
        public VFAInteger NewInt(string name, int def = 0) {
            return new VFAInteger(NewParam(name, AnimatorControllerParameterType.Int, param => param.defaultInt = def));
        }
        private AnimatorControllerParameter NewParam(string name, AnimatorControllerParameterType type, Action<AnimatorControllerParameter> with = null) {
            var exists = Array.Find(ctrl.parameters, other => other.name == name);
            if (exists != null) return exists;
            ctrl.AddParameter(name, type);
            var parameters = ctrl.parameters;
            var param = parameters[parameters.Length-1];
            if (with != null) with(param);
            ctrl.parameters = parameters;
            return param;
        }
    
        public IEnumerable<VFLayer> GetLayers() {
            return ctrl.layers.Select(l => new VFLayer(this, l.stateMachine));
        }

        public bool ContainsLayer(AnimatorStateMachine stateMachine) {
            return ctrl.layers.Any(l => l.stateMachine == stateMachine);
        }

        public int GetLayerId(AnimatorStateMachine stateMachine) {
            return ctrl.layers
                .Select((l, i) => (l, i))
                .Where(tuple => tuple.Item1.stateMachine == stateMachine)
                .Select(tuple => tuple.Item2)
                .First();
        }

        [CanBeNull]
        public VFLayer GetLayer(int index) {
            var layers = ctrl.layers;
            if (index < 0 || index >= layers.Length) return null;
            return new VFLayer(this, layers[index].stateMachine);
        }

        public VFLayer GetLayer(AnimatorStateMachine stateMachine) {
            return GetLayer(GetLayerId(stateMachine));
        }

        public AnimatorControllerLayer[] layers {
            get => ctrl.layers;
            set => ctrl.layers = value;
        }

        public AnimatorControllerParameter[] parameters {
            get => ctrl.parameters;
            set => ctrl.parameters = value;
        }
    }
}