using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using VF.Builder;
using VF.Inspector;
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

        public static void RewriteParameters(this AnimatorController c, Func<string, string> rewriteParamName, bool includeWrites = true) {
            // Params
            if (includeWrites) {
                var prms = c.parameters;
                foreach (var p in prms) {
                    p.name = rewriteParamName(p.name);
                }

                c.parameters = prms;
            }

            // States
            foreach (var state in new AnimatorIterator.States().From(c)) {
                state.speedParameter = rewriteParamName(state.speedParameter);
                state.cycleOffsetParameter = rewriteParamName(state.cycleOffsetParameter);
                state.mirrorParameter = rewriteParamName(state.mirrorParameter);
                state.timeParameter = rewriteParamName(state.timeParameter);
                VRCFuryEditorUtils.MarkDirty(state);
            }

            // Parameter Drivers
            if (includeWrites) {
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
            }

            // Parameter Animations
            if (includeWrites) {
                foreach (var clip in new AnimatorIterator.Clips().From(c)) {
                    clip.Rewrite(AnimationRewriter.RewriteBinding(binding => {
                        if (binding.path != "") return binding;
                        if (binding.type != typeof(Animator)) return binding;
                        if (binding.IsMuscle()) return binding;
                        binding.propertyName = rewriteParamName(binding.propertyName);
                        return binding;
                    }));
                }
            }

            // Blend trees
            foreach (var tree in new AnimatorIterator.Trees().From(c)) {
                tree.blendParameter = rewriteParamName(tree.blendParameter);
                tree.blendParameterY = rewriteParamName(tree.blendParameterY);
                tree.children = tree.children.Select(child => {
                    child.directBlendParameter = rewriteParamName(child.directBlendParameter);
                    return child;
                }).ToArray();
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
        
        public static IEnumerable<MutableLayer> GetLayers(this AnimatorController ctrl) {
            return ctrl.layers.Select(l => new MutableLayer(ctrl, l.stateMachine));
        }

        public static bool ContainsLayer(this AnimatorController ctrl, AnimatorStateMachine stateMachine) {
            return ctrl.layers.Any(l => l.stateMachine == stateMachine);
        }

        public static int GetLayerId(this AnimatorController ctrl, AnimatorStateMachine stateMachine) {
            return ctrl.layers
                .Select((l, i) => (l, i))
                .Where(tuple => tuple.Item1.stateMachine == stateMachine)
                .Select(tuple => tuple.Item2)
                .First();
        }
        
        public static MutableLayer GetLayer(this AnimatorController ctrl, int index) {
            return new MutableLayer(ctrl, ctrl.layers[index].stateMachine);
        }
        
        public static MutableLayer GetLayer(this AnimatorController ctrl, AnimatorStateMachine stateMachine) {
            return ctrl.GetLayer(ctrl.GetLayerId(stateMachine));
        }

    }

    public class MutableLayer {
        private AnimatorController ctrl;
        private AnimatorStateMachine _stateMachine;
        
        public static implicit operator AnimatorStateMachine(MutableLayer d) => d._stateMachine;

        public MutableLayer(AnimatorController ctrl, AnimatorStateMachine stateMachine) {
            this.ctrl = ctrl;
            this._stateMachine = stateMachine;
        }

        public bool Exists() {
            return ctrl.ContainsLayer(_stateMachine);
        }

        public int GetLayerId() {
            return ctrl.GetLayerId(_stateMachine);
        }

        private void WithLayer(Action<AnimatorControllerLayer> with) {
            var layers = ctrl.layers;
            with(layers[GetLayerId()]);
            ctrl.layers = layers;
        }

        public float weight {
            get => ctrl.layers[GetLayerId()].defaultWeight;
            set { WithLayer(l => l.defaultWeight = value); }
        }
        
        public string name {
            get => ctrl.layers[GetLayerId()].name;
            set { WithLayer(l => l.name = value); }
        }
        
        public AnimatorLayerBlendingMode blendingMode {
            get => ctrl.layers[GetLayerId()].blendingMode;
            set { WithLayer(l => l.blendingMode = value); }
        }
        
        public AvatarMask mask {
            get => ctrl.layers[GetLayerId()].avatarMask;
            set { WithLayer(l => l.avatarMask = value); }
        }
        
        public static bool operator ==(MutableLayer a, MutableLayer b) {
            return a?._stateMachine == b?._stateMachine;
        }
        public static bool operator !=(MutableLayer a, MutableLayer b) {
            return !(a == b);
        }
        public override bool Equals(object obj) {
            return this == (MutableLayer)obj;
        }
        public override int GetHashCode() {
            return _stateMachine.GetHashCode();
        }

        public AnimatorStateMachine stateMachine => _stateMachine;
    }
}
