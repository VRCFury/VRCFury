using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using VF.Service;
using VF.Utils;
using Object = UnityEngine.Object;

namespace VF.Tests {
    internal class ArmatureLinkTest {
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

                void Rewrite(Transform oldBone, Transform newBone) {
                    typeof(ArmatureLinkService).GetMethod("RewriteSkins", BindingFlags.NonPublic | BindingFlags.Static)
                        .Invoke(null, new object[] { (VFGameObject)oldBone, (VFGameObject)newBone, (VFGameObject)root });
                    outputs.Add(skin.sharedMesh);
                }
                Rewrite(from, to);
                Rewrite(to, final);
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
    }
}
