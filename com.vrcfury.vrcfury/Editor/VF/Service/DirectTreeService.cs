using UnityEditor.Animations;
using UnityEngine;
using VF.Builder;
using VF.Injector;
using VF.Utils;

namespace VF.Service {
    /** Provides a shared direct tree that can be used by other services / builders */
    [VFService]
    public class DirectTreeService {
        [VFAutowired] private readonly AvatarManager manager;

        private BlendTree tree;
        private BlendTree Get() {
            if (tree == null) {
                var fx = manager.GetFx();
                var layer = fx.NewLayer($"VRCFury Shared Direct Tree");
                tree = fx.NewBlendTree($"VRCFury Shared Direct Tree");
                tree.blendType = BlendTreeType.Direct;
                layer.NewState("Direct Tree").WithAnimation(tree);
            }

            return tree;
        }

        public void Add(VFAFloat param, AnimationClip clip) {
            Get().AddDirectChild(param.Name(), clip);
        }
    }
}
