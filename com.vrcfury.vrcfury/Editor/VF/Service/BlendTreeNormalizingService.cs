using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using VF.Builder;
using VF.Feature.Base;
using VF.Injector;
using VF.Utils;

namespace VF.Service {
    /**
     * Blendtrees do weird things when they are missing children, have empty animations, or are longer than 0 frames.
     * This service ensures that all of the blendtrees we create are valid in all of these respects.
     */
    [VFService]
    internal class BlendTreeNormalizingService {
        [VFAutowired] private readonly ControllersService controllers;
        private ControllerManager fx => controllers.GetFx();
        [VFAutowired] private readonly ClipFactoryTrackingService clipFactoryTracking;
        [VFAutowired] private readonly ClipFactoryService clipFactory;

        [FeatureBuilderAction(FeatureOrder.NormalizeBlendTrees)]
        public void Optimize() {
            foreach (var state in new AnimatorIterator.States().From(fx.GetRaw())) {
                if (state.motion is BlendTree tree && clipFactoryTracking.Created(tree)) {
                    MakeZeroLength(tree);
                }
            }
        }
        
        private void MakeZeroLength(Motion motion) {
            if (motion is AnimationClip clip) {
                clip.Rewrite(AnimationRewriter.RewriteCurve((binding, curve) => {
                    if (curve.lengthInSeconds == 0) return (binding, curve, false);
                    return (binding, curve.GetLast(), true);
                }));
                if (!clip.GetAllBindings().Any()) {
                    clip.SetCurve(
                        "__ignored",
                        typeof(GameObject),
                        "m_IsActive",
                        0
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
    }
}
