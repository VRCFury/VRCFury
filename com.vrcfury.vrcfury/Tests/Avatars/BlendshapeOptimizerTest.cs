using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using VF.Builder;
using VF.Feature;
using VF.Feature.Base;
using VF.Model.Feature;
using VF.Service;
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
                var injector = VRCFuryInjectorBuilder.GetInjector(avatar);
                var globals = injector.GetService<GlobalsService>();
                globals.allFeaturesInRun = new List<FeatureModel>();
                globals.allBuildersInRun = new List<FeatureBuilder>();
                injector.GetService<ControllersService>().LoadAllControllers();
                injector.CreateAndFillObject<BlendshapeOptimizerBuilder>().Apply();
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
    }
}
