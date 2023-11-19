using UnityEditor.Animations;
using UnityEngine;
using VF.Builder;
using VF.Injector;
using VF.Utils;
using VF.Utils.Controller;

namespace VF.Service {
    [VFService]
    public class DirectBlendTreeService {
        [VFAutowired] private readonly AvatarManager manager;
        private BlendTree _tree;

        public BlendTree GetTree() {
            if (_tree != null) return _tree;
            var fx = manager.GetFx();
            var directLayer = fx.NewLayer("Direct Blend Tree Service");
            _tree = fx.NewBlendTree("Direct Blend Tree Service");
            _tree.blendType = BlendTreeType.Direct;
            directLayer.NewState("Tree").WithAnimation(_tree);
            return _tree;
        }

        public void Add(Motion motion) {
            Add(manager.GetFx().One(), motion);
        }
        
        public void Add(VFAFloat param, Motion motion) {
            GetTree().Add(param, motion);
        }
    }
}
