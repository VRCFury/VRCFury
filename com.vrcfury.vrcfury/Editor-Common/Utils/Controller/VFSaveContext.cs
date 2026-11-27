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
        // One save uses a fixed binding root and does not move or rename any objects.
        public Dictionary<VFGameObject, string> BindingPaths { get; } = new();
        public bool ReuseSourceAssets { get; }
        public IEnumerable<Object> NewAssets => newAssets;
        public IEnumerable<Object> OtherAssets => otherAssets;
        public long NewClipKeyCount { get; private set; }

        public VFSaveContext(VFGameObject bindingRoot, bool reuseSourceAssets = true) {
            BindingRoot = bindingRoot;
            ReuseSourceAssets = reuseSourceAssets;
        }

        public void AddNewAsset(Object asset) {
            if (asset != null) newAssets.Add(asset);
        }

        public void AddNewClip(AnimationClip clip, long keyCount) {
            if (newAssets.Add(clip)) NewClipKeyCount += keyCount;
        }

        public void AddOtherAsset(Object asset) {
            if (asset != null) otherAssets.Add(asset);
        }
    }
}
