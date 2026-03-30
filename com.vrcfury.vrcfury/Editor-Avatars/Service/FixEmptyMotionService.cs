using System.Linq;
using UnityEditor.Animations;
using UnityEngine;
using VF.Builder;
using VF.Feature.Base;
using VF.Injector;
using VF.Utils;
using VF.Utils.Controller;
using VRC.SDK3.Avatars.Components;

namespace VF.Service {
    /**
     * States/trees without a motion set, and "Empty" clips (those with no bindings), can break WD off.
     * We replace them all with a fake, short 1-second-long clip.
     * (1 second long because that's how long a state lasts in unity if no motion is set)
     */
    [VFService]
    internal class FixEmptyMotionService {
        [VFAutowired] private readonly ClipFactoryService clipFactory;
        [VFAutowired] private readonly ControllersService controllers;

        [FeatureBuilderAction(FeatureOrder.FixEmptyMotions)]
        public void Apply() {
            foreach (var controller in controllers.GetAllUsedControllers()) {
                var noopClip = clipFactory.NewClip("noop");
                foreach (var layer in controller.GetLayers()) {
                    foreach (var state in new AnimatorIterator.States().From(layer)) {
                        CheckState(controller, layer, state, noopClip);
                    }
                }
            }
        }

        private void CheckState(ControllerManager controller, VFLayer layer, AnimatorState state, AnimationClip noopClip) {
            // ReSharper disable once ReplaceWithSingleAssignment.True
            var replaceNulls = true;
            if (state.writeDefaultValues) {
                // Interestingly, inserting noop clips into WD on states HAS SIDE EFFECTS for some reason
                // so... don't do that. (Doing so breaks the rex eye pupil animations, because it seemingly
                // doesn't properly propagate higher layer states while transitioning from a noop clip into
                // a clip with content)
                replaceNulls = false;
            }
            if (controller.GetType() != VRCAvatarDescriptor.AnimLayerType.FX) {
                // The same also seems to happen in layers with muscle animations, so we also skip this on all controllers that aren't FX
                replaceNulls = false;
            }

            if (state.motion == null && replaceNulls) {
                state.motion = noopClip;
            }
        }
    }
}
