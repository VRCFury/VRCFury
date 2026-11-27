using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using VF.Builder.Haptics;
using VF.Component;
using VRC.SDK3.Avatars.Components;

namespace VF.Tests {
    internal class PlugRendererFinderTest {
        [Test]
        public void AutoRendererSelectionCountsEachVertexOnceAndObservesChangedWeights() {
            var root = new GameObject("Avatar");
            var meshes = new List<Mesh>();
            try {
                root.AddComponent<VRCAvatarDescriptor>();
                var plugObject = new GameObject("Plug");
                plugObject.transform.SetParent(root.transform, false);
                var childBone = new GameObject("Child bone").transform;
                childBone.SetParent(plugObject.transform, false);
                var plug = plugObject.AddComponent<VRCFuryHapticPlug>();
                plug.autoRenderer = true;
                plug.useLegacyRendererFinder = false;

                SkinnedMeshRenderer Skin(string name, BoneWeight[] weights) {
                    var obj = new GameObject(name);
                    obj.transform.SetParent(root.transform, false);
                    var mesh = new Mesh { vertices = new Vector3[weights.Length], boneWeights = weights };
                    meshes.Add(mesh);
                    var skin = obj.AddComponent<SkinnedMeshRenderer>();
                    skin.sharedMesh = mesh;
                    skin.bones = new[] { childBone, root.transform, plugObject.transform };
                    return skin;
                }

                var outside = new BoneWeight { boneIndex0 = 1, weight0 = 1 };
                var oneMatch = new BoneWeight {
                    boneIndex0 = 1,
                    weight0 = 0.75f,
                    boneIndex1 = 0,
                    weight1 = 0.25f
                };
                var twoMatches = new BoneWeight {
                    boneIndex0 = 0,
                    weight0 = 0.5f,
                    boneIndex1 = 2,
                    weight1 = 0.5f
                };
                var oneVertex = Skin("One vertex", new[] { twoMatches, outside, outside });
                var twoVertices = Skin("Two vertices", new[] { oneMatch, oneMatch, outside });
                twoVertices.gameObject.SetActive(false);

                Assert.That(PlugRendererFinder.GetRenderers(plug), Is.EquivalentTo(new[] { twoVertices }));
                twoVertices.sharedMesh.boneWeights = new[] { outside, outside, outside };
                Assert.That(PlugRendererFinder.GetRenderers(plug), Is.EquivalentTo(new[] { oneVertex }));
            } finally {
                Object.DestroyImmediate(root);
                foreach (var mesh in meshes) Object.DestroyImmediate(mesh);
            }
        }
    }
}
