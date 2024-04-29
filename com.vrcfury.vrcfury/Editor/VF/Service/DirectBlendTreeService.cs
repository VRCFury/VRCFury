using System;
using System.Linq;
using JetBrains.Annotations;
using UnityEditor.Animations;
using UnityEngine;
using VF.Builder;
using VF.Feature.Base;
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
            GetTree().Add(param, motion);
        }

        [FeatureBuilderAction(FeatureOrder.OptimizeSharedDbt)]
        public void Optimize() {
            if (_tree == null) return;
            
            // Everything in the shared DBT has to be one frame, or else smoothing can be impacted in some cases
            if (!_tree.IsStatic()) {
                throw new Exception(
                    "Something tried to add a non-static clip to the VRCF shared DBT. This is likely a bug.");
            }
            _tree.MakeZeroLength();
            Flatten(_tree);
        }

        private void Flatten([CanBeNull] Motion motion) {
            if (motion is BlendTree tree) {
                foreach (var child in tree.children) {
                    Flatten(child.motion);
                }

                if (tree.blendType == BlendTreeType.Direct) {
                    tree.children = tree.children.SelectMany(child => {
                        if (child.directBlendParameter == manager.GetFx().One().Name() &&
                            child.motion is BlendTree childTree && childTree.blendType == BlendTreeType.Direct) {
                            return childTree.children;
                        }
                        return new ChildMotion[] { child };
                    }).ToArray();
                }
            }
        }
    }
}
