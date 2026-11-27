using NUnit.Framework;
using UnityEngine;
using VF.Feature;
using VF.Model.Feature;

namespace VF.Tests {
    internal class BlendShapeLinkBuilderTest {
        [TestCase(false)]
        [TestCase(true)]
        public void MatchesExactNamesBeforeUnambiguousLooseNames(bool exactMatch) {
            var root = new GameObject("Avatar");
            var baseMesh = new Mesh();
            var linkedMesh = new Mesh();
            try {
                var baseSkin = CreateSkin(
                    root,
                    "Base",
                    baseMesh,
                    "Direct",
                    "Blink Left",
                    "Smile"
                );
                var linkedSkin = CreateSkin(
                    root,
                    "Linked",
                    linkedMesh,
                    "Direct",
                    "d irect",
                    "blinkLEFT",
                    "SMILE",
                    "s mile"
                );
                var model = new BlendShapeLink {
                    baseObj = baseSkin.name,
                    exactMatch = exactMatch,
                    linkSkins = {
                        new BlendShapeLink.LinkSkin { renderer = linkedSkin }
                    }
                };
                var models = new[] { model };
                Assert.That(BlendShapeLinkBuilder.HandlesBlendshape(root, models, linkedSkin, "Direct"), Is.True);
                Assert.That(BlendShapeLinkBuilder.HandlesBlendshape(root, models, linkedSkin, "d irect"), Is.False);
                Assert.That(BlendShapeLinkBuilder.HandlesBlendshape(root, models, linkedSkin, "blinkLEFT"), Is.EqualTo(!exactMatch));
                Assert.That(BlendShapeLinkBuilder.HandlesBlendshape(root, models, linkedSkin, "SMILE"), Is.False);
                Assert.That(BlendShapeLinkBuilder.HandlesBlendshape(root, models, linkedSkin, "s mile"), Is.False);
            } finally {
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(baseMesh);
                Object.DestroyImmediate(linkedMesh);
            }
        }

        private static SkinnedMeshRenderer CreateSkin(
            GameObject root,
            string name,
            Mesh mesh,
            params string[] shapes
        ) {
            var skin = new GameObject(name).AddComponent<SkinnedMeshRenderer>();
            skin.transform.SetParent(root.transform);
            mesh.vertices = new[] { Vector3.zero };
            var delta = new[] { Vector3.right };
            foreach (var shape in shapes) mesh.AddBlendShapeFrame(shape, 100, delta, null, null);
            skin.sharedMesh = mesh;
            return skin;
        }
    }
}
