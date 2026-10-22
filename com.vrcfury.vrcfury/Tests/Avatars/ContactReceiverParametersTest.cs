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
using VRC.SDK3.Dynamics.Contact.Components;
using Object = UnityEngine.Object;

namespace VF.Tests {
    [Category("VRCFury")]
    public class ContactReceiverParametersTest {
        private static readonly string[] Outputs = {
            "", "_SenderType", "_SenderLocalPositionX", "_SenderLocalPositionY", "_SenderLocalPositionZ",
            "_SenderVelocityX", "_SenderVelocityY", "_SenderVelocityZ", "_SenderVelocityMagnitude",
            "_ProximitySpeed", "_CollisionTag_Test_Tag"
        };

        private static void Inject(object target, string field, object value) =>
            target.GetType().VFField(field).SetValue(target, value);

        private static void EnableAdvanced(VRCContactReceiver receiver) {
#if VRCSDK_HAS_ADVANCED_CONTACT_PARAMETERS
            receiver.advancedProximityParameters = true;
#else
            Assert.Ignore("This SDK predates advanced contact parameters.");
#endif
        }

        [TestCaseSource(nameof(Outputs))]
        public void FullControllerRenamesReceiverWhenOnlyOneOutputIsUsed(string suffix) {
            var root = new GameObject("Avatar");
            try {
                var avatar = root.AddComponent<VRCAvatarDescriptor>();
                avatar.baseAnimationLayers = Array.Empty<VRCAvatarDescriptor.CustomAnimLayer>();
                avatar.specialAnimationLayers = Array.Empty<VRCAvatarDescriptor.CustomAnimLayer>();
                var receiver = root.AddComponent<VRCContactReceiver>();
                receiver.parameter = "Contact";
                receiver.collisionTags = new List<string> { "Test_Tag" };
                if (suffix != "") EnableAdvanced(receiver);

                var globals = new GlobalsService { avatarObject = root, currentFeatureNum = 7 };
                var parameters = new ParamsService();
                Inject(parameters, "avatar", avatar);
                var controllers = new ControllersService();
                Inject(controllers, "avatar", avatar);
                Inject(controllers, "globals", globals);
                Inject(controllers, "paramsService", parameters);
                Inject(controllers, "parameterSourceService", new ParameterSourceService());
                var builder = new FullControllerBuilder { featureBaseObject = root, model = new FullController() };
                Inject(builder, "globals", globals);
                Inject(builder, "controllers", controllers);
                Inject(builder, "animatorLayerControlManager", new AnimatorLayerControlOffsetService());
                var rewrite = builder.GetType().VFMethod("RewriteParamName");
                var output = (string)rewrite.Invoke(builder, new object[] { "Contact" + suffix });
                builder.Apply();
                Assert.That(receiver.parameter, Is.Not.EqualTo("Contact"));
                Assert.That(output, Is.EqualTo(receiver.parameter + suffix));
            } finally {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void AdvancedOutputsIncludeOnlyNonemptyTagsAndKeepTheirNames() {
            var obj = new GameObject("Contact");
            try {
                var receiver = obj.AddComponent<VRCContactReceiver>();
                receiver.parameter = "Contact";
                receiver.collisionTags = new List<string> { "Test_Tag", "", " ", null };
                Assert.That(ContactReceiverUtils.GetParameterNames(receiver), Is.EqualTo(new[] { "Contact" }));
                EnableAdvanced(receiver);
                Assert.That(ContactReceiverUtils.GetParameterNames(receiver),
                    Is.EquivalentTo(Outputs.Select(suffix => "Contact" + suffix)));
                receiver.collisionTags = null;
                Assert.That(ContactReceiverUtils.GetParameterNames(receiver).Count(), Is.EqualTo(10));
                receiver.parameter = "";
                Assert.That(ContactReceiverUtils.GetParameterNames(receiver), Is.Empty);
            } finally {
                Object.DestroyImmediate(obj);
            }
        }
    }
}
