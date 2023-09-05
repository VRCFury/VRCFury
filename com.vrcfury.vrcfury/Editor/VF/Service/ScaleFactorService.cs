using System;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using VF.Builder;
using VF.Feature;
using VF.Injector;
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
        
        private VFAFloat cached;

        public VFAFloat Get() {
            if (cached != null) return cached;
            
            var fx = manager.GetFx();

            var holder = GameObjects.Create("vrcfScaleFactor", manager.AvatarObject);
            holder.active = false;
            forceStateInAnimatorService.ForceEnableLocal(holder);
            var scale = holder.localScale;
            scale.x = scale.y = scale.z = 0.01f / holder.worldScale.z;
            holder.localScale = scale;
            
            var rand = new System.Random();
            var tag = $"VRCF_SF_{rand.Next(100_000_000, 999_999_999)}";

            var senderObj = GameObjects.Create("Sender", holder);
            var receiverObj = GameObjects.Create("Receiver", holder);

            var sender = senderObj.AddComponent<VRCContactSender>();
            sender.radius = 1;
            sender.collisionTags.Add(tag);

            var scaleFactor = fx.NewFloat("ScaleFactor", usePrefix: false, def: 1);
            var scaleFactorInt = fx.NewInt("ScaleFactorInt", synced: true);
            var scaleFactorIntFloat = fx.NewFloat("ScaleFactorIntFl");
            var scaleFactorFract = fx.NewFloat("ScaleFactorFract", synced: true);
            var scaleFactorOut = fx.NewFloat("ScaleFactorOut");

            var receiver = receiverObj.AddComponent<VRCContactReceiver>();
            receiver.position = new Vector3(2, 0, 0);
            receiver.radius = 1;
            receiver.collisionTags.Add(tag);
            receiver.localOnly = true;
            receiver.allowOthers = false;
            receiver.parameter = scaleFactorFract.Name();
            receiver.receiverType = ContactReceiver.ReceiverType.Proximity;

            var intLayer = fx.NewLayer("ScaleFactor Int Extraction");
            var intState = intLayer.NewState("Extract ScaleFactor int");
            var bState = intLayer.NewState("b");
            intState.TransitionsTo(bState).When(fx.Always());
            bState.TransitionsTo(intState).When(fx.Always());
            var driver = intState.GetRaw().VAddStateMachineBehaviour<VRCAvatarParameterDriver>();
            driver.localOnly = true;
            driver.parameters.Add(new VRC_AvatarParameterDriver.Parameter() {
                type = VRC_AvatarParameterDriver.ChangeType.Copy,
                name = scaleFactorInt.Name(),
                source = scaleFactor.Name()
            });
            var driver2 = intState.GetRaw().VAddStateMachineBehaviour<VRCAvatarParameterDriver>();
            driver2.parameters.Add(new VRC_AvatarParameterDriver.Parameter() {
                type = VRC_AvatarParameterDriver.ChangeType.Copy,
                name = scaleFactorIntFloat.Name(),
                source = scaleFactorInt.Name()
            });

            var layer = fx.NewLayer($"ScaleFactor Fraction Extraction");
            var tree = fx.NewBlendTree($"Extract ScaleFactor Fraction");
            tree.blendType = BlendTreeType.Direct;
            layer.NewState("Drive").WithAnimation(tree);
            {
                var maxClip = fx.NewClip($"Sender_one");
                var binding = EditorCurveBinding.FloatCurve(senderObj.GetPath(manager.AvatarObject), typeof(Transform),
                    $"m_LocalPosition.x");
                maxClip.SetConstant(binding, 1f);
                tree.AddDirectChild(scaleFactor.Name(), maxClip);
            }
            {
                var maxClip = fx.NewClip($"Receiver_one");
                var binding = EditorCurveBinding.FloatCurve(receiverObj.GetPath(manager.AvatarObject), typeof(Transform),
                    $"m_LocalPosition.x");
                maxClip.SetConstant(binding, 1f);
                tree.AddDirectChild(scaleFactorIntFloat.Name(), maxClip);
            }
            {
                var addOne = fx.NewClip($"Output_one");
                var binding = EditorCurveBinding.FloatCurve("", typeof(Animator), scaleFactorOut.Name());
                addOne.SetConstant(binding, 1f);
                tree.AddDirectChild(scaleFactorIntFloat.Name(), addOne);
                tree.AddDirectChild(scaleFactorFract.Name(), addOne);
            }

            cached = scaleFactorOut;
            return scaleFactorOut;
        }
    }
}
