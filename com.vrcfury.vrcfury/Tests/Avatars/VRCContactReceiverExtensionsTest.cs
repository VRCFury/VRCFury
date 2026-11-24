namespace VF.Tests {
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

    internal class VRCContactReceiverExtensionsTest : ContactReceiverTestBase {
        [Test]
        public void AdvancedOutputsIncludeOnlyNonemptyTagsAndKeepTheirNames() {
            var obj = new GameObject("Contact");
            try {
                var receiver = obj.AddComponent<VRCContactReceiver>();
                receiver.parameter = "Contact";
                receiver.collisionTags = new List<string> { "Test_Tag", "", " ", null };
                Assert.That(receiver.GetParameterNames(), Is.EqualTo(new[] { "Contact" }));
                EnableAdvanced(receiver);
                Assert.That(receiver.GetParameterNames(),
                    Is.EquivalentTo(Outputs.Select(suffix => "Contact" + suffix)));
                receiver.collisionTags = null;
                Assert.That(receiver.GetParameterNames().Count(), Is.EqualTo(Outputs.Length - 1));
                receiver.parameter = "";
                Assert.That(receiver.GetParameterNames(), Is.Empty);
            } finally {
                Object.DestroyImmediate(obj);
            }
        }

#if VRCSDK_HAS_CONTACT_PARAMETER_FLAGS
        [TestCase(AdvancedParameter.None, "")]
        [TestCase(AdvancedParameter.SenderType, "_SenderType")]
        [TestCase(AdvancedParameter.SenderLocalPosition, "_SenderLocalPositionX,_SenderLocalPositionY,_SenderLocalPositionZ")]
        [TestCase(AdvancedParameter.SenderLossyScale, "_SenderLossyScaleX,_SenderLossyScaleY,_SenderLossyScaleZ")]
        [TestCase(AdvancedParameter.SenderVelocity, "_SenderVelocityX,_SenderVelocityY,_SenderVelocityZ,_SenderVelocityMagnitude")]
        [TestCase(AdvancedParameter.PointSeparation, "_PointSeparation")]
        [TestCase(AdvancedParameter.ProximitySpeed, "_ProximitySpeed")]
        [TestCase(AdvancedParameter.CollisionTags, "_CollisionTag_Test_Tag")]
        public void OnlySelectedAdvancedOutputsAreIncluded(AdvancedParameter flags, string suffixes) {
            var obj = new GameObject("Contact");
            try {
                var receiver = obj.AddComponent<VRCContactReceiver>();
                receiver.parameter = "Contact";
                receiver.collisionTags = new List<string> { "Test_Tag" };
                receiver.advancedParameters = flags;
                Assert.That(receiver.GetParameterNames(), Is.EquivalentTo(
                    new[] { "Contact" }.Concat(suffixes.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                        .Select(suffix => "Contact" + suffix))));
            } finally {
                Object.DestroyImmediate(obj);
            }
        }
#endif
    }
}
