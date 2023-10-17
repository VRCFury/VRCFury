using UnityEditor.Animations;
using UnityEngine;
using VF.Utils.Controller;

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

        public static void Add(this BlendTree tree, VFAFloat param, Motion motion) {
            AddDirectChild(tree, param.Name(), motion);
        }
    }
}
