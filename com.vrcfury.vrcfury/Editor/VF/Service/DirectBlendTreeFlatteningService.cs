using System;
using System.Linq;
using JetBrains.Annotations;
using UnityEditor.Animations;
using UnityEngine;
using VF.Builder;
using VF.Feature.Base;
using VF.Injector;
using VF.Utils;

namespace VF.Service {
    [VFService]
    public class DirectBlendTreeFlatteningService {
        [VFAutowired] private readonly AvatarManager manager;
        
        [FeatureBuilderAction(FeatureOrder.FlattenDbts)]
        public void Optimize() {
            foreach (var c in manager.GetAllUsedControllers()) {
                foreach (var state in new AnimatorIterator.States().From(c.GetRaw())) {
                    if (state.motion is BlendTree tree) {
                        Optimize(tree);
                    }
                }
            }
        }

        private void Optimize(BlendTree tree) {
            if (!tree.IsStatic()) {
                Debug.LogError("Something added a non-static clip to a VRCF DBT. This is likely a bug.");
                return;
            }
            tree.MakeZeroLength();
            FlattenTrees(tree);
            FlattenClips(tree);
        }

        private void FlattenTrees([CanBeNull] Motion motion) {
            if (!(motion is BlendTree tree)) return;
            foreach (var child in tree.children) {
                FlattenTrees(child.motion);
            }
            if (tree.blendType != BlendTreeType.Direct) return;
            tree.children = tree.children.SelectMany(child => {
                if (child.directBlendParameter == manager.GetFx().One().Name() &&
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
            AnimationClip onClip = null;
            tree.children = tree.children.SelectMany(child => {
                if (child.directBlendParameter == manager.GetFx().One().Name() &&
                    child.motion is AnimationClip clip) {
                    if (onClip == null) {
                        onClip = new AnimationClip();
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
