using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using VRC.SDK3.Avatars.Components;

namespace VF.Builder {

    /**
     * Copies everything from one animator controller into another. Optionally rewriting all clips found along the way.
     */
    public class ControllerMerger {
        private readonly Func<string, string> _rewriteParamName;
        private readonly Action<AnimationClip> _rewriteClip;

        public ControllerMerger(
            Func<string, string> rewriteParamName = null,
            Action<AnimationClip> rewriteClip = null
        ) {
            this._rewriteParamName = rewriteParamName;
            this._rewriteClip = rewriteClip;
        }

        public void Merge(AnimatorController from_, ControllerManager toMain, MutableManager mutableManager) {
            var to = toMain.GetRaw();
            var from = mutableManager.CopyRecursive(from_, saveParent: to);
            var type = toMain.GetType();
            var layerOffset = to.layers.Length;

            var rewrittenParams = from.parameters
                .Concat(from.parameters.Select(p => {
                    p.name = RewriteParamName(p.name);
                    return p;
                }))
                .Where(p => {
                    var exists = to.parameters.Any(existing => existing.name == p.name);
                    return !exists;
                });
            to.parameters = to.parameters.Concat(rewrittenParams).ToArray();

            foreach (var layer in from.layers) {
                AnimatorIterator.ForEachState(layer.stateMachine, state => {
                    state.speedParameter = RewriteParamName(state.speedParameter);
                    state.cycleOffsetParameter = RewriteParamName(state.cycleOffsetParameter);
                    state.mirrorParameter = RewriteParamName(state.mirrorParameter);
                    state.timeParameter = RewriteParamName(state.timeParameter);
                });
                AnimatorIterator.ForEachBehaviour(layer.stateMachine, (b, add) => {
                    switch (b) {
                        case VRCAvatarParameterDriver oldB: {
                            foreach (var p in oldB.parameters) {
                                p.name = RewriteParamName(p.name);
                                p.source = RewriteParamName(p.source);
                            }
                            break;
                        }
                        case VRCAnimatorLayerControl oldB: {
                            if (VRCFEnumUtils.GetName(oldB.playable) == VRCFEnumUtils.GetName(type)) {
                                oldB.layer += layerOffset;
                            }
                            break;
                        }
                    }
                    return true;
                });
                AnimatorIterator.ForEachBlendTree(layer.stateMachine, tree => {
                    tree.blendParameter = RewriteParamName(tree.blendParameter);
                    tree.blendParameterY = RewriteParamName(tree.blendParameterY);
                    tree.children = tree.children.Select(child => {
                        child.directBlendParameter = RewriteParamName(child.directBlendParameter);
                        return child;
                    }).ToArray();
                });
                var allClips = new HashSet<AnimationClip>();
                AnimatorIterator.ForEachClip(layer.stateMachine, clip => {
                    allClips.Add(clip);
                });
                foreach (var clip in allClips) {
                    RewriteClip(clip);
                }

                AnimatorIterator.ForEachTransition(layer.stateMachine, transition => {
                    transition.conditions = transition.conditions.Select(c => {
                        c.parameter = RewriteParamName(c.parameter);
                        return c;
                    }).ToArray();
                    EditorUtility.SetDirty(transition);
                });
            }

            toMain.TakeLayersFrom(from);
            AssetDatabase.RemoveObjectFromAsset(from);
        }

        private string RewriteParamName(string name) {
            if (_rewriteParamName == null) return name;
            return _rewriteParamName(name);
        }
        
        private void RewriteClip(AnimationClip clip) {
            if (_rewriteClip != null) _rewriteClip(clip);
        }
    }

}
