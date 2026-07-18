using System;
using UnityEditor.Animations;
using VF.Utils.Controller;

namespace VF.Utils {
    internal abstract class VFBlendTree {
        protected readonly VFTree tree;

        protected VFBlendTree(VFTree tree) {
            this.tree = tree;
            AssertValidType();
        }

        protected abstract bool IsValidType(BlendTreeType type);

        private void AssertValidType() {
            if (!IsValidType(tree.blendType)) throw new Exception("Blendtree is unexpectedly the wrong type");
        }

        public static implicit operator VFMotion(VFBlendTree d) => d.tree;

        protected delegate void WithChild(VFTreeChild child);
        protected void Add(VFMotion motion, WithChild with) {
            AssertValidType();
            if (motion == null) throw new Exception("motion cannot be null");

            var newChild = new VFTreeChild {
                timeScale = 1,
                motion = motion
            };
            with(newChild);
            tree.AddChild(newChild);
        }
    }
}
