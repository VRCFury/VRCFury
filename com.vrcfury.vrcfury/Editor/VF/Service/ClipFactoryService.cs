using System;
using System.Collections.Generic;
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
        public AnimationClip NewClip(string name, bool usePrefix = true) {
            var clip = VrcfObjectFactory.Create<AnimationClip>();
            clip.name = usePrefix ? $"{GetPrefix()}/{name}" : name;
            return clip;
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
