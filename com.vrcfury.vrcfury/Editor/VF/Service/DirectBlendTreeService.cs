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
    [VFPrototypeScope]
    internal class DirectBlendTreeService {
        [VFAutowired] private readonly AvatarManager manager;
        [VFAutowired] private readonly VFInjectorParent parent;
        [VFAutowired] private readonly ClipFactoryService clipFactory;
        private VFLayer _layer;
        private BlendTree _tree;

        [CanBeNull] public VFLayer GetLayer() {
            return _layer;
        }

        public BlendTree GetTree() {
            if (_tree == null) {
                var name = $"DBT for {parent.parent.GetType().Name}";
                if (parent.parent is FeatureBuilder builder) {
                    name += $" #{builder.uniqueModelNum}";
                }
                var fx = manager.GetFx();
                var directLayer = fx.NewLayer(name);
                _layer = directLayer;
                _tree = clipFactory.NewBlendTree(name);
                _tree.blendType = BlendTreeType.Direct;
                directLayer.NewState("DBT").WithAnimation(_tree);
            }
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
