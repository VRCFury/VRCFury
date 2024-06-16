using System;
using System.Collections.Generic;
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
        private HashSet<BlendTree> created = new HashSet<BlendTree>();

        private BlendTree _tree;
        public BlendTree GetTree() {
            if (_tree == null) {
                var name = $"DBT for {parent.parent.GetType().Name}";
                if (parent.parent is FeatureBuilder builder) {
                    name += $" #{builder.uniqueModelNum}";
                }
                var fx = manager.GetFx();
                var directLayer = fx.NewLayer(name);
                _tree = Create(name);
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

        public BlendTree Create(string name) {
            var tree = clipFactory.NewBlendTree(name);
            tree.blendType = BlendTreeType.Direct;
            created.Add(tree);
            return tree;
        }

        public bool Created(BlendTree tree) {
            return created.Contains(tree);
        }
    }
}
