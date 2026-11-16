using System.Linq;
using NUnit.Framework;
using UnityEngine;
using VF.Builder.Haptics;
using Object = UnityEngine.Object;

namespace VF.Tests {
    [Category("VRCFury")]
    public class SpsBakerTest {
        [Test]
        public void RadiusProfileIsAppendedWithoutMovingMeshOrBlendshapeData() {
            var obj = new GameObject("Baked profile");
            var mesh = new Mesh();
            Texture2D original = null;
            Texture2D profiled = null;
            try {
                mesh.vertices = new[] { Vector3.zero, Vector3.right, Vector3.up };
                mesh.normals = Enumerable.Repeat(Vector3.forward, 3).ToArray();
                mesh.triangles = new[] { 0, 1, 2 };
                mesh.AddBlendShapeFrame(
                    "Inflate",
                    100,
                    Enumerable.Repeat(Vector3.forward, 3).ToArray(),
                    new Vector3[3],
                    new Vector3[3]
                );
                var renderer = obj.AddComponent<SkinnedMeshRenderer>();
                renderer.sharedMesh = mesh;
                renderer.SetBlendShapeWeight(0, 25);
                var curve = new AnimationCurve(
                    new Keyframe(0, .1f),
                    new Keyframe(.3f, .15f),
                    new Keyframe(.9f, .05f),
                    new Keyframe(1, 0)
                );
                original = SpsBaker.Bake(
                    renderer,
                    obj.transform,
                    new[] { 0f, .75f, 1f },
                    new[] { "Inflate" },
                    AnimationCurve.Constant(0, 1, .2f),
                    .2f
                );
                profiled = SpsBaker.Bake(
                    renderer,
                    obj.transform,
                    new[] { 0f, .75f, 1f },
                    new[] { "Inflate" },
                    curve,
                    .2f
                );
                var before = original.GetRawTextureData<float>().ToArray();
                var after = profiled.GetRawTextureData<float>().ToArray();
                var offset = 1 + 3 * 10 + 1 + 3 * 9;
                Assert.That(before[0], Is.Zero);
                Assert.That(after[0], Is.Zero);
                Assert.That(after.Skip(1).Take(offset - 1), Is.EqualTo(before.Skip(1).Take(offset - 1)));
                for (var i = 0; i < SpsBaker.RadiusProfileSamples; i++) {
                    var depth = i / (SpsBaker.RadiusProfileSamples - 1f);
                    Assert.That(after[offset + i], Is.EqualTo(curve.Evaluate(depth) / .2f));
                }
                Assert.That(renderer.GetBlendShapeWeight(0), Is.EqualTo(25));
            } finally {
                Object.DestroyImmediate(original);
                Object.DestroyImmediate(profiled);
                Object.DestroyImmediate(obj);
                Object.DestroyImmediate(mesh);
            }
        }
    }
}
