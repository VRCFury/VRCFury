using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Animations;
using VF.Builder;
using VF.Exceptions;
using VF.Feature.Base;
using VF.Model;
using VF.Model.Feature;
using VF.Service;
using VF.Utils;
using VRC.SDK3.Avatars.Components;
using VRC.SDK3.Dynamics.PhysBone.Components;
using Object = UnityEngine.Object;

namespace VF.Tests {
    internal class ArmatureLinkServiceTest {
        [Test]
        public void ForcedMergedNameRejectsASourceOutsideTheAvatar() {
            var avatar = new GameObject("Avatar");
            var external = new GameObject("External source");
            try {
                var descriptor = avatar.AddComponent<VRCAvatarDescriptor>();
                var model = new ArmatureLink {
                    propBone = external,
                    forceMergedName = "Imported",
                    linkTo = new List<ObjectReference> {
                        new ObjectReference {
                            useBone = false,
                            useObj = false
                        }
                    }
                };
                var injector = VRCFuryInjectorBuilder.GetInjector(descriptor);
                var builder = FeatureFinder.GetBuilder(model, avatar, injector);
                builder.featureBaseObject = avatar;
                injector.GetService<GlobalsService>().allBuildersInRun = new List<FeatureBuilder> { builder };

                var service = injector.GetService<ArmatureLinkService>();
                var error = Assert.Throws<ExceptionWithCause>(service.ApplyForceMergedNamesEarly);

                Assert.That(error.Message, Does.Contain("External source is not a child of Avatar"));
                Assert.That(external.transform.parent, Is.Null);
                Assert.That(external.name, Is.EqualTo("External source"));
            } finally {
                Object.DestroyImmediate(avatar);
                if (external != null) Object.DestroyImmediate(external);
            }
        }

        [TestCase(false)]
        [TestCase(true)]
        public void RemovesOnlyParentConstraintsAffectingLinkedBones(bool removeParentConstraints) {
            var avatar = new GameObject("Avatar");
            try {
                var descriptor = avatar.AddComponent<VRCAvatarDescriptor>();
                var target = GameObjects.Create("Target", avatar, removeFromPhysbones: false);
                var prop = GameObjects.Create("Prop", avatar, removeFromPhysbones: false);
                prop.AddComponent<BoxCollider>();
                var position = prop.AddComponent<PositionConstraint>();
                var attached = VFConstraint.CreateParent(prop).GetComponent();
                var other = VFConstraint.CreateParent(target).GetComponent();
                var targetField = attached.GetType().VFField("TargetTransform");
                UnityEngine.Component hosted = null;
                if (targetField != null) {
                    var host = GameObjects.Create("Constraint host", avatar, removeFromPhysbones: false);
                    hosted = VFConstraint.CreateParent(host).GetComponent();
                    targetField.SetValue(hosted, (Transform)prop);
                }

                var model = new ArmatureLink {
                    propBone = prop,
                    removeParentConstraints = removeParentConstraints,
                    linkTo = new List<ObjectReference> {
                        new ObjectReference {
                            useBone = false,
                            useObj = true,
                            obj = target
                        }
                    }
                };
                var injector = VRCFuryInjectorBuilder.GetInjector(descriptor);
                var builder = FeatureFinder.GetBuilder(model, prop, injector);
                injector.GetService<GlobalsService>().allBuildersInRun = new List<FeatureBuilder> { builder };
                injector.GetService<ArmatureLinkService>().Apply();

                Assert.That(attached == null, Is.EqualTo(removeParentConstraints));
                if (targetField != null) Assert.That(hosted == null, Is.EqualTo(removeParentConstraints));
                Assert.That(other != null, Is.True);
                Assert.That(position != null, Is.True);
                Assert.That(prop.parent.parent, Is.EqualTo(target));
            } finally {
                Object.DestroyImmediate(avatar);
            }
        }

        [Test]
        public void ExcludingTheLinkWrapperAlsoExcludesMovedObjectsAndNestedToggleWrappers() {
            var avatar = new GameObject("Avatar");
            try {
                var descriptor = avatar.AddComponent<VRCAvatarDescriptor>();
                var bone = GameObjects.Create("Target bone", avatar, removeFromPhysbones: false);
                var outside = GameObjects.Create("Physbone settings", avatar, removeFromPhysbones: false);
                var physbone = outside.AddComponent<VRCPhysBone>();
                physbone.rootTransform = bone;
                var inactive = GameObjects.Create("Toggle source", avatar, removeFromPhysbones: false);
                inactive.active = false;
                var prop = GameObjects.Create("Prop", inactive, removeFromPhysbones: false);
                prop.AddComponent<BoxCollider>();

                var model = new ArmatureLink {
                    propBone = prop,
                    recursive = false,
                    linkTo = new List<ObjectReference> { new ObjectReference { useBone = false, useObj = true, obj = bone } }
                };
                var injector = VRCFuryInjectorBuilder.GetInjector(descriptor);
                var builder = FeatureFinder.GetBuilder(model, prop, injector);
                injector.GetService<GlobalsService>().allBuildersInRun = new List<FeatureBuilder> { builder };
                injector.GetService<ArmatureLinkService>().Apply();

                Assert.That(prop.parent.name, Is.EqualTo("Toggle From Toggle source"));
                Assert.That(prop.parent.parent.parent, Is.EqualTo(bone));
                Assert.That(physbone.ignoreTransforms, Is.EqualTo(new[] { (Transform)prop.parent.parent }));
                Assert.That(physbone.IsIgnored(prop), Is.True);
            } finally {
                Object.DestroyImmediate(avatar);
            }
        }

        [TestCase(2)]
        [TestCase(3)]
        [TestCase(4)]
        public void RewritingBonesPreservesBindPosesAndSourceMesh(int bindPoseCount) {
            var root = new GameObject("Avatar");
            var source = new Mesh {
                vertices = new[] { Vector3.zero, Vector3.right, Vector3.up },
                triangles = new[] { 0, 1, 2 },
                boneWeights = Enumerable.Repeat(new BoneWeight { boneIndex0 = 0, weight0 = 1 }, 3).ToArray()
            };
            var outputs = new HashSet<Mesh>();
            try {
                Transform Child(string name) {
                    var obj = new GameObject(name);
                    obj.transform.SetParent(root.transform, false);
                    return obj.transform;
                }
                var from = Child("From");
                from.SetPositionAndRotation(new Vector3(1, 2, 3), Quaternion.Euler(10, 20, 30));
                from.localScale = Vector3.one * 1.5f;
                var to = Child("To");
                to.SetPositionAndRotation(new Vector3(-1, 3, 2), Quaternion.Euler(30, -20, 10));
                var final = Child("Final");
                final.position = new Vector3(4, -2, 1);
                var untouched = Child("Untouched");
                var originalPoses = Enumerable.Range(0, bindPoseCount)
                    .Select(i => Matrix4x4.Translate(Vector3.right * i))
                    .ToArray();
                source.bindposes = originalPoses;
                var skin = Child("Skin").gameObject.AddComponent<SkinnedMeshRenderer>();
                skin.sharedMesh = source;
                skin.bones = new[] { from, untouched, from };
                skin.rootBone = from;
                var other = Child("Other skin").gameObject.AddComponent<SkinnedMeshRenderer>();
                other.sharedMesh = source;
                other.bones = new[] { untouched };
                var withoutMesh = Child("No mesh").gameObject.AddComponent<SkinnedMeshRenderer>();
                withoutMesh.bones = new[] { from };

                var rewriter = new ArmatureLinkService.SkinRewriter(root);
                void Rewrite(Transform oldBone, Transform newBone) {
                    rewriter.Rewrite(oldBone, newBone);
                    outputs.Add(skin.sharedMesh);
                }
                Assert.That(rewriter.GetRootName(to), Is.EqualTo("To"));
                Rewrite(from, to);
                Assert.That(rewriter.GetRootName(to), Is.EqualTo("Avatar"));
                Rewrite(to, final);
                Assert.That(rewriter.GetRootName(to), Is.EqualTo("To"));
                Assert.That(rewriter.GetRootName(from), Is.EqualTo("Avatar"));
                Assert.That(skin.bones, Is.EqualTo(new[] { final, untouched, final }));
                Assert.That(withoutMesh.bones, Is.EqualTo(new[] { final }));
                Assert.That(skin.rootBone, Is.SameAs(from), "Root bone controls bounds and must remain unchanged");
                Assert.That(skin.sharedMesh, Is.Not.SameAs(source));
                Assert.That(other.sharedMesh, Is.SameAs(source));
                Assert.That(other.bones, Is.EqualTo(new[] { untouched }));
                Assert.That(source.bindposes, Is.EqualTo(originalPoses));
                var actual = skin.sharedMesh.bindposes;
                Assert.That(actual.Length, Is.EqualTo(Math.Min(3, bindPoseCount)));
                for (var i = 0; i < actual.Length; i++) {
                    var expected = i == 1 ? originalPoses[i]
                        : final.worldToLocalMatrix * from.localToWorldMatrix * originalPoses[i];
                    for (var element = 0; element < 16; element++) {
                        Assert.That(actual[i][element], Is.EqualTo(expected[element]).Within(0.00001f));
                    }
                }
            } finally {
                Object.DestroyImmediate(root);
                foreach (var mesh in outputs.Where(m => m != source)) Object.DestroyImmediate(mesh);
                Object.DestroyImmediate(source);
            }
        }

        [Test]
        public void UsageReasonsKeepSharedAncestorsWithoutEscapingAvatar() {
            var outside = new GameObject("Outside avatar");
            try {
                GameObject Child(string name, Transform parent) {
                    var child = new GameObject(name);
                    child.transform.SetParent(parent, false);
                    return child;
                }
                var avatar = Child("Avatar", outside.transform);
                avatar.AddComponent<BoxCollider>();
                var shared = Child("Shared", avatar.transform);
                shared.AddComponent<BoxCollider>();
                var branch = Child("Branch", shared.transform);
                var first = Child("First", branch.transform);
                first.AddComponent<BoxCollider>();
                var second = Child("Second", branch.transform);
                second.AddComponent<BoxCollider>();
                var unused = Child("Unused", shared.transform);
                var reasons = ArmatureLinkService.GetUsageReasons(avatar);
                foreach (var obj in new[] { avatar, shared, branch }) {
                    Assert.That(reasons.Get(obj), Does.Contain("A child object is used"));
                }
                Assert.That(reasons.Get(shared), Does.Contain("Contains BoxCollider component"));
                foreach (var obj in new[] { first, second }) {
                    Assert.That(reasons.Get(obj), Does.Contain("Contains BoxCollider component"));
                    Assert.That(reasons.Get(obj), Does.Not.Contain("A child object is used"));
                }
                Assert.That(reasons.ContainsKey(unused), Is.False);
                Assert.That(reasons.ContainsKey(outside), Is.False);
            } finally {
                Object.DestroyImmediate(outside);
            }
        }

        [Test]
        public void UsageReasonsPreserveEachBoneReferenceAndObserveRendererMoves() {
            var root = new GameObject("Avatar");
            try {
                var parent = new GameObject("Group");
                parent.transform.SetParent(root.transform, false);
                var first = new GameObject("First");
                first.transform.SetParent(root.transform, false);
                var second = new GameObject("Second");
                second.transform.SetParent(root.transform, false);
                var skin = parent.AddComponent<SkinnedMeshRenderer>();
                skin.bones = new[] { first.transform, second.transform, first.transform };
                foreach (var name in new[] { "Group", "Moved" }) {
                    parent.name = name;
                    var reasons = ArmatureLinkService.GetUsageReasons(root);
                    Assert.That(reasons.Get(first), Does.Contain("m_Bones.Array.data[0] in SkinnedMeshRenderer on " + name));
                    Assert.That(reasons.Get(first), Does.Contain("m_Bones.Array.data[2] in SkinnedMeshRenderer on " + name));
                    Assert.That(reasons.Get(second), Does.Contain("m_Bones.Array.data[1] in SkinnedMeshRenderer on " + name));
                }
            } finally {
                Object.DestroyImmediate(root);
            }
        }
    }
}
