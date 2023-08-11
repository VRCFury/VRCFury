using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using VF.Builder;
using VF.Feature.Base;
using VF.Utils;

namespace VF.Feature {
    /**
     * States/trees without a motion set, and "Empty" clips (those with no bindings), can break WD off.
     * We replace them all with a fake, short 1-second-long clip.
     * (1 second long because that's how long a state lasts in unity if no motion is set)
     */
    public class FixEmptyMotionBuilder : FeatureBuilder {
        [FeatureBuilderAction(FeatureOrder.FixEmptyMotions)]
        public void Apply() {
            foreach (var controller in manager.GetAllUsedControllers()) {
                var noopClip = controller.NewClip("noop");
                // If this is 0 frames (1 second) instead of 1 frame (1/60th of a second), it breaks gogoloco float
                noopClip.SetFloatCurve(
                    EditorCurveBinding.FloatCurve("_vrcf_noop", typeof(GameObject), "m_IsActive"),
                    AnimationCurve.Constant(0, 0, 0) // 0 frames = 1 second long because unity
                );
                foreach (var state in new AnimatorIterator.States().From(controller.GetRaw())) {
                    CheckState(state, noopClip);
                }
            }
        }

        private void CheckState(AnimatorState state, AnimationClip noopClip) {
            if (state.writeDefaultValues) {
                // Interestingly, inserting noop clips into WD on states HAS SIDE EFFECTS for some reason
                // so... don't do that. (Doing so breaks the rex eye pupil animations, because it seemingly
                // doesn't properly propegate higher layer states while transitioning from a noop clip into
                // a clip with content)
                return;
            }
            if (state.motion == null) {
                state.motion = noopClip;
                return;
            }
            foreach (var tree in new AnimatorIterator.Trees().From(state)) {
                tree.children = tree.children.Select(child => {
                    child.motion = child.motion == null ? noopClip : child.motion;
                    return child;
                }).ToArray();
            }
        }
    }
}
