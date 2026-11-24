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

    internal class RemoveLegacyFaceTrackingServiceTest : FaceTrackingTestBase {
        [TestCase(VRCAvatarDescriptor.FaceTrackingKey.EyeClosedLeft, false)]
        [TestCase(VRCAvatarDescriptor.FaceTrackingKey.EyeClosedRight, false)]
        [TestCase(VRCAvatarDescriptor.FaceTrackingKey.JawOpen, false)]
        [TestCase(VRCAvatarDescriptor.FaceTrackingKey.EyeClosedLeft, true)]
        [TestCase(VRCAvatarDescriptor.FaceTrackingKey.EyeClosedRight, true)]
        [TestCase(VRCAvatarDescriptor.FaceTrackingKey.JawOpen, true)]
        public void BehaviorInputsMarkEyelidsHandledWithoutDirectBlendshapeMappings(
            VRCAvatarDescriptor.FaceTrackingKey key, bool custom) {
            using (var build = new Build()) {
                var covered = build.Skin("Covered");
                var other = build.Skin("Other");
                build.avatar.enableEyeLook = true;
                build.avatar.customEyeLookSettings.eyelidType = VRCAvatarDescriptor.EyelidType.Blendshapes;
                build.avatar.customEyeLookSettings.eyelidsSkinnedMesh = covered;
                build.globals.allFeaturesInRun.Add(new FullController {
                    faceTrackingMeshes = { (ObjectReference<SkinnedMeshRenderer>)covered },
                    faceTrackingParameterBehaviors = {
                        new VRCAvatarDescriptor.FaceTrackingParameterBehavior {
                            targetMode = VRCAvatarDescriptor.ParameterBehaviorTargetMode.ApplyToBlendshape,
                            targetBlendshapeName = "Smile",
                            inputTrackingKeyNames = custom ? System.Array.Empty<string>() : new[] { "Voice", key.ToString() },
                            combineMode = custom ? VRCAvatarDescriptor.ParameterBehaviorCombineMode.Custom
                                : VRCAvatarDescriptor.ParameterBehaviorCombineMode.Max,
                            customExpression = $"max(Voice, {key})"
                        }
                    }
                });
                var service = build.RemoveBuiltIns();
                var eyelids = key == VRCAvatarDescriptor.FaceTrackingKey.EyeClosedLeft
                              || key == VRCAvatarDescriptor.FaceTrackingKey.EyeClosedRight;
                Assert.That(service.GetHandled(covered).eyelids, Is.EqualTo(eyelids));
                Assert.That(service.GetHandled(other).eyelids, Is.False);
                Assert.That(build.avatar.customEyeLookSettings.eyelidType, Is.EqualTo(eyelids
                    ? VRCAvatarDescriptor.EyelidType.None : VRCAvatarDescriptor.EyelidType.Blendshapes));
            }
        }

        [TestCase(false, false)]
        [TestCase(false, true)]
        [TestCase(true, false)]
        [TestCase(true, true)]
        public void BehaviorOnlyVisemesReplaceLegacyVisemesOnCoveredRenderer(bool custom, bool jawFlap) {
            using (var build = new Build()) {
                var covered = build.Skin("Covered");
                var other = build.Skin("Other");
                build.avatar.VisemeSkinnedMesh = covered;
                build.avatar.lipSync = jawFlap ? VRC.SDKBase.VRC_AvatarDescriptor.LipSyncStyle.JawFlapBlendShape
                    : VRC.SDKBase.VRC_AvatarDescriptor.LipSyncStyle.VisemeBlendShape;
                build.avatar.VisemeBlendShapes = Enumerable.Repeat("Smile", FaceTrackingUtils.VisemeCount).ToArray();
                build.avatar.MouthOpenBlendShapeName = "Smile";
                build.globals.allFeaturesInRun.Add(new FullController {
                    faceTrackingMeshes = { (ObjectReference<SkinnedMeshRenderer>)covered },
                    faceTrackingParameterBehaviors = {
                        new VRCAvatarDescriptor.FaceTrackingParameterBehavior {
                            targetMode = VRCAvatarDescriptor.ParameterBehaviorTargetMode.ApplyToBlendshape,
                            targetBlendshapeName = "Smile",
                            inputTrackingKeyNames = custom ? System.Array.Empty<string>() : new[] { "Viseme_aa" },
                            combineMode = custom ? VRCAvatarDescriptor.ParameterBehaviorCombineMode.Custom
                                : VRCAvatarDescriptor.ParameterBehaviorCombineMode.Max,
                            customExpression = "max(Viseme_aa, Voice)"
                        }
                    }
                });
                var service = build.RemoveBuiltIns();
                Assert.That(service.GetHandled(covered).visemes, Is.True);
                Assert.That(service.GetHandled(other).visemes, Is.False);
                Assert.That(build.avatar.lipSync, Is.EqualTo(VRC.SDKBase.VRC_AvatarDescriptor.LipSyncStyle.VisemeParameterOnly));
            }
        }

        [Test, Combinatorial]
        public void FullControllerCoverageReplacesOnlyMatchingBuiltInGroups(
            [Values("blink", "gaze", "visemes")] string group,
            [Values] bool sameMesh, [Values] bool coveredFirst, [Values] bool validShape) {
            using (var build = new Build()) {
                var legacy = build.Skin("Legacy");
                legacy.sharedMesh.AddBlendShapeFrame("Up", 100, new[] { Vector3.right }, null, null);
                legacy.sharedMesh.AddBlendShapeFrame("Down", 100, new[] { Vector3.down }, null, null);
                var covered = sameMesh ? legacy : build.Skin("Covered");
                var other = build.Skin("Other");
                build.avatar.lipSync = VRC.SDKBase.VRC_AvatarDescriptor.LipSyncStyle.VisemeBlendShape;
                build.avatar.VisemeSkinnedMesh = legacy;
                build.avatar.VisemeBlendShapes = Enumerable.Repeat("Smile", FaceTrackingUtils.VisemeCount).ToArray();
                build.avatar.enableEyeLook = true;
                build.avatar.customEyeLookSettings.eyelidType = VRCAvatarDescriptor.EyelidType.Blendshapes;
                build.avatar.customEyeLookSettings.eyeGazeMode = VRCAvatarDescriptor.CustomEyeLookSettings.EyeGazeMode.BoneRotation;
                build.avatar.customEyeLookSettings.eyelidsSkinnedMesh = legacy;
                build.avatar.customEyeLookSettings.eyelidsBlendshapes = new[] { 0, 1, 2 };
                var key = group == "blink" ? VRCAvatarDescriptor.FaceTrackingKey.EyeClosedLeft
                    : VRCAvatarDescriptor.FaceTrackingKey.EyeLookUpLeft;
                var shape = validShape ? "Smile" : "Missing";
                var settings = FaceTrackingUtils.CreateBlendshapeSet("Covered");
                SetMapping(settings, group == "visemes" ? 1 : (int)key, shape, visemes: group == "visemes");
                // The prepass sees all components, including the one applied last.
                build.globals.allFeaturesInRun.Add(new FullController {
                    faceTrackingMeshes = { (ObjectReference<SkinnedMeshRenderer>)covered },
                    faceTrackingBlendshapeSets = { settings }
                });
                build.RemoveBuiltIns();
                void Covered() => build.Full(covered.gameObject, key, visemes: group == "visemes", blendshape: shape);
                void Other() => build.Full(other.gameObject,
                    group == "blink" ? VRCAvatarDescriptor.FaceTrackingKey.EyeLookUpLeft
                        : VRCAvatarDescriptor.FaceTrackingKey.EyeClosedLeft,
                    visemes: group == "visemes");
                if (coveredFirst) { Covered(); Other(); }
                else { Other(); Covered(); }
                build.Run();
                var replaced = sameMesh;
                var upgradedVisemes = build.avatar.faceTrackingBlendshapesSetList
                    .Where(set => set.setName.StartsWith("_vrcf_upgraded_visemes")).ToArray();
                if (group == "visemes") Assert.That(upgradedVisemes.Any(), Is.EqualTo(!replaced));
                if (group != "visemes") {
                    var upgradedEyes = build.avatar.faceTrackingBlendshapesSetList
                        .Where(set => set.setName.StartsWith("_vrcf_upgraded_eyelids")).ToArray();
                    bool HasLegacy(VRCAvatarDescriptor.FaceTrackingKey eyeKey) => upgradedEyes.Any(set =>
                        !string.IsNullOrEmpty(set.faceTrackingBlendshapeNames[(int)eyeKey]));
                    Assert.That(HasLegacy(VRCAvatarDescriptor.FaceTrackingKey.EyeClosedLeft), Is.EqualTo(group != "blink" || !replaced));
                    Assert.That(HasLegacy(VRCAvatarDescriptor.FaceTrackingKey.EyeLookUpLeft), Is.EqualTo(group != "blink" || !replaced));
                }
            }
        }

        [TestCase(VRCAvatarDescriptor.EyelidType.Blendshapes, VRCAvatarDescriptor.FaceTrackingKey.EyeClosedLeft)]
        [TestCase(VRCAvatarDescriptor.EyelidType.Bones, VRCAvatarDescriptor.FaceTrackingKey.EyeClosedLeft)]
        [TestCase(VRCAvatarDescriptor.EyelidType.Blendshapes, VRCAvatarDescriptor.FaceTrackingKey.EyeLookUpLeft)]
        [TestCase(VRCAvatarDescriptor.EyelidType.Bones, VRCAvatarDescriptor.FaceTrackingKey.EyeLookUpLeft)]
        public void RemovingBuiltInBlinkingPreventsLegacyFtMigration(VRCAvatarDescriptor.EyelidType eyelidType, VRCAvatarDescriptor.FaceTrackingKey importedKey) {
            using (var build = new Build()) {
                var legacy = build.Skin("Legacy");
                var imported = build.Skin("Imported");
                build.avatar.enableEyeLook = true;
                build.avatar.customEyeLookSettings.eyelidType = eyelidType;
                build.avatar.customEyeLookSettings.eyelidsSkinnedMesh = legacy;
                build.avatar.customEyeLookSettings.eyelidsBlendshapes = new[] { 0, -1, -1 };
                build.globals.allFeaturesInRun.Add(new RemoveBlinking());
                build.RemoveBuiltIns();
                build.Full(imported.gameObject, importedKey);
                build.Run();
                Assert.That(build.avatar.faceTrackingBlendshapesSetList
                    .Any(set => set.setName.StartsWith("_vrcf_upgraded_eyelids")), Is.False);
                Assert.That(build.avatar.faceTrackingBlendshapesSetList
                    .Any(set => !string.IsNullOrEmpty(set.faceTrackingBlendshapeNames[(int)importedKey])), Is.True);
            }
        }
    }
}
#endif
