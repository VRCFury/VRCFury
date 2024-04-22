using System;
using JetBrains.Annotations;
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
        private VFLayer _layer;
        private BlendTree _tree;

        [CanBeNull] public VFLayer GetLayer() {
            return _layer;
        }

        public BlendTree GetTree() {
            if (_tree == null) {
                var fx = manager.GetFx();
                var directLayer = fx.NewLayer("Direct Blend Tree Service");
                _layer = directLayer;
                _tree = fx.NewBlendTree("Direct Blend Tree Service");
                _tree.blendType = BlendTreeType.Direct;
                directLayer.NewState("Tree").WithAnimation(_tree);
            }
            return _tree;
        }

        public void Add(Motion motion) {
            Add(manager.GetFx().One(), motion);
        }
        
        public void Add(VFAFloat param, Motion motion) {
            if (param.Name() == manager.GetFx().One().Name() && motion is BlendTree tree && tree.blendType == BlendTreeType.Direct) {
                foreach (var child in tree.children) {
                    Add(new VFAFloat(child.directBlendParameter, 0), child.motion);
                }
                return;
            }

            // Everything in the shared DBT has to be one frame, or else smoothing can be impacted in some cases
            var copy = MutableManager.CopyRecursive(motion, false);
            copy.MakeZeroLength();
            GetTree().Add(param, copy);
        }
    }
}
