using System.Collections.Generic;
using UnityEditor.Animations;
using UnityEngine;

namespace VF.Utils.Controller {
    internal sealed class VFSaveContext {
        private readonly HashSet<Object> newAssets = new HashSet<Object>();
        private readonly HashSet<Object> otherAssets = new HashSet<Object>();
        public Dictionary<VFStateMachine, AnimatorStateMachine> StateMachines { get; } = new Dictionary<VFStateMachine, AnimatorStateMachine>();
        public HashSet<VFStateMachine> LinkedStateMachines { get; } = new HashSet<VFStateMachine>();
        public Dictionary<VFState, AnimatorState> States { get; } = new Dictionary<VFState, AnimatorState>();
        public Dictionary<VFMotion, Motion> Motions { get; } = new Dictionary<VFMotion, Motion>();
        public VFGameObject BindingRoot { get; }
        public bool ReuseSourceAssets { get; }
        public IEnumerable<Object> NewAssets => newAssets;
        public IEnumerable<Object> OtherAssets => otherAssets;

        public VFSaveContext(VFGameObject bindingRoot, bool reuseSourceAssets = true) {
            BindingRoot = bindingRoot;
            ReuseSourceAssets = reuseSourceAssets;
        }

        public void AddNewAsset(Object asset) {
            if (asset != null) newAssets.Add(asset);
        }

        public void AddOtherAsset(Object asset) {
            if (asset != null) otherAssets.Add(asset);
        }
    }
}
