using System;
using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using VF.Inspector;
using VF.Utils.Controller;

namespace VF.Utils {
    internal static class BlendTreeExtensions {

        /**
         * Updating blend tree children is expensive if not needed, because it calls
         * AnimatorController.OnInvalidateAnimatorController
         */
        public static void RewriteChildren(this BlendTree tree, Func<ChildMotion, ChildMotion> rewrite) {
            tree.RewriteChildren(c => new [] { rewrite(c) });
        }
        public static void RewriteChildren(this BlendTree tree, Func<ChildMotion, IList<ChildMotion>> rewrite) {
            var updated = false;
            var newChildren = tree.children.SelectMany(child => {
                var newSubchildren = rewrite(child);
                updated |= newSubchildren.Count != 1 
                           || newSubchildren[0].motion != child.motion
                           || newSubchildren[0].threshold != child.threshold
                           || newSubchildren[0].position != child.position
                           || newSubchildren[0].timeScale != child.timeScale
                           || newSubchildren[0].cycleOffset != child.cycleOffset
                           || newSubchildren[0].directBlendParameter != child.directBlendParameter
                           || newSubchildren[0].mirror != child.mirror;
                return newSubchildren;
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

        public static bool GetNormalizedBlendValues(this BlendTree tree) {
            using (var so = new SerializedObject(tree)) {
                return so.FindProperty("m_NormalizedBlendValues").boolValue;
            }
        }
        
        public static void SetNormalizedBlendValues(this BlendTree tree, bool on) {
            using (var so = new SerializedObject(tree)) {
                so.FindProperty("m_NormalizedBlendValues").boolValue = on;
                so.ApplyModifiedPropertiesWithoutUndo();
            }
        }
    }
}
