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

    internal class BlinkingBuilderTest : FaceTrackingTestBase {
        [TestCase(false, false)]
        [TestCase(false, true)]
        [TestCase(true, false)]
        [TestCase(true, true)]
        public void BlinkControllerDropsActionsTargetingFtCoveredRenderers(bool allRenderers, bool eyelids) {
            using (var build = new Build()) {
                var covered = build.Skin("Covered");
                var other = build.Skin("Other");
                var settings = FaceTrackingUtils.CreateBlendshapeSet("Covered");
                SetMapping(settings, eyelids ? (int)VRCAvatarDescriptor.FaceTrackingKey.EyeClosedLeft : 1,
                    "Smile", visemes: !eyelids);
                build.globals.allFeaturesInRun.Add(new FullController {
                    faceTrackingMeshes = { (ObjectReference<SkinnedMeshRenderer>)covered },
                    faceTrackingBlendshapeSets = { settings }
                });
                var service = build.RemoveBuiltIns();
                Assert.That(service.GetHandled(covered).eyelids, Is.EqualTo(eyelids));
                Assert.That(service.GetHandled(covered).visemes, Is.EqualTo(!eyelids));
                Assert.That(service.GetHandled(other), Is.EqualTo((false, false)));
                var action = new BlendShapeAction {
                    renderer = covered, allRenderers = allRenderers, blendShape = "Smile",
                    blendShapeValue = 70, localOnly = true
                };
                var builder = new BlinkingBuilder { model = new Blinking { state = new State { actions = { action } } } };
                typeof(BlinkingBuilder).VFField("avatarObject").SetValue(builder, (VFGameObject)build.root);
                typeof(BlinkingBuilder).VFField("legacyFaceTracking").SetValue(builder, service);
                var state = builder.GetBlinkState();
                var shapes = state.actions.Cast<BlendShapeAction>().ToArray();
                Assert.That(shapes, Is.EqualTo(eyelids ? System.Array.Empty<BlendShapeAction>() : new[] { action }));
                Assert.That(action.allRenderers, Is.EqualTo(allRenderers), "Do not modify authored actions");
                if (eyelids) {
                    // No animator services are injected: returning here proves no parameters/layers are created.
                    Assert.DoesNotThrow(() => builder.Apply());
                }
                other.sharedMesh.AddBlendShapeFrame("OtherBlink", 100, new[] { Vector3.down }, null, null);
                var otherAction = new BlendShapeAction { allRenderers = true, blendShape = "OtherBlink" };
                builder.model.state.actions.Add(otherAction);
                Assert.That(builder.GetBlinkState().actions, Does.Contain(otherAction), "A shape absent from covered renderers is retained");
                var nonShape = new ObjectToggleAction { obj = other.gameObject };
                builder.model.state.actions.Add(nonShape);
                Assert.That(builder.GetBlinkState().actions, Does.Contain(nonShape));
            }
        }
    }
}
#endif
