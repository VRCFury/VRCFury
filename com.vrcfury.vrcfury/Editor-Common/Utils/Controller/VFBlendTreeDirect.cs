using UnityEditor.Animations;
using VF.Utils.Controller;

namespace VF.Utils {
    internal class VFBlendTreeDirect : VFBlendTree {
        private VFBlendTreeDirect(VFTree tree) : base(tree) {}

        public const string AlwaysOneParam = "__vrcf_dbt_always_one";

        public void Add(string param, VFMotion motion) {
            Add(motion, child => {
                child.directBlendParameter = param;
            });
        }

        public void Add(VFMotion motion) {
            Add(AlwaysOneParam, motion);
        }

        public void SetNormalizedBlendValues(bool on) {
            tree.SetNormalizedBlendValues(on);
        }

        protected override bool IsValidType(BlendTreeType type) {
            return type == BlendTreeType.Direct;
        }

        public static VFBlendTreeDirect Create(string name) {
            return new VFBlendTreeDirect(VFTree.Create(name, BlendTreeType.Direct));
        }
    }
}
