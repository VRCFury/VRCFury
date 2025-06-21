using System;
using UnityEditor.Animations;
using UnityEngine;
using VF.Utils.Controller;

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
        
        protected static BlendTree NewBlendTree(string name, BlendTreeType type) {
            var tree = VrcfObjectFactory.Create<BlendTree>();
            tree.name = name;
            tree.useAutomaticThresholds = false;
            tree.blendType = type;
            return tree;
        }
    }

    internal class VFBlendTreeDirect : VFBlendTree {
        public VFBlendTreeDirect(BlendTree tree) : base(tree) {}

        public const string AlwaysOneParam = "__vrcf_dbt_always_one";

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
        
        public void Add(Motion motion) {
            Add(AlwaysOneParam, motion);
        }

        public void SetNormalizedBlendValues(bool on) {
            tree.SetNormalizedBlendValues(on);
        }

        protected override bool IsValidType() {
            return tree.blendType == BlendTreeType.Direct;
        }

        public static VFBlendTreeDirect Create(string name) {
            var tree = NewBlendTree(name, BlendTreeType.Direct);
            return new VFBlendTreeDirect(tree);
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

        public static VFBlendTree1D Create(string name, string blendParameter) {
            var tree = NewBlendTree(name, BlendTreeType.Simple1D);
            tree.blendParameter = blendParameter;
            return new VFBlendTree1D(tree);
        }

        public static VFBlendTree1D CreateWithData(string name, VFAFloat param, params (float, Motion)[] children) {
            var tree = Create(name, param);
            foreach (var (threshold, motion) in children) {
                tree.Add(threshold, (motion != null) ? motion : VrcfObjectFactory.Create<AnimationClip>());
            }
            return tree;
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
        
        public static VFBlendTree2D CreateSimpleDirectional(string name, string blendParameterX, string blendParameterY) {
            var tree = NewBlendTree(name, BlendTreeType.SimpleDirectional2D);
            tree.blendParameter = blendParameterX;
            tree.blendParameterY = blendParameterY;
            return new VFBlendTree2D(tree);
        }
        
        public static VFBlendTree2D CreateFreeformDirectional(string name, string blendParameterX, string blendParameterY) {
            var tree = NewBlendTree(name, BlendTreeType.FreeformDirectional2D);
            tree.blendParameter = blendParameterX;
            tree.blendParameterY = blendParameterY;
            return new VFBlendTree2D(tree);
        }
    }
}
