using System;
using System.Linq;
using JetBrains.Annotations;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using VF.Builder;
using VF.Feature.Base;
using VF.Injector;
using VF.Utils;

namespace VF.Service {
    [VFService]
    internal class DirectBlendTreeFlatteningService {
        [VFAutowired] private readonly AvatarManager manager;
        [VFAutowired] private readonly ClipFactoryService clipFactory;
        
        [FeatureBuilderAction(FeatureOrder.FlattenDbts)]
        public void Optimize() {
            var fx = manager.GetFx();
            foreach (var state in new AnimatorIterator.States().From(fx.GetRaw())) {
                if (state.motion is BlendTree tree) {
                    Optimize(tree);
                }
            }
        }

        private void Optimize(BlendTree tree) {
            if (!tree.IsStatic()) {
                //Debug.LogError("Something added a non-static clip to a VRCF DBT. This is likely a bug.");
                return;
            }
            MakeZeroLength(tree);
            FlattenTrees(tree);
            FlattenClips(tree);
        }
        
        private void MakeZeroLength(Motion motion) {
            if (motion is AnimationClip clip) {
                clip.Rewrite(AnimationRewriter.RewriteCurve((binding, curve) => {
                    if (curve.lengthInSeconds == 0) return (binding, curve, false);
                    return (binding, curve.GetLast(), true);
                }));
                if (!clip.GetAllBindings().Any()) {
                    clip.SetFloatCurve(
                        EditorCurveBinding.FloatCurve("__ignored", typeof(GameObject), "m_IsActive"),
                        AnimationCurve.Constant(0, 0, 0)
                    );
                }
            } else {
                foreach (var tree in new AnimatorIterator.Trees().From(motion)) {
                    tree.RewriteChildren(child => {
                        if (child.motion == null) {
                            child.motion = clipFactory.NewClip("Empty");
                        }
                        return child;
                    });
                }
                foreach (var c in new AnimatorIterator.Clips().From(motion)) {
                    MakeZeroLength(c);
                }
            }
        }

        private void FlattenTrees([CanBeNull] Motion motion) {
            if (!(motion is BlendTree tree)) return;
            foreach (var child in tree.children) {
                FlattenTrees(child.motion);
            }
            if (tree.blendType != BlendTreeType.Direct) return;
            tree.children = tree.children.SelectMany(child => {
                if (child.directBlendParameter == manager.GetFx().One() &&
                    child.motion is BlendTree childTree && childTree.blendType == BlendTreeType.Direct) {
                    return childTree.children;
                }
                return new ChildMotion[] { child };
            }).ToArray();
        }

        private void FlattenClips([CanBeNull] Motion motion) {
            if (!(motion is BlendTree tree)) return;
            foreach (var child in tree.children) {
                FlattenClips(child.motion);
            }
            if (tree.blendType != BlendTreeType.Direct) return;

            bool IsAlwaysOnClip(ChildMotion child) =>
                child.directBlendParameter == manager.GetFx().One()
                && child.motion is AnimationClip clip;

            var hasMultipleAlwaysOnClips = tree.children.Where(IsAlwaysOnClip).Count() > 1;
            if (hasMultipleAlwaysOnClips) {
                AnimationClip onClip = null;
                tree.children = tree.children.SelectMany(child => {
                    if (IsAlwaysOnClip(child) && child.motion is AnimationClip clip) {
                        if (onClip == null) {
                            onClip = clipFactory.NewClip("Flattened");
                            onClip.CopyFrom(clip);
                            child.motion = onClip;
                            return new ChildMotion[] { child };
                        } else {
                            onClip.CopyFrom(clip);
                            return new ChildMotion[] { };
                        }
                    }

                    return new ChildMotion[] { child };
                }).ToArray();
            }
        }
    }
}
