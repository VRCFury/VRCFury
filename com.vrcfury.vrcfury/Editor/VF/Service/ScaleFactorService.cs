using System;
using System.Collections.Generic;
using JetBrains.Annotations;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.Animations;
using VF.Builder;
using VF.Feature;
using VF.Injector;
using VF.Inspector;
using VF.Utils;
using VF.Utils.Controller;
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
    internal class ScaleFactorService {
        [VFAutowired] private readonly AvatarManager manager;
        [VFAutowired] private readonly MathService math;
        [VFAutowired] private readonly ForceStateInAnimatorService forceStateInAnimatorService;
        private ControllerManager fx => manager.GetFx();
        [VFAutowired] private readonly OverlappingContactsFixService overlappingService;

        private int scaleIndex = 0;

        /**
         * localSpace and worldSpace MUST be at identical positions
         */
        [CanBeNull]
        public VFAFloat Get(VFGameObject localSpace, VFGameObject worldSpace) {
            if (!BuildTargetUtils.IsDesktop()) {
                return null;
            }

            var localContactObj = GameObjects.Create("Scale Detector (Sender)", localSpace);
            localContactObj.worldScale = Vector3.one;
            var localContact = localContactObj.AddComponent<VRCContactSender>();
            localContact.radius = 0.001f;
            var tag = $"VRCF_SCALEFACTORFIX_AA_{scaleIndex++}";
            localContact.collisionTags.Add(tag);

            var worldContactObj = GameObjects.Create("Scale Detector (Receiver)", worldSpace);
            overlappingService.Activate();
            var worldContact = worldContactObj.AddComponent<VRCContactReceiver>();
            worldContact.allowOthers = false;
            worldContact.receiverType = ContactReceiver.ReceiverType.Proximity;
            worldContact.collisionTags.Add(tag);
            worldContact.radius = 0.1f;
            worldContact.position = new Vector3(0.1f, 0, 0);
            var receiverParam = fx.NewFloat($"SFFix {localSpace.name} - Rcv");
            worldContact.parameter = receiverParam;

            var final = math.Multiply($"SFFix {localSpace.name} - Final", receiverParam, 100 * localSpace.worldScale.x);
            return final;
        }
    }
}
