using System.Linq;
using NUnit.Framework;
using UnityEngine;
using VF.Utils;
using VF.Utils.Controller;

namespace VF.Tests {
    [Category("VRCFury")]
    public class ObjectCloningTest {
        [Test]
        public void FilteredClipCurvesAreIndependentSnapshots() {
            var first = new GameObject("First");
            var second = new GameObject("Second");
            var original = new Material(Shader.Find("Standard"));
            var replacement = new Material(Shader.Find("Standard"));
            try {
                var renderer = first.AddComponent<MeshRenderer>();
                var clip = VFClip.Create("Filtered curves");
                clip.SetCurve(first.transform, "m_LocalPosition.x", AnimationCurve.Constant(0, 0, 1));
                clip.SetCurve(second.transform, "m_LocalPosition.x", AnimationCurve.Constant(0, 0, 2));
                clip.SetAap("Unrelated", 3);
                clip.SetCurve(renderer, "m_Materials.Array.data[0]", new[] {
                    new UnityEditor.ObjectReferenceKeyframe { time = 0, value = original }
                });
                var snapshot = clip.GetAllCurves(binding => binding.Targets(first));
                Assert.That(snapshot, Has.Length.EqualTo(2));
                Assert.That(clip.GetAllCurves(), Has.Length.EqualTo(4));
                Assert.That(clip.GetAllCurves(_ => false), Is.Empty);

                var floatPair = snapshot.Single(pair => pair.Item2.IsFloat);
                var objectPair = snapshot.Single(pair => !pair.Item2.IsFloat);
                floatPair.Item2.FloatCurve.AddKey(1, 7);
                objectPair.Item2.ObjectCurve[0].value = replacement;
                Assert.That(clip.GetFloatCurve(floatPair.Item1).length, Is.EqualTo(1));
                Assert.That(clip.GetObjectCurve(objectPair.Item1)[0].value, Is.SameAs(original));
                clip.SetCurve(floatPair.Item1, 8);
                Assert.That(floatPair.Item2.FloatCurve.Evaluate(0), Is.EqualTo(1));
            } finally {
                Object.DestroyImmediate(first);
                Object.DestroyImmediate(second);
                Object.DestroyImmediate(original);
                Object.DestroyImmediate(replacement);
            }
        }

        [TestCase(false)]
        [TestCase(true)]
        public void OwnedMeshIsReusedUnlessCloningIsForced(bool forceClone) {
            var original = VrcfObjectFactory.Create<Mesh>();
            Mesh copy = null;
            try {
                copy = original.Clone("Clone test", forceClone: forceClone);
                Assert.That(copy == original, Is.EqualTo(!forceClone));
                Assert.That(VrcfObjectFactory.DidCreate(copy), Is.True);
                Assert.That(copy.GetWorkLog(), Does.Contain("Clone test"));
            } finally {
                if (copy != original) Object.DestroyImmediate(copy);
                Object.DestroyImmediate(original);
            }
        }
    }
}
