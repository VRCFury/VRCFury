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
        [VFAutowired] private readonly ControllersService controllers;
        private ControllerManager fx => controllers.GetFx();
        [VFAutowired] private readonly VFGameObject avatarObject;
        [VFAutowired] private readonly DbtLayerService directTreeService;
        [VFAutowired] private readonly ClipFactoryService clipFactory;

        private bool activate = false;

        public void Activate() {
            activate = true;
        }
        
        [FeatureBuilderAction(FeatureOrder.ForceStateInAnimator)]
        public void Fix() {
            if (!activate) return;

            var allOffClip = clipFactory.NewClip("AllReceiversOff");
            foreach (var r in avatarObject.GetComponentsInSelfAndChildren<VRCContactReceiver>()) {
                allOffClip.SetEnabled(r, false);
            }

            var directTree = directTreeService.Create();
            var blendtreeMath = directTreeService.GetMath(directTree);
            var counter = fx.MakeAap("counter");

            var counterSetToZero = counter.MakeSetter(0);
            var counterAddOne = VFBlendTreeDirect.Create("addToCounter");
            var counterEqualsOne = counter.MakeSetter(1);
            counterAddOne.Add(fx.One(), counterEqualsOne);
            counterAddOne.Add(counter, counterEqualsOne);

            var scaleFactor = fx.NewFloat("ScaleFactor", usePrefix: false);
            var scaleFactorBuffered = blendtreeMath.Buffer(scaleFactor);
            var scaleFactorDiff = blendtreeMath.Add("ScaleFactorDiff", (scaleFactor, 1), (scaleFactorBuffered, -1));

            directTree.Add(
                BlendtreeMath.Not(BlendtreeMath.Equals(scaleFactorDiff, 0f))
                .create(counterSetToZero, counterAddOne)
            );

            directTree.Add(BlendtreeMath.LessThan(counter, 10).create(allOffClip, null));
        }
    }
}
