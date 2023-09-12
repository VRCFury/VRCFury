using System;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.Animations;
using VF.Builder;
using VF.Feature;
using VF.Injector;
using VF.Inspector;
using VF.Utils;
using VRC.Dynamics;
using VRC.SDK3.Avatars.Components;
using VRC.SDK3.Dynamics.Contact.Components;
using VRC.SDKBase;

namespace VF.Service {
    /**
     * "Fixes" https://feedback.vrchat.com/bug-reports/p/scalefactor-is-not-synchronized-to-late-joiners-or-existing-players-in-newly-joi
     * by using local ScaleFactor to move a contact receiver, then syncing that contact receiver's proximity value
     */
    [VFService]
    public class ScaleFactorService {
        [VFAutowired] private readonly AvatarManager manager;
        [VFAutowired] private readonly ParamSmoothingService smoothingService;
        [VFAutowired] private readonly ForceStateInAnimatorService forceStateInAnimatorService;
        [VFAutowired] private readonly ClipBuilderService clipBuilder;
        
        private VFAFloat cached;
        public VFAFloat Get() {
            if (cached == null) cached = Generate();
            return cached;
        }

        private VFAFloat Generate() {
            var fx = manager.GetFx();

            var holder = GameObjects.Create("vrcf_ScaleFactorFix", manager.AvatarObject);
            var senderObj = GameObjects.Create("Sender", holder);
            var sender = senderObj.AddComponent<VRCContactSender>();
            sender.radius = 0.001f / senderObj.worldScale.x;
            var tag = "VRCF_SCALEFACTORFIX_AA";
            sender.collisionTags.Add(tag);

            var receiverObj = GameObjects.Create("Receiver", holder);
            var receiver = receiverObj.AddComponent<VRCContactReceiver>();
            receiver.allowOthers = false;
            receiver.receiverType = ContactReceiver.ReceiverType.Proximity;
            receiver.collisionTags.Add(tag);
            receiver.radius = 0.1f;
            receiver.position = new Vector3(0.1f, 0, 0);
            var receiverParam = fx.NewFloat("SFFix_Rcv");
            receiver.parameter = receiverParam.Name();
            var p = receiverObj.AddComponent<ScaleConstraint>();
            p.AddSource(new ConstraintSource() {
                sourceTransform = VRCFuryEditorUtils.GetResource<Transform>("world.prefab"),
                weight = 1
            });
            p.weight = 1;
            p.constraintActive = true;
            p.locked = true;

            var layer = fx.NewLayer("ScaleFactorFix");

            var off = layer.NewState("Off");
            var offClip = fx.NewClip("ScaleFactorFix Off");
            clipBuilder.Enable(offClip, receiverObj, false);
            off.WithAnimation(offClip);
            
            var off2 = layer.NewState("Off2");
            off.TransitionsTo(off2).When(fx.Always()).WithTransitionDurationSeconds(0.5f);
            off2.WithAnimation(offClip);
            
            var on = layer.NewState("On");
            off2.TransitionsTo(on).When(fx.Always());

            var on2 = layer.NewState("On2");
            on.TransitionsTo(on2).When(fx.Always()).WithTransitionDurationSeconds(0.5f);

            var read = layer.NewState("Read");
            on2.TransitionsTo(read).When(fx.Always());
            var readParam = fx.NewFloat("SFFix_Read");
            var readDriver = read.GetRaw().VAddStateMachineBehaviour<VRCAvatarParameterDriver>();
            readDriver.parameters.Add(new VRC_AvatarParameterDriver.Parameter() {
                type = VRC_AvatarParameterDriver.ChangeType.Copy,
                name = readParam.Name(),
                source = receiverParam.Name(),
                convertRange = true,
                sourceMin = 0,
                sourceMax = 1,
                destMin = 0,
                destMax = 100
            });

            var copy = layer.NewState("Copy");
            // Because sometimes vrc doesn't turn on the receiver fast enough and it still reads 0
            read.TransitionsTo(copy).When(readParam.IsGreaterThan(0.001f));
            read.TransitionsTo(off).When(fx.Always());
            copy.TransitionsTo(off).When(fx.Always());
            var finalParam = fx.NewFloat("SFFix_Final");
            var finalDriver = copy.GetRaw().VAddStateMachineBehaviour<VRCAvatarParameterDriver>();
            finalDriver.parameters.Add(new VRC_AvatarParameterDriver.Parameter() {
                type = VRC_AvatarParameterDriver.ChangeType.Copy,
                name = finalParam.Name(),
                source = readParam.Name(),
            });

            return finalParam;
        }
    }
}
