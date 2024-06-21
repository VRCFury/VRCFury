using VF.Builder;
using VF.Feature.Base;
using VF.Injector;
using VF.Utils;
using VRC.Dynamics;
using VRC.SDK3.Dynamics.Contact.Components;

namespace VF.Service {
    [VFService]
    internal class OverlappingContactsFixService {
        [VFAutowired] private readonly AvatarManager manager;
        private VFGameObject avatarObject => manager.AvatarObject;
        [VFAutowired] private readonly DirectBlendTreeService directTree;
        [VFAutowired] private readonly MathService math;
        [VFAutowired] private readonly ClipFactoryService clipFactory;
        [VFAutowired] private readonly ClipBuilderService clipBuilder;
        
        [FeatureBuilderAction(FeatureOrder.FixTouchingContacts)]
        public void Fix() {
            var holder = GameObjects.Create("OverlappingContactsFix", avatarObject);
            var senderObj = GameObjects.Create("Sender", holder);
            var sender = senderObj.AddComponent<VRCContactSender>();
            sender.collisionTags.Add("__vrcf_overlapping_contact_fix");
            sender.radius = 0.1f / senderObj.worldScale.x;
            var receiverObj = GameObjects.Create("Receiver", holder);
            var receiver = receiverObj.AddComponent<VRCContactReceiver>();
            receiver.collisionTags.Add("__vrcf_overlapping_contact_fix");
            receiver.allowOthers = false;
            receiver.radius = 0.1f / receiverObj.worldScale.x;
            receiver.receiverType = ContactReceiver.ReceiverType.Proximity;
            var param = manager.GetFx().NewFloat("OverlappingContactFix");
            receiver.parameter = param;

            var allOffClip = clipFactory.NewClip("AllReceiversOff");
            foreach (var r in avatarObject.GetComponentsInSelfAndChildren<VRCContactReceiver>()) {
                clipBuilder.Enable(allOffClip, r.owner(), false);
            }

            var isOff = math.MakeAap("IfOff");
            allOffClip.SetAap(isOff.Name(), 1);
            var b1 = math.Buffer(isOff);
            var b2 = math.Buffer(b1);

            directTree.Add(math.And(math.LessThan(param, 0.1f), math.LessThan(b2, 0.1f)).create(allOffClip, null));
        }
    }
}
