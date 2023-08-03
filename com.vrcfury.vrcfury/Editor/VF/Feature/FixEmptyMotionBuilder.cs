using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using VF.Builder;
using VF.Feature.Base;
using VF.Utils;
using VRC.SDK3.Avatars.Components;

namespace VF.Feature {
    /**
     * "Empty" clips (those with no bindings) last 1 second for no reason, and also break WD off.
     * We replace them all with a fake, short 1-binding clip.
     */
    public class FixEmptyMotionBuilder : FeatureBuilder {
        [FeatureBuilderAction(FeatureOrder.FixEmptyMotions)]
        public void Apply() {
            foreach (var controller in manager.GetAllUsedControllers()) {
                var noopClip = controller.NewClip("noop");
                // If this is 0 frames (1 second) instead of 1 frame (1/60th of a second), it breaks gogoloco float
                noopClip.SetFloatCurve(
                    EditorCurveBinding.FloatCurve("_vrcf_noop", typeof(GameObject), "m_IsActive"),
                    AnimationCurve.Constant(0, 1/noopClip.frameRate, 0)
                );
                foreach (var state in new AnimatorIterator.States().From(controller.GetRaw())) {
                    CheckState(state, noopClip);
                }
            }
        }

        private void CheckState(AnimatorState state, AnimationClip noopClip) {
            if (IsNoop(state.motion)) {
                state.motion = noopClip;
                return;
            }
            foreach (var tree in new AnimatorIterator.Trees().From(state)) {
                tree.children = tree.children.Select(child => {
                    child.motion = IsNoop(child.motion) ? noopClip : child.motion;
                    return child;
                }).ToArray();
            }
        }
        
        private bool IsNoop(Motion motion) {
            return motion == null || ClipBuilder.IsEmptyMotion(motion, avatarObject);
        }
    }
}
