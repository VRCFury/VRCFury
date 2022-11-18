using System.Linq;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;
using VF.Builder;
using VF.Feature.Base;
using VF.Inspector;
using VF.Model.Feature;
using VRC.SDK3.Avatars.Components;

namespace VF.Feature {

public class BlinkingBuilder : FeatureBuilder<Blinking> {
    /** Adding this feature to the build will disable blinking when the param is true */
    [NoBuilder]
    public class BlinkingPrevention : FeatureModel {
        public VFABool param;
    }

    [FeatureBuilderAction(FeatureOrder.Blinking)]
    public void Apply() {
        if (!StateExists(model.state)) return;

        var avatar = avatarObject.GetComponent<VRCAvatarDescriptor>();
        avatar.customEyeLookSettings.eyelidType = VRCAvatarDescriptor.EyelidType.None;

        var fx = GetFx();
        var blinkTriggerSynced = fx.NewBool("BlinkTriggerSynced", synced: true);
        var blinkTrigger = fx.NewTrigger("BlinkTrigger");

        // Generator
        {
            var blinkCounter = fx.NewInt("BlinkCounter");
            var layer = fx.NewLayer("Blink - Generator");
            var entry = layer.NewState("Entry");
            var remote = layer.NewState("Remote").Move(entry, 0, -1);
            var idle = layer.NewState("Idle").Move(entry, 0, 1);
            var subtract = layer.NewState("Subtract");
            var trigger0 = layer.NewState("Trigger 0").Move(subtract, 1, 0);
            var trigger1 = layer.NewState("Trigger 1").Move(trigger0, 1, 0);
            var randomize = layer.NewState("Randomize").Move(idle, 1, 0);

            entry.TransitionsTo(remote).When(fx.IsLocal().IsFalse());
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

        // Receiver
        {
            var layer = fx.NewLayer("Blink - Receiver");
            var blink0 = layer.NewState("Trigger == false");
            var blink1 = layer.NewState("Trigger == true");

            blink0.TransitionsTo(blink1).When(blinkTriggerSynced.IsTrue());
            blink0.Drives(blinkTrigger, true);
            blink1.TransitionsTo(blink0).When(blinkTriggerSynced.IsFalse());
            blink1.Drives(blinkTrigger, true);
        }

        // Animator
        {
            var blinkClip = LoadState("blink", model.state);
            var blinkDuration = model.transitionTime >= 0 ? model.transitionTime : 0.07f;
            var holdDuration = model.holdTime >= 0 ? model.holdTime : 0;
            var layer = fx.NewLayer("Blink - Animate");
            var idle = layer.NewState("Idle");
            var checkActive = layer.NewState("Check Active");
            var blinkStart = layer.NewState("Blink Start").Move(checkActive, 1, 0);
            var blink = layer.NewState("Blink").WithAnimation(blinkClip).Move(blinkStart, 0, -1);

            idle.TransitionsTo(checkActive).When(blinkTrigger.IsTrue());
            foreach (var prevention in allFeaturesInRun.Select(f => f as BlinkingPrevention).Where(f => f != null)) {
                checkActive.TransitionsTo(idle).When(prevention.param.IsTrue());
            }
            checkActive.TransitionsTo(blinkStart).When(fx.Always());
            blinkStart.TransitionsTo(blink).WithTransitionDurationSeconds(blinkDuration).When(fx.Always());
            if (holdDuration > 0) {
                var hold = layer.NewState("Hold").WithAnimation(blinkClip);
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
        c.Add(VRCFuryEditorUtils.WrappedLabel("Blinking state:"));
        c.Add(VRCFuryEditorUtils.Prop(prop.FindPropertyRelative("state")));
        var adv = new Foldout {
            text = "Advanced",
            value = false
        };
        adv.Add(VRCFuryEditorUtils.Prop(prop.FindPropertyRelative("transitionTime"), "Transition Time (s)"));
        adv.Add(VRCFuryEditorUtils.WrappedLabel("-1 will use VRCFury recommended value"));
        adv.Add(VRCFuryEditorUtils.Prop(prop.FindPropertyRelative("holdTime"), "Hold Time (s)"));
        adv.Add(VRCFuryEditorUtils.WrappedLabel("Time eyelids will remain closed, -1 will use VRCFury recommended value"));
        c.Add(adv);
        return c;
    }
    
    public override bool AvailableOnProps() {
        return false;
    }
}

}
