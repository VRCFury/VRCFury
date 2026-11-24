#if VRCSDK_HAS_CONTACT_PARAMETER_FLAGS
using NUnit.Framework;
using UnityEngine;
using VF.Utils;
using VRC.Dynamics;
using VRC.SDK3.Dynamics.Contact.Components;

namespace VF.Tests {
    public class FixPendingContactReceiverHookTest {
        [Test]
        public void ReceiverEnabledAfterSchedulingJoinsTheNextFrame() {
            var previous = ContactManager.Inst;
            var managerObject = new GameObject("Manager");
            var obj = new GameObject("Receiver");
            var manager = managerObject.AddComponent<ContactManager>();
            try {
                ContactManager.Inst = manager;
                manager.Init();
                void Schedule() {
                    manager.ScheduleUpdateReceiversJob(default).Complete();
                    typeof(CollisionScene).VFMethod("Complete").Invoke(manager.collision, null);
                }
                void Finish() => typeof(ContactManager).VFMethod("HandleDynamicsFrameComplete").Invoke(manager, null);

                Schedule();
                var receiver = obj.AddComponent<VRCContactReceiver>();
                receiver.receiverType = ContactReceiver.ReceiverType.Proximity;
                receiver.allowInit = true;
                typeof(ContactReceiver).VFMethod("Start").Invoke(receiver, null);
                Assert.That(receiver.manager, Is.SameAs(manager));
                Finish();
                Schedule();
                Assert.That(receiver.receiverId, Is.Not.EqualTo(CollisionScene.Shape.NullId));
                Finish();
            } finally {
                Object.DestroyImmediate(obj);
                manager.Dispose();
                Object.DestroyImmediate(managerObject);
                ContactManager.Inst = previous;
            }
        }
    }
}
#endif
