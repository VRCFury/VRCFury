using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using UnityEditor.Animations;
using UnityEngine;
using VF.Builder;
using VF.Feature;
using VF.Feature.Base;
using VF.Injector;
using VF.Utils;
using VRC.SDK3.Avatars.Components;

namespace VF.Service {
    [VFService]
    internal class BlendTreeOptimizingService {
        [VFAutowired] private readonly ControllersService controllers;
        private ControllerManager fx => controllers.GetFx();
        [VFAutowired] private readonly ParamsService paramsService;
        private ParamManager paramz => paramsService.GetParams();
        [VFAutowired] private readonly ClipFactoryTrackingService clipFactoryTracking;
        
        [FeatureBuilderAction(FeatureOrder.OptimizeBlendTrees)]
        public void Optimize() {
            var alwaysOneParams = GetAlwaysOneParams();
            foreach (var state in new AnimatorIterator.States().From(fx.GetRaw())) {
                if (state.motion is BlendTree tree && clipFactoryTracking.Created(tree)) {
                    Optimize(tree, alwaysOneParams);
                }
            }
        }

        private ISet<string> GetAlwaysOneParams() {
            var animatedParams = GetAnimatedParams();
            return fx.GetRaw().parameters
                .Where(p => p.type == AnimatorControllerParameterType.Float)
                .Where(p => p.defaultFloat == 1)
                .Where(p => !animatedParams.Contains(p.name))
                .Where(p => !FullControllerBuilder.VRChatGlobalParams.Contains(p.name))
                .Select(p => p.name)
                .ToImmutableHashSet();
        }

        private ISet<string> GetAnimatedParams() {
            var animatedParams = new HashSet<string>();
            var vrcControlled = paramz.GetRaw().parameters
                .Select(p => p.name);
            animatedParams.UnionWith(vrcControlled);
            var driven = controllers.GetAllUsedControllers()
                .SelectMany(c => new AnimatorIterator.Behaviours().From(c.GetRaw()))
                .OfType<VRCAvatarParameterDriver>()
                .SelectMany(driver => driver.parameters.Select(p => p.name));
            animatedParams.UnionWith(driven);
            var aaps = fx.GetClips()
                .SelectMany(clip => clip.GetFloatBindings())
                .Where(b => b.GetPropType() == EditorCurveBindingType.Aap)
                .Select(b => b.propertyName);
            animatedParams.UnionWith(aaps);
            return animatedParams;
        }

        private void Optimize(BlendTree tree, ISet<string> alwaysOneParams) {
            foreach (var child in tree.children) {
                if (child.motion is BlendTree t) Optimize(t, alwaysOneParams);
            }

            ClaimSubtreesWithOneChild(tree, alwaysOneParams);
            MergeSubtreesWithOneWeight(tree, alwaysOneParams);
            MergeClipsWithSameWeight(tree);
        }

        private void MergeSubtreesWithOneWeight(BlendTree tree, ISet<string> alwaysOneParams) {
            if (tree.blendType != BlendTreeType.Direct) return;
            if (tree.GetNormalizedBlendValues()) return;
            tree.RewriteChildren(child => {
                if (alwaysOneParams.Contains(child.directBlendParameter)
                    && child.motion is BlendTree childTree
                    && childTree.blendType == BlendTreeType.Direct
                    && !childTree.GetNormalizedBlendValues()
                ) {
                    return childTree.children;
                }
                return new ChildMotion[] { child };
            });
        }

        private void ClaimSubtreesWithOneChild(BlendTree tree, ISet<string> alwaysOneParams) {
            tree.RewriteChildren(child => {
                if (child.motion is BlendTree childTree
                    && childTree.blendType == BlendTreeType.Direct
                    && childTree.children.Length == 1
                    && alwaysOneParams.Contains(childTree.children[0].directBlendParameter)
                ) {
                    child.motion = childTree.children[0].motion;
                }
                return new ChildMotion[] { child };
            });
        }

        private void MergeClipsWithSameWeight(BlendTree tree) {
            if (tree.blendType != BlendTreeType.Direct) return;
            if (tree.GetNormalizedBlendValues()) return;

            var children = tree.children;
            var motions = children.Select(child => child.motion).ToList();
            var clones = motions.Select(motion => {
                if (!(motion is AnimationClip clip)) return null;
                var clone = clip.Clone();
                clone.name = $"{clip.name} (VRCF Flattened)";
                return clone;
            }).ToList();

            var firstClipByWeightParam = new Dictionary<string, int>();

            for (var i = 0; i < children.Length; i++) {
                var child = children[i];
                if (!(child.motion is AnimationClip clip)) continue;
                var param = child.directBlendParameter;
                if (!firstClipByWeightParam.TryGetValue(param, out var firstClipI)) {
                    firstClipByWeightParam[param] = i;
                    continue;
                }

                var changed = false;
                clones[i].Rewrite(AnimationRewriter.RewriteCurve((binding, curve) => {
                    if (curve.IsFloat && curve.FloatCurve.keys.Length == 1 &&
                        curve.FloatCurve.keys[0].time == 0) {
                        motions[firstClipI] = clones[firstClipI];
                        var firstClip = clones[firstClipI];
                        var firstClipCurve = firstClip.GetFloatCurve(binding);
                        if (firstClipCurve != null) {
                            // Merge curve into first clip's
                            firstClipCurve.keys = firstClipCurve.keys.Select(key => {
                                key.value += curve.FloatCurve.keys[0].value;
                                return key;
                            }).ToArray();
                            firstClip.SetCurve(binding, firstClipCurve);
                        } else {
                            // Just shove it into the first clip
                            firstClip.SetCurve(binding, curve);
                        }

                        changed = true;
                        return (binding, null, true);
                    }

                    return (binding, curve, false);
                }));

                if (changed) motions[i] = clones[i];
            }

            var j = 0;
            tree.RewriteChildren(child => {
                child.motion = motions[j++];
                if (child.motion is AnimationClip clip && !clip.GetAllBindings().Any()) {
                    return new ChildMotion[] { };
                }
                return new ChildMotion[] { child };
            });
        }
    }
}
