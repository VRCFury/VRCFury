using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using Editor.VF.Utils;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using VF.Builder;
using VF.Inspector;
using VF.Utils.Controller;
using VRC.SDK3.Avatars.Components;

namespace VF.Utils {
    public static class AnimatorControllerExtensions {
        public static void Rewrite(this AnimatorController c, AnimationRewriter rewriter) {
            // Rewrite clips
            foreach (var clip in new AnimatorIterator.Clips().From(c)) {
                clip.Rewrite(rewriter);
            }

            // Rewrite masks
            foreach (var layer in c.layers) {
                var mask = layer.avatarMask;
                if (mask == null || mask.transformCount == 0) continue;
                mask.SetTransforms(mask.GetTransforms()
                    .Select(rewriter.RewritePath)
                    .Where(path => path != null));
            }
        }

        public static void RewriteParameters(this AnimatorController c, Func<string, string> rewriteParamNameNullUnsafe, bool includeWrites = true, ICollection<AnimatorStateMachine> limitToLayers = null) {
            string RewriteParamName(string str) {
                if (string.IsNullOrEmpty(str)) return str;
                return rewriteParamNameNullUnsafe(str);
            }
            var layers = c.layers
                .Where(l => limitToLayers == null || limitToLayers.Contains(l.stateMachine))
                .ToArray();
            
            // Params
            if (includeWrites && limitToLayers == null) {
                var prms = c.parameters;
                foreach (var p in prms) {
                    p.name = RewriteParamName(p.name);
                }

                c.parameters = prms;
            }

            // States
            foreach (var state in new AnimatorIterator.States().From(layers)) {
                state.speedParameter = RewriteParamName(state.speedParameter);
                state.cycleOffsetParameter = RewriteParamName(state.cycleOffsetParameter);
                state.mirrorParameter = RewriteParamName(state.mirrorParameter);
                state.timeParameter = RewriteParamName(state.timeParameter);
                VRCFuryEditorUtils.MarkDirty(state);
            }

            // Parameter Drivers
            if (includeWrites) {
                foreach (var b in new AnimatorIterator.Behaviours().From(layers)) {
                    if (b is VRCAvatarParameterDriver oldB) {
                        foreach (var p in oldB.parameters) {
                            p.name = RewriteParamName(p.name);
                            var sourceField = p.GetType().GetField("source");
                            if (sourceField != null) {
                                sourceField.SetValue(p, RewriteParamName((string)sourceField.GetValue(p)));
                            }
                        }
                    }
                }
            }

            // Parameter Animations
            if (includeWrites) {
                foreach (var clip in new AnimatorIterator.Clips().From(layers)) {
                    clip.Rewrite(AnimationRewriter.RewriteBinding(binding => {
                        if (binding.path != "") return binding;
                        if (binding.type != typeof(Animator)) return binding;
                        if (binding.IsMuscle()) return binding;
                        binding.propertyName = RewriteParamName(binding.propertyName);
                        return binding;
                    }));
                }
            }

            // Blend trees
            foreach (var tree in new AnimatorIterator.Trees().From(layers)) {
                tree.RewriteParameters(RewriteParamName);
            }

            // Transitions
            foreach (var transition in new AnimatorIterator.Transitions().From(layers)) {
                transition.RewriteConditions(cond => {
                    cond.parameter = RewriteParamName(cond.parameter);
                    return cond;
                });
                VRCFuryEditorUtils.MarkDirty(transition);
            }
            
            VRCFuryEditorUtils.MarkDirty(c);
        }
    }
}
