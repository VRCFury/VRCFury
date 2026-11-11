using System.Linq;
using NUnit.Framework;
using UnityEngine;
using VF.Utils;
using VF.Utils.Controller;

namespace VF.Tests {
    [Category("VRCFury")]
    public class ObjectCloningTest {
        [System.Serializable]
        private sealed class ReferenceBranch {
            public Object target;
            public string label;
            public int[] numbers;
        }

        private sealed class ReferenceAsset : ScriptableObject {
            public string text;
            public float[] numbers;
            public Object[] references;
            public ReferenceBranch[] branches;
            public Object tail;
        }

        [Test]
        public void FastTraversalFindsAndRewritesNestedReferencesWhileSkippingValueArrays() {
            var owner = ScriptableObject.CreateInstance<ReferenceAsset>();
            var first = new GameObject("First");
            var second = new GameObject("Second");
            try {
                owner.text = "Do not enter string characters";
                owner.numbers = new[] { 1f, 2f, 3f };
                owner.references = new Object[] { null, first };
                owner.branches = new[] {
                    new ReferenceBranch {
                        target = second,
                        label = "Nested string",
                        numbers = new[] { 4, 5 }
                    },
                    new ReferenceBranch {
                        target = first,
                        label = "Empty array",
                        numbers = new int[0]
                    }
                };
                owner.tail = second;
                using var serialized = new UnityEditor.SerializedObject(owner);
                var expected = new System.Collections.Generic.Dictionary<string, Object>();
                var iterator = serialized.GetIterator();
                while (iterator.Next(true)) {
                    if (iterator.propertyType == UnityEditor.SerializedPropertyType.ObjectReference
                        && iterator.objectReferenceValue != null) expected[iterator.propertyPath] = iterator.objectReferenceValue;
                }
                var paths = serialized.IterateFast().Select(p => p.propertyPath).ToArray();
                Assert.That(paths, Does.Not.Contain("numbers.Array.data[0]"));
                Assert.That(paths, Does.Not.Contain("branches.Array.data[0].numbers.Array.data[0]"));
                var actual = new System.Collections.Generic.Dictionary<string, Object>();
                MutableManager.ForEachChildObjectReference(owner, (path, value, set) => {
                    actual[path] = value;
                    if (value == first) set(second);
                });
                Assert.That(actual, Is.EquivalentTo(expected));
                Assert.That(owner.references, Is.EqualTo(new Object[] { null, second }));
                Assert.That(owner.branches.Select(b => b.target), Is.All.SameAs(second));
                Assert.That(owner.tail, Is.SameAs(second));
            } finally {
                Object.DestroyImmediate(owner);
                Object.DestroyImmediate(first);
                Object.DestroyImmediate(second);
            }
        }

        [TestCase(0f, 0f, false, true, false, true)]
        [TestCase(0f, 1f, false, true, true, false)]
        [TestCase(0f, 1f, true, false, true, false)]
        [TestCase(1f, 2f, false, false, false, false)]
        [TestCase(0f, 2f, false, true, true, false)]
        [TestCase(1f, 1f, false, false, false, false)]
        public void ClipClassificationPreservesFloatAndObjectKeys(
            float firstTime,
            float lastTime,
            bool changes,
            bool isStatic,
            bool isTwoState,
            bool isEmptyOrZeroLength
        ) {
            var owner = new GameObject("Renderer");
            var first = new Material(Shader.Find("Standard"));
            var last = new Material(Shader.Find("Standard"));
            try {
                var renderer = owner.AddComponent<MeshRenderer>();
                foreach (var objects in new[] { false, true }) {
                    var clip = VFClip.Create("Classification");
                    var times = new[] { firstTime, lastTime }.Distinct().ToArray();
                    if (objects) {
                        clip.SetCurve(
                            renderer,
                            "m_Materials.Array.data[0]",
                            times.Select((time, i) => new UnityEditor.ObjectReferenceKeyframe {
                                time = time,
                                value = changes && i > 0 ? last : first
                            }).ToArray()
                        );
                    } else {
                        clip.SetAap(
                            "Value",
                            new AnimationCurve(times.Select((time, i) => new Keyframe(time, changes && i > 0 ? 2 : 1)).ToArray())
                        );
                    }
                    var before = clip.GetAllCurves().Single();
                    Assert.That(clip.IsStatic(), Is.EqualTo(isStatic));
                    Assert.That(clip.IsTwoState(), Is.EqualTo(isTwoState));
                    Assert.That(clip.IsEmptyOrZeroLength(), Is.EqualTo(isEmptyOrZeroLength));
                    var after = clip.GetAllCurves().Single();
                    Assert.That(after.Item1, Is.EqualTo(before.Item1));
                    if (objects) Assert.That(after.Item2.ObjectCurve, Is.EqualTo(before.Item2.ObjectCurve));
                    else Assert.That(after.Item2.FloatCurve.keys, Is.EqualTo(before.Item2.FloatCurve.keys));
                }
                var empty = VFClip.Create();
                Assert.That(empty.IsStatic(), Is.True);
                Assert.That(empty.IsTwoState(), Is.False);
                Assert.That(empty.IsEmptyOrZeroLength(), Is.True);
            } finally {
                Object.DestroyImmediate(owner);
                Object.DestroyImmediate(first);
                Object.DestroyImmediate(last);
            }
        }

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
