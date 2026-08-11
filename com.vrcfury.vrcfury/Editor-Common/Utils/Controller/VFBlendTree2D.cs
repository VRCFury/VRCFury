using UnityEditor.Animations;
using UnityEngine;
using VF.Utils.Controller;

namespace VF.Utils {
    internal class VFBlendTree2D : VFBlendTree {
        private VFBlendTree2D(VFTree tree) : base(tree) {}

        public void Add(Vector2 position, VFMotion motion) {
            Add(motion, child => {
                child.position = position;
            });
        }

        protected override bool IsValidType(BlendTreeType type) {
            return type == BlendTreeType.FreeformCartesian2D
                || type == BlendTreeType.FreeformDirectional2D
                || type == BlendTreeType.SimpleDirectional2D;
        }

        public static VFBlendTree2D CreateSimpleDirectional(string name, string blendParameterX, string blendParameterY) {
            return new VFBlendTree2D(VFTree.Create(name, BlendTreeType.SimpleDirectional2D, blendParameterX, blendParameterY));
        }

        public static VFBlendTree2D CreateFreeformDirectional(string name, string blendParameterX, string blendParameterY) {
            return new VFBlendTree2D(VFTree.Create(name, BlendTreeType.FreeformDirectional2D, blendParameterX, blendParameterY));
        }
    }
}
