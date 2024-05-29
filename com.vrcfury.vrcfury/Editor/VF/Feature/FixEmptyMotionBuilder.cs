using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using VF.Builder;
using VF.Feature.Base;
using VF.Utils;
using VF.Utils.Controller;
using VRC.SDK3.Avatars.Components;

namespace VF.Feature {
    /**
     * States/trees without a motion set, and "Empty" clips (those with no bindings), can break WD off.
     * We replace them all with a fake, short 1-second-long clip.
     * (1 second long because that's how long a state lasts in unity if no motion is set)
     */
    internal class FixEmptyMotionBuilder : FeatureBuilder {
        [FeatureBuilderAction(FeatureOrder.FixEmptyMotions)]
        public void Apply() {
            foreach (var controller in manager.GetAllUsedControllers()) {
                var noopClip = controller.NewClip("noop");
                // If this is 0 frames (1 second) instead of 1 frame (1/60th of a second), it breaks gogoloco float
                noopClip.SetFloatCurve(
                    EditorCurveBinding.FloatCurve("_vrcf_noop", typeof(GameObject), "m_IsActive"),
                    AnimationCurve.Constant(0, 0, 0) // 0 frames = 1 second long because unity
                );
                foreach (var layer in controller.GetLayers()) {
                    foreach (var state in new AnimatorIterator.States().From(layer)) {
                        CheckState(controller, layer, state, noopClip);
                    }
                }
            }
        }

        private void CheckState(ControllerManager controller, VFLayer layer, AnimatorState state, AnimationClip noopClip) {
            if (state.writeDefaultValues) {
                // Interestingly, inserting noop clips into WD on states HAS SIDE EFFECTS for some reason
                // so... don't do that. (Doing so breaks the rex eye pupil animations, because it seemingly
                // doesn't properly propagate higher layer states while transitioning from a noop clip into
                // a clip with content)
                return;
            }
            if (controller.GetType() != VRCAvatarDescriptor.AnimLayerType.FX) {
                // The same also seems to happen in layers with muscle animations, so we also skip this on all controllers that aren't FX
                return;
            }
            if (state.motion == null) {
                Debug.LogWarning($"Replacing empty motion in {layer.name}.{state.name}");
                state.motion = noopClip;
                return;
            }
            foreach (var tree in new AnimatorIterator.Trees().From(state)) {
                tree.RewriteChildren(child => {
                    if (child.motion == null) {
                        Debug.LogWarning($"Replacing empty blendtree motion in {layer.name}.{state.name}");
                        child.motion = noopClip;
                    }
                    return child;
                });
            }
        }
    }
}
