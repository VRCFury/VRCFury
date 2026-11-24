using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using VF.Feature;
using VF.Model.Feature;
using VF.Service;
using VF.Utils;
using VRC.SDK3.Avatars.Components;
using VRC.SDK3.Avatars.ScriptableObjects;
using VRC.SDK3.Dynamics.Contact.Components;
#if VRCSDK_HAS_CONTACT_PARAMETER_FLAGS
using AdvancedParameter = VRC.Dynamics.ContactReceiver.AdvancedParameter;
#endif
using Object = UnityEngine.Object;

namespace VF.Tests {
    internal abstract class ContactReceiverTestBase {
        protected static readonly string[] Outputs = {
            "", "_SenderType", "_SenderLocalPositionX", "_SenderLocalPositionY", "_SenderLocalPositionZ",
            "_SenderVelocityX", "_SenderVelocityY", "_SenderVelocityZ", "_SenderVelocityMagnitude",
            "_ProximitySpeed", "_CollisionTag_Test_Tag"
#if VRCSDK_HAS_CONTACT_PARAMETER_FLAGS
            , "_SenderLossyScaleX", "_SenderLossyScaleY", "_SenderLossyScaleZ", "_PointSeparation"
#endif
        };

        protected static void Inject(object target, string field, object value) =>
            target.GetType().VFField(field).SetValue(target, value);

        protected static void EnableAdvanced(VRCContactReceiver receiver) {
#if VRCSDK_HAS_CONTACT_PARAMETER_FLAGS
            receiver.advancedParameters = VRC.Dynamics.ContactReceiver.AdvancedParameter.All;
#else
            Assert.Ignore("This SDK predates advanced contact parameters.");
#endif
        }
    }
}
