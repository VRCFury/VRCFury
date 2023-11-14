using System.Linq;
using UnityEngine;
using VF.Builder;
using VF.Feature;
using VF.Injector;
using VF.Utils;
using VF.Utils.Controller;
using VRC.SDK3.Avatars.Components;
using VRC.SDKBase;

namespace VF.Service {
    [VFService]
    public class FullBodyEmoteService {
        [VFAutowired] private readonly AvatarManager manager;
        [VFAutowired] private readonly DriveOtherTypesFromFloatService driveOtherTypesFromFloatService;
        
        private VFLayer layer;
        private VFState idle;
        public VFAFloat AddClip(AnimationClip clip) {
            var action = manager.GetController(VRCAvatarDescriptor.AnimLayerType.Action);
            if (layer == null) {
                layer = action.NewLayer("VRCFury Actions");
                layer.mask = AvatarMaskExtensions.Empty();
                layer.mask.AllowAllMuscles();
                idle = layer.NewState("Idle");
            }

            clip = MutableManager.CopyRecursive(clip, false);
            var nonActionBindings = clip.GetAllBindings()
                .Where(b => b.GetMuscleBindingType() == EditorCurveBindingExtensions.MuscleBindingType.None);
            clip.SetCurves(nonActionBindings.Select(b => (b,(FloatOrObjectCurve)null)));
            var state = layer.NewState(clip.name).WithAnimation(clip);

            var myParam = action.NewBool(clip.name+" (Action)");
            var myCond = myParam.IsTrue();
            state.TransitionsFromEntry().When(myCond);
            idle.TransitionsToExit().When(myCond);

            var outState = layer.NewState($"{clip.name} - Out");
            state.TransitionsTo(outState).WithTransitionDurationSeconds(1000).Interruptable().When(myCond.Not());
            outState.TransitionsToExit().When(action.Always());

            var weightOn = state.GetRaw().VAddStateMachineBehaviour<VRCPlayableLayerControl>();
            weightOn.layer = VRC_PlayableLayerControl.BlendableLayer.Action;
            weightOn.goalWeight = 1;
            var weightOff = outState.GetRaw().VAddStateMachineBehaviour<VRCPlayableLayerControl>();
            weightOff.layer = VRC_PlayableLayerControl.BlendableLayer.Action;
            weightOff.goalWeight = 0;
            
            var animOn = state.GetRaw().VAddStateMachineBehaviour<VRCAnimatorTrackingControl>();
            var animOff = outState.GetRaw().VAddStateMachineBehaviour<VRCAnimatorTrackingControl>();
            foreach (var type in TrackingConflictResolverBuilder.allTypes) {
                if (type.fieldName == "trackingEyes" || type.fieldName == "trackingMouth") {
                    continue;
                }
                type.SetValue(animOn, VRC_AnimatorTrackingControl.TrackingType.Animation);
                type.SetValue(animOff, VRC_AnimatorTrackingControl.TrackingType.Tracking);
            }

            var fx = manager.GetFx();
            var enableParam = fx.NewFloat(clip.name + " (Trigger)");
            driveOtherTypesFromFloatService.Drive(enableParam, myParam, 0, 1);
            return enableParam;
        }
    }
}
