namespace VF.Tests {
    using System.Linq;
    using NUnit.Framework;
    using UnityEngine;
    using VF.Utils;
    using VF.Utils.Controller;

    internal class MutableManagerTest {
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
    }
}
