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
using VF.Utils.Controller;
using VRC.SDK3.Avatars.Components;

namespace VF.Service {
    [VFService]
    internal class TreeFlatteningService {
        [VFAutowired] private readonly ControllersService controllers;
        private ControllerManager fx => controllers.GetFx();
        [VFAutowired] private readonly ParamsService paramsService;
        private ParamManager paramz => paramsService.GetParams();
        
        [FeatureBuilderAction(FeatureOrder.TreeFlattening)]
        public void Optimize() {
            var alwaysOneParams = GetAlwaysOneParams();
            foreach (var state in new AnimatorIterator.States().From(fx.GetRaw()).Where(VFLayer.Created)) {
                if (state.motion is BlendTree tree) {
                    Optimize(tree, alwaysOneParams);
                    if (tree.blendType == BlendTreeType.Direct
                        && tree.children.Length == 1
                        && alwaysOneParams.Contains(tree.children[0].directBlendParameter)) {
                        state.motion = tree.children[0].motion;
                    }
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
                .Append(VFBlendTreeDirect.AlwaysOneParam)
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
            MergeClipsWithSameWeight(tree, alwaysOneParams);
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
                    child.timeScale = childTree.children[0].timeScale;
                    child.mirror = childTree.children[0].mirror;
                    child.cycleOffset = childTree.children[0].cycleOffset;
                }
                return new ChildMotion[] { child };
            });
        }

        private void MergeClipsWithSameWeight(BlendTree tree, ISet<string> alwaysOneParams) {
            if (tree.blendType != BlendTreeType.Direct) return;
            if (tree.GetNormalizedBlendValues()) return;

            var firstClipByWeightParam = new Dictionary<string, AnimationClip>();
            tree.RewriteChildren(child => {
                if (!(child.motion is AnimationClip clip)) return new [] { child };
                // Don't merge if we're using an original clip from the user
                if (clip.GetUseOriginalUserClip()) return new [] { child };

                var param = child.directBlendParameter;
                if (alwaysOneParams.Contains(param)) param = VFBlendTreeDirect.AlwaysOneParam;
                if (!firstClipByWeightParam.TryGetValue(param, out var firstClip)) {
                    var clone = clip.Clone();
                    child.motion = clone;
                    firstClipByWeightParam[param] = clone;
                    return new [] { child };
                }

                // Eliminating a non-zero length clip has side effects on the playable length of the tree,
                // so don't merge those.
                if (clip.GetLengthInSeconds() != 0) {
                    return new [] { child };
                }

                // If the two clips share any bindings, don't merge them since they
                // would normally be additive
                if (firstClip.GetAllBindings().Intersect(clip.GetAllBindings()).Any()) {
                    return new [] { child };
                }

                firstClip.SetCurves(clip.GetAllCurves());
                firstClip.name += " + " + clip.name;
                return new ChildMotion[] { };
            });
        }
    }
}
