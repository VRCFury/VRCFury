using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine.UIElements;
using VF.Builder;
using VF.Feature.Base;
using VF.Injector;
using VF.Inspector;
using VF.Model.Feature;
using VF.Service;
using VF.Utils.Controller;
using VRC.SDK3.Avatars.Components;

namespace VF.Feature {

public class BlinkingBuilder : FeatureBuilder<Blinking> {
    [VFAutowired] private readonly ActionClipService actionClipService;
    [VFAutowired] private readonly TrackingConflictResolverBuilder trackingConflictResolverBuilder;

    [FeatureBuilderAction]
    public void Apply() {
        var avatar = manager.Avatar;
        avatar.customEyeLookSettings.eyelidType = VRCAvatarDescriptor.EyelidType.None;

        var fx = GetFx();
        var blinkTriggerSynced = fx.NewBool("BlinkTriggerSynced", synced: true);

        // Generator
        {
            var blinkCounter = fx.NewInt("BlinkCounter");
            var layer = fx.NewLayer("Blink - Generator");
            var remote = layer.NewState("Remote Trap");
            var entry = layer.NewState("Entry");
            var idle = layer.NewState("Idle");
            var subtract = layer.NewState("Subtract");
            var trigger0 = layer.NewState("Trigger 0").Move(subtract, 1, 0);
            var trigger1 = layer.NewState("Trigger 1").Move(trigger0, 1, 0);
            var randomize = layer.NewState("Randomize").Move(idle, 1, 0);

            remote.TransitionsTo(entry).When(fx.IsLocal().IsTrue());
            entry.TransitionsTo(idle).When(fx.Always());

            idle.TransitionsTo(trigger0).When(blinkCounter.IsLessThan(1).And(blinkTriggerSynced.IsTrue()));
            trigger0.Drives(blinkTriggerSynced, false);
            trigger0.TransitionsTo(randomize).When(fx.Always());

            idle.TransitionsTo(trigger1).When(blinkCounter.IsLessThan(1).And(blinkTriggerSynced.IsFalse()));
            trigger1.Drives(blinkTriggerSynced, true);
            trigger1.TransitionsTo(randomize).When(fx.Always());

            randomize.DrivesRandom(blinkCounter, 2, 10);
            randomize.TransitionsTo(idle).When(fx.Always());

            idle.TransitionsTo(subtract).WithTransitionDurationSeconds(1f).When(fx.Always());
            subtract.DrivesDelta(blinkCounter, -1);
            subtract.TransitionsTo(idle).When(fx.Always());
        }

        // Receiver / Animator
        {
            var blinkClip = actionClipService.LoadState("blink", model.state);
            var blinkDuration = model.transitionTime >= 0 ? model.transitionTime : 0.07f;
            var holdDuration = model.holdTime >= 0 ? model.holdTime : 0;
            var layer = fx.NewLayer("Blink - Receiver");
            var idle = layer.NewState("Idle");
            var waitFalse = layer.NewState("Waiting (false)");
            var waitTrue = layer.NewState("Waiting (true)").Move(waitFalse, 1, 0);
            var checkActive = layer.NewState("Check Active").Move(waitFalse, 0, 1);
            var blinkStart = layer.NewState("Blink Start").Move(checkActive, 1, 0);
            var blink = layer.NewState("Blink").WithAnimation(blinkClip).Move(blinkStart, 1, 0);

            idle.TransitionsTo(waitFalse).When(blinkTriggerSynced.IsFalse());
            idle.TransitionsTo(waitTrue).When(blinkTriggerSynced.IsTrue());
            waitFalse.TransitionsTo(checkActive).When(blinkTriggerSynced.IsTrue());
            waitTrue.TransitionsTo(checkActive).When(blinkTriggerSynced.IsFalse());

            trackingConflictResolverBuilder.WhenCollected(() => {
                foreach (var inhibitorParam in trackingConflictResolverBuilder.GetInhibitors(TrackingConflictResolverBuilder.TrackingEyes)) {
                    checkActive.TransitionsTo(idle).When(inhibitorParam.IsGreaterThan(0));
                }
                checkActive.TransitionsTo(blinkStart).When(fx.Always());
            });
            blinkStart.TransitionsTo(blink).WithTransitionDurationSeconds(blinkDuration).When(fx.Always());
            if (holdDuration > 0) {
                var hold = layer.NewState("Hold").WithAnimation(blinkClip).Move(blink, 1, 0);
                blink.TransitionsTo(hold).WithTransitionDurationSeconds(holdDuration).When(fx.Always());
                hold.TransitionsTo(idle).WithTransitionDurationSeconds(blinkDuration).When(fx.Always());
            } else {
                blink.TransitionsTo(idle).WithTransitionDurationSeconds(blinkDuration).When(fx.Always());
            }
        }
    }

    public override string GetEditorTitle() {
        return "Blink Controller";
    }

    public override VisualElement CreateEditor(SerializedProperty prop) {
        var c = new VisualElement();
        c.Add(VRCFuryEditorUtils.Info(
            "This feature will manage eye-blinking for your avatar. Note this will disable 'Eyelid Type' on the VRC avatar descriptor."));
        c.Add(VRCFuryEditorUtils.Prop(
            prop.FindPropertyRelative("state"),
            "Blinking State"
        ));
        var adv = new Foldout {
            text = "Advanced",
            value = false
        };
        adv.Add(VRCFuryEditorUtils.Prop(
            prop.FindPropertyRelative("transitionTime"),
            "Transition Time (in seconds, -1 will use VRCFury recommended value)"));
        adv.Add(VRCFuryEditorUtils.Prop(
            prop.FindPropertyRelative("holdTime"),
            "Hold Time - Time eyelids will remain closed (in seconds, -1 will use VRCFury recommended value)"));
        c.Add(adv);
        return c;
    }
    
    public override bool AvailableOnRootOnly() {
        return true;
    }
}

}
