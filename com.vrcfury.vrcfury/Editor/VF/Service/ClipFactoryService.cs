using System;
using UnityEditor.Animations;
using UnityEngine;
using VF.Feature.Base;
using VF.Injector;
using VF.Utils;

namespace VF.Service {
    [VFService]
    [VFPrototypeScope]
    internal class ClipFactoryService {
        [VFAutowired] private readonly VFInjectorParent parent;

        public AnimationClip GetEmptyClip() {
            return NewClip("Empty");
        }
        public AnimationClip NewClip(string name) {
            var clip = VrcfObjectFactory.Create<AnimationClip>();
            clip.name = $"{GetPrefix()}/{name}";
            return clip;
        }
        public BlendTree NewBlendTree(string name) {
            var tree = VrcfObjectFactory.Create<BlendTree>();
            tree.name = $"{GetPrefix()}/{name}";
            return tree;
        }

        public string GetPrefix() {
            var name = $"{parent.parent.GetType().Name}";
            if (parent.parent is FeatureBuilder builder) {
                name += $" #{builder.uniqueModelNum}";
                var prefix = builder.GetClipPrefix();
                if (prefix != null) name += $" ({prefix})";
            }
            return name;
        }
    }
}
