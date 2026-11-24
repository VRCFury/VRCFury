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

    internal class FaceTrackingUtilsTest : FaceTrackingTestBase {
        [Test]
        public void EnsureMeshCompactsNullAndDuplicateEntries() {
            using (var build = new Build()) {
                var native = build.Skin("Native");
                var added = build.Skin("Added");
                build.avatar.faceTrackingMeshes = Enumerable.Repeat(native, FaceTrackingUtils.MaxFaceTrackingMeshes).ToList();
                build.avatar.faceTrackingMeshes[1] = null;
                build.NativeMesh(added);
                Assert.That(build.avatar.faceTrackingMeshes, Is.EqualTo(new[] { native, added }));
            }
        }

        [Test]
        public void SharedSetReferenceRewritePreservesInputsAndControlSettings() {
            var original = new FtBehaviourControl {
                blendshapesSetName = "Source", weight = 0.3f, lerpSpeed = 0.7f, sourceParameter = "Weight",
                type = VRC.SDKBase.VRC_AnimatorFaceTrackingBlendshapesSet.SourceType.Parameter
            };
            var unchanged = new FtBehaviourControl { blendshapesSetName = "Unchanged" };
            var controls = new List<FtBehaviourControl> { original, null, unchanged };
            var output = FaceTrackingUtils.RewriteSetReferences(controls, name => name == "Source" ? new[] { "Left", "Right" } : null);
            Assert.That(output.Select(control => control?.blendshapesSetName), Is.EqualTo(new[] { "Left", "Right", null, "Unchanged" }));
            Assert.That(original.blendshapesSetName, Is.EqualTo("Source"));
            foreach (var copy in output.Take(2)) {
                Assert.That(copy, Is.Not.SameAs(original));
                Assert.That(copy.weight, Is.EqualTo(original.weight));
                Assert.That(copy.lerpSpeed, Is.EqualTo(original.lerpSpeed));
                Assert.That(copy.sourceParameter, Is.EqualTo(original.sourceParameter));
                Assert.That(copy.type, Is.EqualTo(original.type));
            }
            Assert.That(output[3], Is.Not.SameAs(unchanged));
        }
    }
}
#endif
