using System.Collections.Generic;
using UnityEngine;

namespace VF.Utils.Controller {
    internal sealed class VFSaveContext {
        private readonly Dictionary<VFMotion, Motion> savedByMotion = new Dictionary<VFMotion, Motion>();
        public VFGameObject BindingRoot { get; }
        public bool ReuseSourceAssets { get; }

        public VFSaveContext(VFGameObject bindingRoot, bool reuseSourceAssets = true) {
            BindingRoot = bindingRoot;
            ReuseSourceAssets = reuseSourceAssets;
        }

        public bool TryGet(VFMotion motion, out Motion saved) {
            return savedByMotion.TryGetValue(motion, out saved);
        }

        public void Add(VFMotion motion, Motion saved) {
            savedByMotion[motion] = saved;
        }
    }
}
