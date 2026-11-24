#if VRCSDK_HAS_FACE_TRACKING
namespace VF.Tests {
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;
    using NUnit.Framework;
    using UnityEditor;
    using UnityEngine;
    using UnityEngine.TestTools;
    using UnityEngine.UIElements;
    using VF.Feature;
    using VF.Builder;
    using VF.Feature.Base;
    using VF.Inspector;
    using VF.Model;
    using VF.Model.Feature;
    using VF.Model.StateAction;
    using VF.Service;
    using VF.Utils;
    using VF.Utils.Controller;
    using VRC.SDK3.Avatars.Components;
    using FtBehaviourControl = VRC.SDKBase.VRC_AnimatorFaceTrackingBlendshapesSet.BlendSet;
    using Toggle = VF.Model.Feature.Toggle;

    internal class FuryFullControllerTest : FaceTrackingTestBase {
        [Test]
        public void PublicApiAddsFaceTrackingConfiguration() {
            var obj = new GameObject("FullControllerApi");
            try {
                var skin = obj.AddComponent<SkinnedMeshRenderer>();
                var set = FaceTrackingUtils.CreateBlendshapeSet("Face");
                var behavior = new VRCAvatarDescriptor.FaceTrackingParameterBehavior();
                var api = com.vrcfury.api.FuryComponents.CreateFullController(obj);
                api.AddFaceTrackingRenderer(skin);
                api.AddFaceTrackingBlendshapeSet(set);
                api.AddFaceTrackingBehavior(behavior);
                var model = (FullController)obj.GetComponent<VRCFury>().content;
                Assert.That(model.faceTrackingMeshes.Single().useBone, Is.False);
                Assert.That(model.faceTrackingMeshes.Single().componentOverride, Is.SameAs(skin));
                Assert.That(model.faceTrackingBlendshapeSets.Single(), Is.SameAs(set));
                Assert.That(model.faceTrackingParameterBehaviors.Single(), Is.SameAs(behavior));
            } finally {
                Object.DestroyImmediate(obj);
            }
        }
    }
}
#endif
