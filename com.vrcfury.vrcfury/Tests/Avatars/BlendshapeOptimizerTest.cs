using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using VF.Builder;
using VF.Feature;
using VF.Feature.Base;
using VF.Model.Feature;
using VF.Service;
using VF.Utils;
using VRC.SDK3.Avatars.Components;
using VRC.SDKBase;
using Object = UnityEngine.Object;

namespace VF.Tests {
    internal class BlendshapeOptimizerTest {
        [TestCase(0f)]
        [TestCase(37f)]
        public void RemovingZeroWeightShapesPreservesBakingAndKeptFrames(float keptWeight) {
            var root = new GameObject("Avatar");
            var source = new Mesh {
                vertices = new[] { Vector3.zero, Vector3.right, Vector3.up },
                normals = Enumerable.Repeat(Vector3.forward, 3).ToArray(),
                tangents = Enumerable.Repeat(new Vector4(1, 0, 0, 1), 3).ToArray(),
                triangles = new[] { 0, 1, 2 }
            };
            Mesh output = null;
            try {
                var avatar = root.AddComponent<VRCAvatarDescriptor>();
                avatar.baseAnimationLayers = new VRCAvatarDescriptor.CustomAnimLayer[0];
                avatar.specialAnimationLayers = new VRCAvatarDescriptor.CustomAnimLayer[0];
                var body = new GameObject("Body");
                body.transform.SetParent(root.transform, false);
                var skin = body.AddComponent<SkinnedMeshRenderer>();
                skin.sharedMesh = source;
                var keptDeltas = Enumerable.Repeat(Vector3.up * 0.2f, 3).ToArray();
                var bakedDeltas = Enumerable.Repeat(Vector3.right * 4, 3).ToArray();
                var bakedNormals = Enumerable.Repeat(Vector3.up * 0.4f, 3).ToArray();
                var bakedTangents = Enumerable.Repeat(Vector3.forward * 0.8f, 3).ToArray();
                source.AddBlendShapeFrame("Kept", 50, keptDeltas, bakedNormals, bakedTangents);
                source.AddBlendShapeFrame("Kept", 100, bakedDeltas, bakedNormals, bakedTangents);
                source.AddBlendShapeFrame("RemovedZero", 100, bakedDeltas, bakedNormals, bakedTangents);
                source.AddBlendShapeFrame("RemovedBaked", 100, bakedDeltas, bakedNormals, bakedTangents);
                skin.SetBlendShapeWeight(0, keptWeight);
                skin.SetBlendShapeWeight(1, 0);
                skin.SetBlendShapeWeight(2, 25);
                avatar.lipSync = VRC_AvatarDescriptor.LipSyncStyle.JawFlapBlendShape;
                avatar.VisemeSkinnedMesh = skin;
                avatar.MouthOpenBlendShapeName = "Kept";
                Optimize(avatar);
                output = skin.sharedMesh;
                Assert.That(output, Is.Not.SameAs(source));
                Assert.That(output.blendShapeCount, Is.EqualTo(1));
                Assert.That(output.GetBlendShapeName(0), Is.EqualTo("Kept"));
                Assert.That(output.GetBlendShapeFrameCount(0), Is.EqualTo(2));
                Assert.That(skin.GetBlendShapeWeight(0), Is.EqualTo(keptWeight));
                Assert.That(output.vertices, Is.EqualTo(source.vertices.Select(v => v + Vector3.right).ToArray()));
                Assert.That(output.normals, Is.EqualTo(source.normals.Select(v => v + Vector3.up * 0.1f).ToArray()));
                Assert.That(output.tangents, Is.EqualTo(source.tangents.Select(v => v + new Vector4(0, 0, 0.2f, 0)).ToArray()));
                for (var frame = 0; frame < 2; frame++) {
                    var vertices = new Vector3[3];
                    var normals = new Vector3[3];
                    var tangents = new Vector3[3];
                    output.GetBlendShapeFrameVertices(0, frame, vertices, normals, tangents);
                    Assert.That(output.GetBlendShapeFrameWeight(0, frame), Is.EqualTo(frame == 0 ? 50 : 100));
                    Assert.That(vertices, Is.EqualTo(frame == 0 ? keptDeltas : bakedDeltas));
                    Assert.That(normals, Is.EqualTo(bakedNormals));
                    Assert.That(tangents, Is.EqualTo(bakedTangents));
                }
                Assert.That(source.blendShapeCount, Is.EqualTo(3), "Do not mutate the source mesh");
                Assert.That(source.vertices, Is.EqualTo(new[] { Vector3.zero, Vector3.right, Vector3.up }));
            } finally {
                if (output != null && output != source) Object.DestroyImmediate(output);
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(source);
            }
        }

        [TestCase(0.5f, "position")]
        [TestCase(100f, "normal")]
        [TestCase(100f, "tangent")]
        [TestCase(0.5f, null)]
        [TestCase(100f, null)]
        public void RemovesOnlyEntirelyTinyShapes(float size, string significantChannel) {
            const float tiny = 0.00005f;
            var root = new GameObject("Avatar");
            var source = new Mesh {
                vertices = new[] { Vector3.zero, Vector3.right * size, Vector3.up * size, Vector3.forward * size }
            };
            Mesh output = null;
            try {
                var vertices = new[] { Vector3.right * (size * tiny), Vector3.zero, Vector3.zero, Vector3.zero };
                var normals = new[] { Vector3.up * tiny, Vector3.zero, Vector3.zero, Vector3.zero };
                var tangents = (Vector3[])normals.Clone();
                source.AddBlendShapeFrame("Kept", 50, vertices, normals, tangents);
                if (significantChannel == "position") vertices[1] = Vector3.up * (size * 0.0002f);
                if (significantChannel == "normal") normals[2] = Vector3.up * 0.0002f;
                if (significantChannel == "tangent") tangents[3] = Vector3.up * 0.0002f;
                source.AddBlendShapeFrame("Kept", 100, vertices, normals, tangents);
                source.AddBlendShapeFrame("Unused", 100, vertices, null, null);
                source.AddBlendShapeFrame("Blink", 100, source.vertices, null, null);
                source.bounds = new Bounds(Vector3.zero, Vector3.one * 1000);
                var skin = root.AddComponent<SkinnedMeshRenderer>();
                skin.sharedMesh = source;
                skin.localBounds = new Bounds(Vector3.zero, Vector3.one * 1000);
                var avatar = root.AddComponent<VRCAvatarDescriptor>();
                avatar.lipSync = VRC_AvatarDescriptor.LipSyncStyle.JawFlapBlendShape;
                avatar.VisemeSkinnedMesh = skin;
                avatar.MouthOpenBlendShapeName = "Kept";
                avatar.customEyeLookSettings.eyelidType = VRCAvatarDescriptor.EyelidType.Blendshapes;
                avatar.customEyeLookSettings.eyelidsSkinnedMesh = skin;
                avatar.customEyeLookSettings.eyelidsBlendshapes = new[] { 0, 2, -1 };
                skin.SetBlendShapeWeight(2, 42);
                Optimize(avatar);
                output = skin.sharedMesh;
                var removed = significantChannel == null || significantChannel == "tangent";
                var workLog = string.Join("\n", output.GetWorkLog());
                Assert.That(workLog, Does.Contain($"Removed 1 unanimated blendshapes and {(removed ? 1 : 0)} blendshapes with negligible deltas"));
                Assert.That(workLog, Does.Contain("Removed unanimated \"Unused\" at weight 0"));
                if (removed) {
                    Assert.That(workLog, Does.Contain(FormattableString.Invariant($"Removed \"Kept\" (peak position {size * tiny:G4} mesh units ({tiny:G4} of mesh size), normal {tiny:G4})")));
                }
                var blink = removed ? 0 : 1;
                Assert.That(output.blendShapeCount, Is.EqualTo(blink + 1));
                Assert.That(output.GetBlendShapeIndex("Blink"), Is.EqualTo(blink));
                Assert.That(skin.GetBlendShapeWeight(blink), Is.EqualTo(42));
                Assert.That(avatar.customEyeLookSettings.eyelidsBlendshapes, Is.EqualTo(new[] { removed ? -1 : 0, blink, -1 }));
                if (removed) {
                    Assert.That(output.GetBlendShapeIndex("Kept"), Is.EqualTo(-1));
                    return;
                }
                Assert.That(output.GetBlendShapeName(0), Is.EqualTo("Kept"));
                Assert.That(output.GetBlendShapeFrameCount(0), Is.EqualTo(2));
                var v = new Vector3[4];
                var n = new Vector3[4];
                var t = new Vector3[4];
                for (var frame = 0; frame < 2; frame++) {
                    source.GetBlendShapeFrameVertices(0, frame, vertices, normals, tangents);
                    output.GetBlendShapeFrameVertices(0, frame, v, n, t);
                    Assert.That(output.GetBlendShapeFrameWeight(0, frame), Is.EqualTo(source.GetBlendShapeFrameWeight(0, frame)));
                    Assert.That(v, Is.EqualTo(vertices));
                    Assert.That(n, Is.EqualTo(normals));
                    Assert.That(t, Is.EqualTo(tangents));
                }
                Assert.That(source.bounds.size, Is.EqualTo(Vector3.one * 1000));
            } finally {
                if (output != null && output != source) Object.DestroyImmediate(output);
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(source);
            }
        }
        [TestCase(25, 50, 100)]
        [TestCase(50, 50, 100)]
        [TestCase(75, 50, 100)]
        [TestCase(95, 10, 85, 100)]
        [TestCase(125, 50, 100)]
        [TestCase(-25, 50, 100)]
        [TestCase(0, -50, 50)]
        [TestCase(25, 0, 50)]
        [TestCase(25, 50)]
        public void BakingStaticShapesMatchesUnityWithPosedBones(float weight, params int[] frameWeights) {
            var root = new GameObject("Avatar");
            var source = new Mesh {
                vertices = new[] { Vector3.zero, Vector3.right, Vector3.up },
                triangles = new[] { 0, 1, 2 },
                normals = Enumerable.Repeat(Vector3.forward, 3).ToArray(),
                tangents = Enumerable.Repeat(new Vector4(1, 0, 0, -1), 3).ToArray(),
                bindposes = new[] { Matrix4x4.identity },
                boneWeights = Enumerable.Repeat(new BoneWeight { weight0 = 1 }, 3).ToArray()
            };
            var before = new Mesh();
            var after = new Mesh();
            Mesh output = null;
            try {
                var avatar = root.AddComponent<VRCAvatarDescriptor>();
                var bone = new GameObject("Bone").transform;
                bone.SetParent(root.transform, false);
                bone.localPosition = Vector3.one;
                bone.localRotation = Quaternion.Euler(15, 30, 45);
                var skin = root.AddComponent<SkinnedMeshRenderer>();
                skin.bones = new[] { bone };
                for (var i = 0; i < frameWeights.Length; i++) {
                    var deltas = new Vector3[3];
                    deltas[i] = Vector3.up * (i + 1);
                    source.AddBlendShapeFrame("Static", frameWeights[i], deltas, deltas, deltas);
                }
                var offsets = new[] { Vector3.forward, Vector3.left, Vector3.down };
                source.AddBlendShapeFrame("Kept", 100, offsets, null, null);
                source.AddBlendShapeFrame("Other static", 100, offsets, null, null);
                skin.sharedMesh = source;
                skin.SetBlendShapeWeight(0, weight);
                skin.SetBlendShapeWeight(1, 30);
                skin.SetBlendShapeWeight(2, 20);
                avatar.lipSync = VRC_AvatarDescriptor.LipSyncStyle.JawFlapBlendShape;
                avatar.VisemeSkinnedMesh = skin;
                avatar.MouthOpenBlendShapeName = "Kept";

                skin.BakeMesh(before);
                Optimize(avatar);
                output = skin.sharedMesh;
                Assert.That(output.blendShapeCount, Is.EqualTo(1));
                Assert.That(output.GetBlendShapeName(0), Is.EqualTo("Kept"));
                skin.BakeMesh(after);
                for (var i = 0; i < source.vertexCount; i++) {
                    Assert.That(Vector3.Distance(before.vertices[i], after.vertices[i]), Is.LessThan(0.00001f));
                    Assert.That(Vector3.Distance(before.normals[i], after.normals[i]), Is.LessThan(0.00001f));
                    Assert.That(Vector4.Distance(before.tangents[i], after.tangents[i]), Is.LessThan(0.00001f));
                }
            } finally {
                Object.DestroyImmediate(root);
                if (output != null && output != source) Object.DestroyImmediate(output);
                Object.DestroyImmediate(source);
                Object.DestroyImmediate(before);
                Object.DestroyImmediate(after);
            }
        }

        private static void Optimize(VRCAvatarDescriptor avatar) {
            var injector = VRCFuryInjectorBuilder.GetInjector(avatar);
            var globals = injector.GetService<GlobalsService>();
            globals.allFeaturesInRun = new List<FeatureModel>();
            globals.allBuildersInRun = new List<FeatureBuilder>();
            injector.GetService<ControllersService>().LoadAllControllers();
            injector.CreateAndFillObject<BlendshapeOptimizerBuilder>().Apply();
        }
    }
}
