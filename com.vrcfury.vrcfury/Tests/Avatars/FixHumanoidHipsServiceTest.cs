using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;
using VF.Builder;
using VF.Model.Feature;
using VF.Service;
using VF.Utils;
using VRC.SDK3.Avatars.Components;
using Object = UnityEngine.Object;

namespace VF.Tests {
    public class FixHumanoidHipsServiceTest {
        [TestCase(false, "Armature", true, 0)]
        [TestCase(false, "Root", false, 0)]
        [TestCase(true, "Armature", true, 0)]
        [TestCase(true, "Node42", true, 0)]
        [TestCase(true, "Armature", true, 1)]
        [TestCase(true, "Armature", true, 2)]
        public void ArmatureLinkResolvesHips(
            bool misconfigured,
            string armatureName,
            bool separatePelvis,
            int missingLegs
        ) {
            var root = new GameObject("Avatar");
            Avatar original = null;
            Avatar repaired = null;
            try {
                var (rig, armature, hips) = HumanoidTestRig.Create(
                    root,
                    misconfigured,
                    armatureName,
                    separatePelvis,
                    spinePivot: false
                );
                original = rig;
                const string defaultPoseZ = "m_Avatar.m_DefaultPose.data.m_X.Array.data[1].t.z";
                if (misconfigured) {
                    using var serialized = new SerializedObject(original);
                    serialized.FindProperty(defaultPoseZ).floatValue = 0.125f;
                    serialized.ApplyModifiedPropertiesWithoutUndo();
                }
                var animator = root.AddComponent<Animator>();
                animator.avatar = original;
                var cloth = new GameObject("Clothes").transform;
                cloth.SetParent(root.transform, false);
                var clothArmature = Object.Instantiate(armature.gameObject, cloth);
                clothArmature.name = armature.name;
                cloth.SetAsFirstSibling();
                if (missingLegs >= 1) Object.DestroyImmediate(hips.Find("Left Thigh").gameObject);
                if (missingLegs >= 2) Object.DestroyImmediate(hips.Find("Right Thigh").gameObject);
                var link = new ArmatureLink { propBone = cloth.gameObject };
                ArmatureLinkService.Links GetLinks(VRCFArmatureCache cache) {
                    return ArmatureLinkService.GetLinks(link, root, VRCFObjectPathCache.GetPerFrame(root), cache);
                }
                var injector = VRCFuryInjectorBuilder.GetInjector(root.AddComponent<VRCAvatarDescriptor>());
                var armatureCache = injector.GetService<VRCFArmatureCache>();
                var inspectorCache = VRCFArmatureCache.GetPerFrame(root);
                var preview = GetLinks(inspectorCache);
                Assert.That((Transform)preview.avatarMain, Is.EqualTo(hips));
                Assert.That((Transform)preview.mergeBones.Peek().Item2, Is.EqualTo(hips));
                Assert.That(animator.avatar, Is.SameAs(original));

                var offset = hips.localPosition + Vector3.forward * 0.25f;
                hips.localPosition = offset;
                var fix = injector.GetService<FixHumanoidHipsService>();
                fix.Apply();
                repaired = animator.avatar;
                injector.GetService<ObjectCacheService>().CaptureInitialState();
                Assert.That((Transform)GetLinks(armatureCache).mergeBones.Peek().Item2, Is.EqualTo(hips));
                Assert.That(hips.localPosition, Is.EqualTo(offset));
                Assert.That(repaired != original, Is.EqualTo(misconfigured));
                Assert.That(repaired.humanDescription.armStretch, Is.EqualTo(original.humanDescription.armStretch));
                if (misconfigured) {
                    using var serialized = new SerializedObject(repaired);
                    Assert.That(serialized.FindProperty(defaultPoseZ).floatValue, Is.EqualTo(0.125f));
                }
                var animatorHolder = injector.GetService<AnimatorHolderService>();
                animatorHolder.ApplyBefore();
                animatorHolder.ApplyAfter();
                Assert.That(root.GetComponent<Animator>().avatar, Is.SameAs(repaired));
                var finalCache = HumanoidTestRig.CreateCache(root);
                Assert.That((Transform)finalCache.FindBoneOnArmatureOrException(HumanBodyBones.Hips), Is.EqualTo(hips));
                var hipsHumanName = HumanTrait.BoneName[(int)HumanBodyBones.Hips];
                Assert.That(
                    original.humanDescription.human.Single(bone => bone.humanName == hipsHumanName).boneName,
                    Is.EqualTo(misconfigured ? armature.name : hips.name)
                );
            } finally {
                Object.DestroyImmediate(root);
                if (repaired != null && repaired != original) Object.DestroyImmediate(repaired);
                if (original != null) Object.DestroyImmediate(original);
            }
        }

        [TestCase(false, 1f)]
        [TestCase(true, 1f)]
        [TestCase(true, 1.25f)]
        public void DirectPatchMatchesUnityHumanoidPoses(bool rotateBones, float scale) {
            var root = new GameObject("Avatar");
            Avatar template = null;
            Avatar original = null;
            Avatar expected = null;
            Avatar repaired = null;
            GameObject reference = null;
            try {
                var (rig, armature, hips) = HumanoidTestRig.Create(
                    root,
                    misconfigured: true,
                    spinePivot: false
                );
                template = rig;
                armature.localScale = Vector3.one * scale;
                if (rotateBones) {
                    var children = hips.Cast<Transform>().Select(child => (child, child.position, child.rotation)).ToArray();
                    var hipsPosition = hips.position;
                    armature.localRotation = Quaternion.Euler(47, -21, 13);
                    hips.SetPositionAndRotation(hipsPosition, Quaternion.Euler(67, 19, -41));
                    foreach (var (child, position, rotation) in children) child.SetPositionAndRotation(position, rotation);
                }
                var description = template.humanDescription;
                description.skeleton = root.GetComponentsInChildren<Transform>().Select(bone => new SkeletonBone {
                    name = bone.name,
                    position = bone.localPosition,
                    rotation = bone.localRotation,
                    scale = bone.localScale
                }).ToArray();
                original = AvatarBuilder.BuildHumanAvatar(root, description);
                var hipsHumanName = HumanTrait.BoneName[(int)HumanBodyBones.Hips];
                description.human[Array.FindIndex(description.human, bone => bone.humanName == hipsHumanName)].boneName = hips.name;
                expected = AvatarBuilder.BuildHumanAvatar(root, description);
                reference = Object.Instantiate(root);

                var animator = root.AddComponent<Animator>();
                animator.avatar = original;
                var injector = VRCFuryInjectorBuilder.GetInjector(root.AddComponent<VRCAvatarDescriptor>());
                injector.GetService<FixHumanoidHipsService>().Apply();
                repaired = animator.avatar;
                Assert.That(repaired, Is.Not.SameAs(original));

                using var actualRig = new SerializedObject(repaired);
                using var originalRig = new SerializedObject(original);
                const string axes = "m_Avatar.m_Human.data.m_Skeleton.data.m_AxesArray";
                Assert.That(SerializedProperty.DataEquals(originalRig.FindProperty(axes), actualRig.FindProperty(axes)), Is.True);
                const string poses = "m_Avatar.m_Human.data.m_SkeletonPose.data.m_X";
                for (var i = 0; i < 2; i++) {
                    var before = originalRig.FindProperty(poses).GetArrayElementAtIndex(i + 1);
                    var after = actualRig.FindProperty(poses).GetArrayElementAtIndex(i);
                    Assert.That(SerializedProperty.DataEquals(before, after), Is.True);
                }

                using var actualPose = new HumanPoseHandler(repaired, root.transform);
                using var expectedPose = new HumanPoseHandler(expected, reference.transform);
                var actual = root.GetComponentsInChildren<Transform>();
                var wanted = reference.GetComponentsInChildren<Transform>();
                void CheckPose() {
                    for (var i = 0; i < actual.Length; i++) {
                        Assert.That(Vector3.Distance(actual[i].position, wanted[i].position), Is.LessThan(0.0001f), actual[i].name);
                        Assert.That(Quaternion.Angle(actual[i].rotation, wanted[i].rotation), Is.LessThan(0.1f), actual[i].name);
                    }
                }
                var pose = new HumanPose {
                    bodyPosition = new Vector3(0.1f, 1.2f, -0.15f),
                    bodyRotation = Quaternion.Euler(5, 30, -10),
                    muscles = new float[HumanTrait.MuscleCount]
                };
                for (var sample = 0; sample < 4; sample++) {
                    for (var i = 0; i < pose.muscles.Length; i++) pose.muscles[i] = Mathf.Sin(i + sample) * 0.5f;
                    actualPose.SetHumanPose(ref pose);
                    expectedPose.SetHumanPose(ref pose);
                    CheckPose();
                    var actualReadback = new HumanPose();
                    var expectedReadback = new HumanPose();
                    actualPose.GetHumanPose(ref actualReadback);
                    expectedPose.GetHumanPose(ref expectedReadback);
                    Assert.That(Vector3.Distance(actualReadback.bodyPosition, expectedReadback.bodyPosition), Is.LessThan(0.0001f));
                    Assert.That(Quaternion.Angle(actualReadback.bodyRotation, expectedReadback.bodyRotation), Is.LessThan(0.1f));
                    Assert.That(actualReadback.muscles, Is.EqualTo(expectedReadback.muscles).Within(0.0001f));
                }

                var referenceAnimator = reference.AddComponent<Animator>();
                referenceAnimator.avatar = expected;
                var clip = new AnimationClip();
                var graph = PlayableGraph.Create();
                try {
                    for (var i = 0; i < HumanTrait.MuscleCount; i++) {
                        var binding = EditorCurveBinding.FloatCurve("", typeof(Animator), HumanTrait.MuscleName[i]);
                        AnimationUtility.SetEditorCurve(clip, binding, AnimationCurve.Linear(0, -0.2f, 1, 0.4f));
                    }
                    var rootT = EditorCurveBinding.FloatCurve("", typeof(Animator), "RootT.x");
                    AnimationUtility.SetEditorCurve(clip, rootT, AnimationCurve.Linear(0, 0, 1, 0.3f));
                    foreach (var player in new[] { animator, referenceAnimator }) {
                        player.applyRootMotion = true;
                        var output = AnimationPlayableOutput.Create(graph, player.name, player);
                        output.SetSourcePlayable(AnimationClipPlayable.Create(graph, clip));
                    }
                    Assert.That(clip.humanMotion, Is.True);
                    graph.Play();
                    for (var sample = 0; sample < 4; sample++) {
                        graph.Evaluate(0.1f);
                        CheckPose();
                    }
                } finally {
                    graph.Destroy();
                    Object.DestroyImmediate(clip);
                }
            } finally {
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(reference);
                Object.DestroyImmediate(template);
                Object.DestroyImmediate(expected);
                if (repaired != original) Object.DestroyImmediate(repaired);
                Object.DestroyImmediate(original);
            }
        }

        [TestCase(false)]
        [TestCase(true)]
        public void SkipsUnlessSpineParentIsDirectChildAndContainsAllMappedBones(bool spinePivot) {
            var root = new GameObject("Avatar");
            Avatar rig = null;
            try {
                var (avatar, armature, hips) = HumanoidTestRig.Create(
                    root,
                    misconfigured: true,
                    spinePivot: spinePivot,
                    thighOutsidePelvis: !spinePivot
                );
                rig = avatar;
                var animator = root.AddComponent<Animator>();
                animator.avatar = rig;
                if (!spinePivot) Object.DestroyImmediate(hips.parent.Find("Left Thigh").gameObject);
                var inspectorCache = VRCFArmatureCache.GetPerFrame(root);
                Assert.That((Transform)inspectorCache.FindBoneOnArmatureOrException(HumanBodyBones.Hips), Is.EqualTo(armature));
                var injector = VRCFuryInjectorBuilder.GetInjector(root.AddComponent<VRCAvatarDescriptor>());
                injector.GetService<FixHumanoidHipsService>().Apply();
                Assert.That(animator.avatar, Is.SameAs(rig));
            } finally {
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(rig);
            }
        }
    }

    internal static class HumanoidTestRig {
        public static VRCFArmatureCache CreateCache(GameObject root) {
            var cache = new VRCFArmatureCache();
            typeof(VRCFArmatureCache).VFField("avatarObject").SetValue(cache, (VFGameObject)root);
            return cache;
        }

        public static (Avatar avatar, Transform armature, Transform hips) Create(
            GameObject root,
            bool misconfigured = false,
            string armatureName = "Armature",
            bool separatePelvis = true,
            bool spinePivot = true,
            bool thighOutsidePelvis = false
        ) {
            var mapping = new List<HumanBone>();
            Transform Add(Transform parent, string name, Vector3 position, HumanBodyBones bone = HumanBodyBones.LastBone) {
                var joint = new GameObject(name).transform;
                joint.SetParent(parent, false);
                joint.localPosition = position;
                if (bone != HumanBodyBones.LastBone) {
                    mapping.Add(new HumanBone {
                        boneName = name,
                        humanName = HumanTrait.BoneName[(int)bone],
                        limit = new HumanLimit { useDefaultValues = true }
                    });
                }
                return joint;
            }

            var armature = Add(
                root.transform,
                armatureName,
                Vector3.zero,
                misconfigured || !separatePelvis ? HumanBodyBones.Hips : HumanBodyBones.LastBone
            );
            var hips = separatePelvis ? Add(
                armature,
                "Pelvis",
                Vector3.up,
                misconfigured ? HumanBodyBones.LastBone : HumanBodyBones.Hips
            ) : armature;
            var pivot = spinePivot ? Add(hips, "Spine Pivot", Vector3.up * 0.1f) : hips;
            var spine = Add(pivot, "Spine", Vector3.up * 0.1f, HumanBodyBones.Spine);
            var chest = Add(spine, "Chest", Vector3.up * 0.25f, HumanBodyBones.Chest);
            Add(chest, "Head", Vector3.up * 0.35f, HumanBodyBones.Head);

            void AddSide(bool left) {
                var side = left ? "Left" : "Right";
                var direction = left ? -1 : 1;
                var thigh = Add(
                    left && thighOutsidePelvis ? armature : hips,
                    side + " Thigh",
                    new Vector3(0.15f * direction, -0.1f, 0),
                    left ? HumanBodyBones.LeftUpperLeg : HumanBodyBones.RightUpperLeg
                );
                var calf = Add(
                    thigh,
                    side + " Calf",
                    new Vector3(0, -0.45f, 0.02f),
                    left ? HumanBodyBones.LeftLowerLeg : HumanBodyBones.RightLowerLeg
                );
                Add(
                    calf,
                    side + " Foot",
                    new Vector3(0, -0.35f, -0.02f),
                    left ? HumanBodyBones.LeftFoot : HumanBodyBones.RightFoot
                );
                var arm = Add(
                    chest,
                    side + " Arm",
                    new Vector3(0.2f * direction, 0.2f, 0),
                    left ? HumanBodyBones.LeftUpperArm : HumanBodyBones.RightUpperArm
                );
                var elbow = Add(
                    arm,
                    side + " Elbow",
                    new Vector3(0.3f * direction, 0, 0),
                    left ? HumanBodyBones.LeftLowerArm : HumanBodyBones.RightLowerArm
                );
                Add(
                    elbow,
                    side + " Hand",
                    new Vector3(0.25f * direction, 0, 0),
                    left ? HumanBodyBones.LeftHand : HumanBodyBones.RightHand
                );
            }
            AddSide(true);
            AddSide(false);

            var description = new HumanDescription {
                human = mapping.ToArray(),
                skeleton = root.GetComponentsInChildren<Transform>().Select(joint => new SkeletonBone {
                    name = joint.name,
                    position = joint.localPosition,
                    rotation = joint.localRotation,
                    scale = joint.localScale
                }).ToArray(),
                armStretch = 0.09f
            };
            var avatar = AvatarBuilder.BuildHumanAvatar(root, description);
            Assert.That(avatar.isValid && avatar.isHuman, Is.True);
            return (avatar, armature, hips);
        }
    }
}
