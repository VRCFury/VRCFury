using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using VF.Utils;

namespace VF.Tests {
    internal class VFBindingTest {
        [Test]
        public void HashUsesTheResolvedTargetInsteadOfTheOriginalClipPath() {
            var target = new GameObject("Target");
            try {
                var first = VFBinding.From(
                    new VFResolvedObject(target, "Before", null, true),
                    EditorCurveBinding.FloatCurve("Before", typeof(Transform), "m_LocalPosition.x")
                );
                var second = VFBinding.From(
                    new VFResolvedObject(target, "After", null, true),
                    EditorCurveBinding.FloatCurve("After", typeof(Transform), "m_LocalPosition.x")
                );
                var bindings = new HashSet<VFBinding> { first };
                Assert.That(bindings.Contains(second), Is.True);
                Assert.That(bindings.Contains(second.WithPropertyName("m_LocalPosition.y")), Is.False);
            } finally {
                Object.DestroyImmediate(target);
            }
        }

        [Test]
        public void UnresolvedBindingsKeepTheirSeparatePaths() {
            var raw = EditorCurveBinding.FloatCurve("Original", typeof(Transform), "m_LocalPosition.x");
            var first = VFBinding.From(new VFResolvedObject(null, "Original", "One"), raw);
            var second = VFBinding.From(new VFResolvedObject(null, "Original", "Two"), raw);
            var equivalent = first.WithPath("One");
            var bindings = new HashSet<VFBinding> { first, second };
            Assert.That(bindings.Count, Is.EqualTo(2));
            Assert.That(bindings.Contains(equivalent), Is.True);
        }

        [Test]
        public void CachedOutsideTargetUsesEachBindingsOwnFallbackPath() {
            var root = new GameObject("Avatar");
            var target = new GameObject("Outside root");
            try {
                var paths = new Dictionary<VFGameObject, string>();
                var raw = EditorCurveBinding.FloatCurve("Original", typeof(Transform), "m_LocalPosition.x");
                var first = VFBinding.From(new VFResolvedObject(target, "Original", "One", true), raw);
                var second = VFBinding.From(new VFResolvedObject(target, "Original", "Two", true), raw);
                Assert.That(first.GetPath(root, paths), Is.EqualTo("One"));
                Assert.That(second.GetPath(root, paths), Is.EqualTo("Two"));
                Assert.That(paths.Count, Is.EqualTo(1));
            } finally {
                Object.DestroyImmediate(target);
                Object.DestroyImmediate(root);
            }
        }
    }
}
