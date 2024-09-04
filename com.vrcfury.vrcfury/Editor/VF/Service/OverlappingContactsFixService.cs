using UnityEditor;
using UnityEngine;
using UnityEngine.Animations;
using VF.Builder;
using VF.Feature.Base;
using VF.Injector;
using VF.Inspector;
using VF.Utils;
using VRC.Dynamics;
using VRC.SDK3.Dynamics.Contact.Components;

namespace VF.Service {
    /**
     * This attempts to resolve this issue:
     * https://feedback.vrchat.com/feature-requests/p/overlapping-on-load-receivers-are-still-broken
     *
     * It does so by disabling all avatar receivers for the first 10 frames after load-in or re-scaling.
     */
    [VFService]
    internal class OverlappingContactsFixService {
        [VFAutowired] private readonly AvatarManager manager;
        private VFGameObject avatarObject => manager.AvatarObject;
        [VFAutowired] private readonly DirectBlendTreeService directTree;
        [VFAutowired] private readonly MathService math;
        [VFAutowired] private readonly ClipFactoryService clipFactory;

        private bool activate = false;

        public void Activate() {
            activate = true;
        }
        
        [FeatureBuilderAction(FeatureOrder.FixTouchingContacts)]
        public void Fix() {
            if (!activate) return;

            var allOffClip = clipFactory.NewClip("AllReceiversOff");
            foreach (var r in avatarObject.GetComponentsInSelfAndChildren<VRCContactReceiver>()) {
                allOffClip.SetCurve(r, "m_Enabled", 0);
            }

            var counter = math.MakeAap("counter");

            var counterSetToZero = math.MakeSetter(counter, 0);
            var counterAddOne = clipFactory.NewDBT("addToCounter");
            var counterEqualsOne = clipFactory.NewClip("counter=1");
            counterEqualsOne.SetAap(counter, 1);
            counterAddOne.Add(manager.GetFx().One(), counterEqualsOne);
            counterAddOne.Add(counter, counterEqualsOne);

            var scaleFactor = manager.GetFx().NewFloat("ScaleFactor", usePrefix: false);
            var scaleFactorBuffered = math.Buffer(scaleFactor);
            var scaleFactorDiff = math.Add("ScaleFactorDiff", (scaleFactor, 1), (scaleFactorBuffered, -1));

            directTree.Add(
                math.Not(math.Equals(scaleFactorDiff, 0f))
                .create(counterSetToZero, counterAddOne)
            );

            directTree.Add(math.LessThan(counter, 10).create(allOffClip, null));
        }
    }
}
