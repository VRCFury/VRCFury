using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor.Animations;
using UnityEngine;
using VF.Builder;
using VF.Inspector;
using VRC.SDK3.Avatars.Components;

namespace VF.Utils {
    public static class AnimatorControllerExtensions {
        public static void RewritePaths(this AnimatorController c, Func<string,string> rewrite) {
            // Rewrite clips
            foreach (var clip in new AnimatorIterator.Clips().From(c)) {
                if (clip.IsProxyAnimation()) continue;
                clip.RewritePaths(rewrite);
            }

            // Rewrite masks
            foreach (var layer in c.layers) {
                var mask = layer.avatarMask;
                if (mask == null || mask.transformCount == 0) continue;
                mask.SetTransforms(mask.GetTransforms().Select(rewrite).Where(path => path != null));
            }
        }

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
            
            // Parameter Animations
            foreach (var clip in new AnimatorIterator.Clips().From(c)) {
                foreach (var binding in clip.GetFloatBindings()) {
                    if (binding.path != "") continue;
                    if (binding.type != typeof(Animator)) continue;

                    var propName = binding.propertyName;
                    if (IsMuscle(propName)) continue;

                    var newPropName = rewriteParamName(propName);
                    if (propName != newPropName) {
                        var newBinding = binding;
                        newBinding.propertyName = newPropName;
                        clip.SetFloatCurve(newBinding, clip.GetFloatCurve(binding));
                        clip.SetFloatCurve(binding, null);
                    }
                }
            }

            // Motions
            foreach (var motion in new AnimatorIterator.Motions().From(c)) {
                if (motion is BlendTree tree) {
                    tree.blendParameter = rewriteParamName(tree.blendParameter);
                    tree.blendParameterY = rewriteParamName(tree.blendParameterY);
                    tree.children = tree.children.Select(child => {
                        child.directBlendParameter = rewriteParamName(child.directBlendParameter);
                        return child;
                    }).ToArray();
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
        
        private static HashSet<string> _humanMuscleList;
        private static HashSet<string> GetHumanMuscleList() {
            if (_humanMuscleList != null) return _humanMuscleList;
            _humanMuscleList = new HashSet<string>();
            _humanMuscleList.UnionWith(HumanTrait.MuscleName);
            return _humanMuscleList;
        }
        private static bool IsMuscle(string name) {
            return GetHumanMuscleList().Contains(name)
                   || name.EndsWith(" Stretched")
                   || name.EndsWith(".Spread")
                   || name.EndsWith(".x")
                   || name.EndsWith(".y")
                   || name.EndsWith(".z")
                   || name.EndsWith(".w");
        }
    }
}
