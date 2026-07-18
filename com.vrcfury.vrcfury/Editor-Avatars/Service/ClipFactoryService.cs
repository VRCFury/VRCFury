using UnityEngine;
using VF.Feature.Base;
using VF.Injector;
using VF.Utils;
using VF.Utils.Controller;

namespace VF.Service {
    [VFService]
    [VFPrototypeScope]
    internal class ClipFactoryService {
        [VFAutowired] private readonly VFInjectorParent parent;

        public VFClip GetEmptyClip() {
            return NewClip("Empty");
        }
        public VFClip NewClip(string name, bool usePrefix = true) {
            return VFClip.Create(usePrefix ? $"{GetPrefix()}/{name}" : name);
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
