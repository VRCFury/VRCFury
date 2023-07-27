using UnityEditor.Animations;
using UnityEngine;

namespace VF.Utils {
    public static class BlendTreeExtensions {
        public static void AddDirectChild(this BlendTree tree, string param, Motion motion) {
            tree.AddChild(motion);
            var children = tree.children;
            var child = children[children.Length - 1];
            child.directBlendParameter = param;
            children[children.Length - 1] = child;
            tree.children = children;
        }
    }
}
