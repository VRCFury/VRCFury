using UnityEditor.Animations;
using VF.Utils.Controller;

namespace VF.Utils {
    internal class VFBlendTree1D : VFBlendTree {
        private VFBlendTree1D(VFTree tree) : base(tree) {}

        public void Add(float threshold, VFMotion motion) {
            Add(motion, child => {
                child.threshold = threshold;
            });
        }

        protected override bool IsValidType(BlendTreeType type) {
            return type == BlendTreeType.Simple1D;
        }

        public static VFBlendTree1D Create(string name, string blendParameter) {
            return new VFBlendTree1D(VFTree.Create(name, BlendTreeType.Simple1D, blendParameter));
        }

        public static VFBlendTree1D CreateWithData(string name, string param, params (float, VFMotion)[] children) {
            var tree = Create(name, param);
            foreach (var (threshold, motion) in children) {
                tree.Add(threshold, motion ?? VFClip.Create());
            }
            return tree;
        }
    }
}
