using System;
using System.Linq;
using UnityEditor.Animations;
using UnityEngine;
using VF.Builder;
using VF.Inspector;
using VRC.SDK3.Avatars.Components;

namespace VF.Utils {
    public static class AnimatorControllerExtensions {
        public static void RewriteParameters(this AnimatorController c, Func<string, string> rewriteParamName) {
            // Params
            var prms = c.parameters;
            foreach (var p in prms) {
                p.name = rewriteParamName(p.name);
            }
            c.parameters = prms;

            // States
            foreach (var state in new AnimatorIterator.States().From(c)) {
                state.speedParameter = rewriteParamName(state.speedParameter);
                state.cycleOffsetParameter = rewriteParamName(state.cycleOffsetParameter);
                state.mirrorParameter = rewriteParamName(state.mirrorParameter);
                state.timeParameter = rewriteParamName(state.timeParameter);
                VRCFuryEditorUtils.MarkDirty(state);
            }

            // Parameter Drivers
            foreach (var b in new AnimatorIterator.Behaviours().From(c)) {
                if (b is VRCAvatarParameterDriver oldB) {
                    foreach (var p in oldB.parameters) {
                        p.name = rewriteParamName(p.name);
                        var sourceField = p.GetType().GetField("source");
                        if (sourceField != null) {
                            sourceField.SetValue(p, rewriteParamName((string)sourceField.GetValue(p)));
                        }
                    }
                }
            }

            // Motions
            var rewriter = new ClipRewriter(
                rewriteParam: rewriteParamName
            );
            foreach (var motion in new AnimatorIterator.Motions().From(c)) {
                if (motion is BlendTree tree) {
                    tree.blendParameter = rewriteParamName(tree.blendParameter);
                    tree.blendParameterY = rewriteParamName(tree.blendParameterY);
                    tree.children = tree.children.Select(child => {
                        child.directBlendParameter = rewriteParamName(child.directBlendParameter);
                        return child;
                    }).ToArray();
                } else if (motion is AnimationClip clip) {
                    rewriter.Rewrite(clip);
                }
            }

            // Transitions
            foreach (var transition in new AnimatorIterator.Transitions().From(c)) {
                transition.conditions = transition.conditions.Select(cond => {
                    cond.parameter = rewriteParamName(cond.parameter);
                    return cond;
                }).ToArray();
                VRCFuryEditorUtils.MarkDirty(transition);
            }
            
            VRCFuryEditorUtils.MarkDirty(c);
        }
    }
}
