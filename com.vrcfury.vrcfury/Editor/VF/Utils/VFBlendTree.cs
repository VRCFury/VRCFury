using System;
using UnityEditor.Animations;
using UnityEngine;

namespace VF.Utils {
    internal abstract class VFBlendTree {
        protected readonly BlendTree tree;

        protected VFBlendTree(BlendTree tree) {
            this.tree = tree;
            AssertValidType();
        }

        protected abstract bool IsValidType();

        protected void AssertValidType() {
            if (!IsValidType()) throw new Exception("Blendtree is unexpectedly the wrong type");
        }
        
        public static implicit operator Motion(VFBlendTree d) => d.tree;
    }

    internal class VFBlendTreeDirect : VFBlendTree {
        public VFBlendTreeDirect(BlendTree tree) : base(tree) {}

        public void Add(string param, Motion motion) {
            AssertValidType();
            if (motion == null) throw new Exception("motion cannot be null");
            tree.AddChild(motion);
            var children = tree.children;
            var child = children[children.Length - 1];
            child.directBlendParameter = param;
            children[children.Length - 1] = child;
            tree.children = children;
        }

        public void SetNormalizedBlendValues(bool on) {
            tree.SetNormalizedBlendValues(on);
        }

        protected override bool IsValidType() {
            return tree.blendType == BlendTreeType.Direct;
        }
    }
    
    internal class VFBlendTree1D : VFBlendTree {
        public VFBlendTree1D(BlendTree tree) : base(tree) {}

        public void Add(float threshold, Motion motion) {
            AssertValidType();
            if (motion == null) throw new Exception("motion cannot be null");
            tree.AddChild(motion, threshold);
        }
        
        protected override bool IsValidType() {
            return tree.blendType == BlendTreeType.Simple1D;
        }
    }
    
    internal class VFBlendTree2D : VFBlendTree {
        public VFBlendTree2D(BlendTree tree) : base(tree) {}

        public void Add(Vector2 position, Motion motion) {
            AssertValidType();
            if (motion == null) throw new Exception("motion cannot be null");
            tree.AddChild(motion, position);
        }
        
        protected override bool IsValidType() {
            return tree.blendType == BlendTreeType.FreeformCartesian2D
                || tree.blendType == BlendTreeType.FreeformDirectional2D
                || tree.blendType == BlendTreeType.SimpleDirectional2D;
        }
    }
}
