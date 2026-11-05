using UnityEditor;
using System.Linq;
using UnityEngine.UIElements;
using UnityEngine;
using VF.Feature.Base;
using VF.Injector;
using VF.Inspector;
using VF.Model.Feature;
using VF.Model;
using VF.Model.StateAction;
using VF.Service;
using VF.Utils;
using VF.Utils.Controller;
using VRC.SDK3.Avatars.ScriptableObjects;

namespace VF.Feature {

    [FeatureTitle("Blink Controller")]
    internal class BlinkingBuilder : FeatureBuilder<Blinking> {
        [VFAutowired] private readonly ActionClipService actionClipService;
        [VFAutowired] private readonly TrackingConflictResolverService trackingConflictResolverService;
        [VFAutowired] private readonly ControllersService controllers;
        [VFAutowired] private readonly VFGameObject avatarObject;
        [VFAutowired] private readonly RemoveLegacyFaceTrackingService legacyFaceTracking;
        private VFAvatarController fx => controllers.GetFx();
        private VFAvatarController actionController => controllers.GetAction();

        [FeatureBuilderAction]
        public void Apply() {
            var state = GetBlinkState();
            if (state.actions.Count == 0) return;

            var blinkTriggerSynced = fx.CreateUniqueBool("BlinkTriggerSynced");
            blinkTriggerSynced.MakeExpressionBool();
            blinkTriggerSynced.CopyTo(actionController);

            // Generator
            {
                var blinkCounter = actionController.CreateUniqueInt("BlinkCounter");
                var layer = actionController.NewLayer("Blink - Generator");
                var remote = layer.NewState("Remote Trap");
                var entry = layer.NewState("Entry");
                var idle = layer.NewState("Idle");
                var subtract = layer.NewState("Subtract");
                var trigger0 = layer.NewState("Trigger 0").Move(subtract, 1, 0);
                var trigger1 = layer.NewState("Trigger 1").Move(trigger0, 1, 0);
                var randomize = layer.NewState("Randomize").Move(idle, 1, 0);

                remote.TransitionsTo(entry).When(actionController.IsLocal().IsTrue());
                entry.TransitionsTo(idle).When(actionController.Always());

                idle.TransitionsTo(trigger0).When(blinkCounter.IsLessThan(1).And(blinkTriggerSynced.IsTrue()));
                trigger0.Drives(blinkTriggerSynced, false);
                trigger0.TransitionsTo(randomize).When(actionController.Always());

                idle.TransitionsTo(trigger1).When(blinkCounter.IsLessThan(1).And(blinkTriggerSynced.IsFalse()));
                trigger1.Drives(blinkTriggerSynced, true);
                trigger1.TransitionsTo(randomize).When(actionController.Always());

                randomize.DrivesRandom(blinkCounter, 2, 10);
                randomize.TransitionsTo(idle).When(actionController.Always());

                idle.TransitionsTo(subtract).WithTransitionDurationSeconds(1f).When(actionController.Always());
                subtract.DrivesDelta(blinkCounter, -1);
                subtract.TransitionsTo(idle).When(actionController.Always());
            }

            // Receiver / Animator
            {
                var blinkClip = actionClipService.LoadState("blink", state);
                var blinkDuration = model.transitionTime >= 0 ? model.transitionTime : 0.07f;
                var holdDuration = model.holdTime >= 0 ? model.holdTime : 0;
                var layer = fx.NewLayer("Blink - Receiver");
                var idle = layer.NewState("Idle");
                var waitFalse = layer.NewState("Waiting (false)");
                var waitTrue = layer.NewState("Waiting (true)").Move(waitFalse, 1, 0);
                var checkActive = layer.NewState("Check Active").Move(waitFalse, 0, 1);
                var blinkStart = layer.NewState("Blink Start").Move(checkActive, 1, 0);

                idle.TransitionsTo(waitFalse).When(blinkTriggerSynced.IsFalse());
                idle.TransitionsTo(waitTrue).When(blinkTriggerSynced.IsTrue());
                waitFalse.TransitionsTo(checkActive).When(blinkTriggerSynced.IsTrue());
                waitTrue.TransitionsTo(checkActive).When(blinkTriggerSynced.IsFalse());

                trackingConflictResolverService.WhenCollected(() => {
                    if (!layer.Exists()) return; // Deleted by empty layer builder
                    foreach (var inhibitorParam in trackingConflictResolverService.GetInhibitors(TrackingConflictResolverService.TrackingEyes)) {
                        checkActive.TransitionsTo(idle).When(inhibitorParam.IsGreaterThan(0));
                    }
                    checkActive.TransitionsTo(blinkStart).When(fx.Always());
                });

                if (blinkClip.IsStatic()) {
                    var blink = layer.NewState("Blink").WithAnimation(blinkClip).Move(blinkStart, 1, 0);
                    blinkStart.TransitionsTo(blink).WithTransitionDurationSeconds(blinkDuration).When(fx.Always());
                    if (holdDuration > 0) {
                        var hold = layer.NewState("Hold").WithAnimation(blinkClip).Move(blink, 1, 0);
                        blink.TransitionsTo(hold).WithTransitionDurationSeconds(holdDuration).When(fx.Always());
                        hold.TransitionsTo(idle).WithTransitionDurationSeconds(blinkDuration).When(fx.Always());
                    } else {
                        blink.TransitionsTo(idle).WithTransitionDurationSeconds(blinkDuration).When(fx.Always());
                    }
                } else {
                    blinkStart
                        .WithAnimation(blinkClip)
                        .TransitionsTo(idle)
                        .WithTransitionExitTime(1)
                        .When(fx.Always());
                }
            }
        }

        internal State GetBlinkState() {
            var state = new State();
            if (model.state == null) return state;
            foreach (var action in model.state.actions) {
                if (action is BlendShapeAction shape) {
                    if (shape.allRenderers) {
                        if (avatarObject.GetComponentsInSelfAndChildren<SkinnedMeshRenderer>()
                            .Any(skin => legacyFaceTracking.GetHandled(skin).eyelids && skin.HasBlendshape(shape.blendShape))) continue;
                    } else if (legacyFaceTracking.GetHandled(shape.renderer).eyelids) continue;
                }
                if (action != null) state.actions.Add(action);
            }
            return state;
        }

        [FeatureEditor]
        public static VisualElement Editor(SerializedProperty prop) {
            var c = new VisualElement();
            c.Add(VRCFuryEditorUtils.Info(
                "This feature will add Blinking to your avatar. You can use this in place of blinking in the VRC Avatar Descriptor, or on a prop to add blinking to an additional object."));
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
    }

}
