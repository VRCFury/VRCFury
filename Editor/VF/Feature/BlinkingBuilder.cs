using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;
using VF.Feature.Base;
using VF.Inspector;
using VF.Model.Feature;

namespace VF.Feature {

public class BlinkingBuilder : FeatureBuilder<Blinking> {
    [FeatureBuilderAction]
    public void Apply() {
        if (!StateExists(model.state)) return;

        var blinkTriggerSynced = manager.NewBool("BlinkTriggerSynced", synced: true);
        var blinkTrigger = manager.NewTrigger("BlinkTrigger");
        var blinkActive = manager.NewBool("BlinkActive", def: true);

        // Generator
        {
            var blinkCounter = manager.NewInt("BlinkCounter");
            var layer = manager.NewLayer("Blink - Generator");
            var entry = layer.NewState("Entry");
            var remote = layer.NewState("Remote").Move(entry, 0, -1);
            var idle = layer.NewState("Idle").Move(entry, 0, 1);
            var subtract = layer.NewState("Subtract");
            var trigger0 = layer.NewState("Trigger 0").Move(subtract, 1, 0);
            var trigger1 = layer.NewState("Trigger 1").Move(trigger0, 1, 0);
            var randomize = layer.NewState("Randomize").Move(idle, 1, 0);

            entry.TransitionsTo(remote).When(IsLocal().IsFalse());
            entry.TransitionsTo(idle).When(Always());

            idle.TransitionsTo(trigger0).When(blinkCounter.IsLessThan(1).And(blinkTriggerSynced.IsTrue()));
            trigger0.Drives(blinkTriggerSynced, false);
            trigger0.TransitionsTo(randomize).When(Always());

            idle.TransitionsTo(trigger1).When(blinkCounter.IsLessThan(1).And(blinkTriggerSynced.IsFalse()));
            trigger1.Drives(blinkTriggerSynced, true);
            trigger1.TransitionsTo(randomize).When(Always());

            randomize.DrivesRandom(blinkCounter, 2, 10);
            randomize.TransitionsTo(idle).When(Always());

            idle.TransitionsTo(subtract).WithTransitionDurationSeconds(1f).When(Always());
            subtract.DrivesDelta(blinkCounter, -1);
            subtract.TransitionsTo(idle).When(Always());
        }

        // Receiver
        {
            var layer = manager.NewLayer("Blink - Receiver");
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
            var layer = manager.NewLayer("Blink - Animate");
            var idle = layer.NewState("Idle");
            var checkActive = layer.NewState("Check Active");
            var blink = layer.NewState("Blink").WithAnimation(blinkClip).Move(checkActive, 1, 0);

            idle.TransitionsTo(checkActive).When(blinkTrigger.IsTrue());
            checkActive.TransitionsTo(blink).WithTransitionDurationSeconds(blinkDuration).When(blinkActive.IsTrue());
            checkActive.TransitionsTo(idle).When(Always());
            if (holdDuration > 0) {
                var hold = layer.NewState("Hold").WithAnimation(blinkClip);
                blink.TransitionsTo(hold).WithTransitionDurationSeconds(holdDuration).When(Always());
                hold.TransitionsTo(idle).WithTransitionDurationSeconds(blinkDuration).When(Always());
            } else {
                blink.TransitionsTo(idle).WithTransitionDurationSeconds(blinkDuration).When(Always());
            }
        }
    }

    public override string GetEditorTitle() {
        return "Blink Controller";
    }

    public override VisualElement CreateEditor(SerializedProperty prop) {
        var c = new VisualElement();
        c.Add(VRCFuryStateEditor.render(prop.FindPropertyRelative("state")));
        var adv = new Foldout {
            text = "Advanced",
            value = false
        };
        adv.Add(new PropertyField(prop.FindPropertyRelative("transitionTime"), "Transition Time (s)"));
        adv.Add(new Label("-1 will use VRCFury recommended value"));
        adv.Add(new PropertyField(prop.FindPropertyRelative("holdTime"), "Hold Time (s)"));
        adv.Add(new Label("Time eyelids will remain closed, -1 will use VRCFury recommended value"));
        c.Add(adv);
        return c;
    }
    
    public override bool AvailableOnProps() {
        return false;
    }
}

}
