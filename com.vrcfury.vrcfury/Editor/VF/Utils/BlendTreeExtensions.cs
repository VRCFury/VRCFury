using System;
using System.Linq;
using UnityEditor.Animations;
using UnityEngine;
using VF.Inspector;
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

        /**
         * Updating blend tree children is expensive if not needed, because it calls
         * AnimatorController.OnInvalidateAnimatorController
         */
        public static void RewriteChildren(this BlendTree tree, Func<ChildMotion, ChildMotion> rewrite) {
            var updated = false;
            var newChildren = tree.children.Select(child => {
                var newChild = rewrite(child);
                updated |= newChild.motion != child.motion
                           || newChild.threshold != child.threshold
                           || newChild.position != child.position
                           || newChild.timeScale != child.timeScale
                           || newChild.cycleOffset != child.cycleOffset
                           || newChild.directBlendParameter != child.directBlendParameter
                           || newChild.mirror != child.mirror;
                return newChild;
            }).ToArray();
            if (updated) {
                tree.children = newChildren;
                VRCFuryEditorUtils.MarkDirty(tree);
            }
        }

        public static void RewriteParameters(this BlendTree tree, Func<string, string> rewriteParamName) {
            if (tree.blendType != BlendTreeType.Direct) {
                tree.blendParameter = rewriteParamName(tree.blendParameter);
                if (tree.blendType != BlendTreeType.Simple1D) {
                    tree.blendParameterY = rewriteParamName(tree.blendParameterY);
                }
            }
            tree.RewriteChildren(child => {
                if (tree.blendType == BlendTreeType.Direct) {
                    child.directBlendParameter = rewriteParamName(child.directBlendParameter);
                }
                return child;
            });
            VRCFuryEditorUtils.MarkDirty(tree);
        }
    }
}
