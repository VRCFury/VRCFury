using System;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using VF.Builder;
using VF.Feature;
using VF.Injector;
using VF.Utils;
using VRC.Dynamics;
using VRC.SDK3.Dynamics.Contact.Components;

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
            senderObj.localPosition = new Vector3(0, 0, 0);

            var scaleFactorReceived = fx.NewFloat("ScaleFactorReceived", synced: true);
            var receiver = receiverObj.AddComponent<VRCContactReceiver>();
            receiver.radius = 1;
            receiver.collisionTags.Add(tag);
            receiver.localOnly = true;
            receiver.allowOthers = false;
            receiver.parameter = scaleFactorReceived.Name();
            receiver.receiverType = ContactReceiver.ReceiverType.Proximity;
            receiverObj.localPosition = new Vector3(2, 0, 0);

            var zeroClip = fx.NewClip($"ScaleFactor_zero");
            var maxClip = fx.NewClip($"ScaleFactor_max");
            var path = senderObj.GetPath(manager.AvatarObject);
            var binding = EditorCurveBinding.FloatCurve(path, typeof(Transform), $"m_LocalPosition.x");
            zeroClip.SetConstant(binding, 0);
            maxClip.SetConstant(binding, 0.01f); // When ScaleFactor is 100, it'll max out the receiver
            var layer = fx.NewLayer($"Move ScaleFactor contact");
            var tree = fx.NewBlendTree($"Move ScaleFactor contact");
            tree.blendType = BlendTreeType.Direct;
            layer.NewState("Drive").WithAnimation(tree);
            tree.AddDirectChild(fx.One().Name(), zeroClip);
            tree.AddDirectChild(fx.NewFloat("ScaleFactor", usePrefix: false, def: 1).Name(), maxClip);

            var scaleFactor = smoothingService.Map("ScaleFactor_Remapped", scaleFactorReceived, 0, 1, 0, 100);
            cached = scaleFactor;
            return scaleFactor;
        }
    }
}