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
     * If a receiver is off when the avatar loads, but is immediately animated on (by a saved toggle or after changing avatar scale),
     * the receiver parameter will be 0 until the overlap is cleared and reset. This service fixes that issue by detecting the error
     * condition and resetting all receivers on the avatar.
     */
    [VFService]
    internal class OverlappingContactsFixService {
        [VFAutowired] private readonly AvatarManager manager;
        private VFGameObject avatarObject => manager.AvatarObject;
        [VFAutowired] private readonly DirectBlendTreeService directTree;
        [VFAutowired] private readonly MathService math;
        [VFAutowired] private readonly ClipFactoryService clipFactory;
        [VFAutowired] private readonly ClipBuilderService clipBuilder;
        [VFAutowired] private readonly ScaleFactorService scaleFactorService;

        private bool activate = false;

        public void Activate() {
            activate = true;
        }
        
        [FeatureBuilderAction(FeatureOrder.FixTouchingContacts)]
        public void Fix() {
            if (!activate) return;

            var testObject = GameObjects.Create("OverlappingContactsFix", avatarObject);
            testObject.active = false;
            testObject.worldScale = Vector3.one;

            var sender = testObject.AddComponent<VRCContactSender>();
            sender.radius = 0.01f;
            sender.collisionTags.Add("__vrcf_overlappingcontactsfix");
            sender.shapeType = ContactBase.ShapeType.Sphere;

            var receiver = testObject.AddComponent<VRCContactReceiver>();
            receiver.radius = 0.01f;
            receiver.collisionTags.Add("__vrcf_overlappingcontactsfix");
            receiver.shapeType = ContactBase.ShapeType.Sphere;
            receiver.receiverType = ContactReceiver.ReceiverType.Proximity;
            var param = manager.GetFx().NewFloat("overlappingcontactsfix");
            receiver.parameter = param;
            
            var testObjectOn = clipFactory.NewClip("TestObjectOn");
            clipBuilder.Enable(testObjectOn, testObject);
            directTree.Add(testObjectOn);

            var allOffClip = clipFactory.NewClip("AllReceiversOff");
            foreach (var r in avatarObject.GetComponentsInSelfAndChildren<VRCContactReceiver>()) {
                allOffClip.SetCurve(EditorCurveBinding.FloatCurve(r.owner().GetPath(avatarObject), r.GetType(), "m_Enabled"), 0);
            }

            var counter = math.MakeAap("counter");

            var counterAddOne = clipFactory.NewDBT("addToCounter");
            var counterEqualsOne = clipFactory.NewClip("counter=1");
            counterEqualsOne.SetAap(counter.Name(), 1);
            counterAddOne.Add(manager.GetFx().One(), counterEqualsOne);
            counterAddOne.Add(counter.Name(), counterEqualsOne);

            directTree.Add(math.And(math.LessThan(param, 0.5f), math.GreaterThan(counter, 20)).create(
                math.MakeSetter(counter, 0), counterAddOne));

            directTree.Add(math.LessThan(counter, 10).create(allOffClip, null));
        }
    }
}
