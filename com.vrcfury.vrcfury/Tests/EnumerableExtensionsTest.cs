using System.Linq;
using NUnit.Framework;
using UnityEngine;
using VF.Utils;

namespace VF.Tests {
    internal class EnumerableExtensionsTest {
        [Test]
        public void NotNullRespectsDestroyedUnityObjectsAndCustomNullSemantics() {
            var alive = new GameObject("Alive");
            var destroyed = new GameObject("Destroyed");
            var wrappedAlive = alive.asVf();
            var wrappedDestroyed = destroyed.asVf();
            Object.DestroyImmediate(destroyed);
            FloatOrObjectCurve nullCurve = (AnimationCurve)null;
            var managed = new object();
            try {
                var results = new object[] {
                    null,
                    alive,
                    destroyed,
                    wrappedAlive,
                    wrappedDestroyed,
                    nullCurve,
                    managed
                }.NotNull().ToArray();
                Assert.That(results.Length, Is.EqualTo(3));
                Assert.That(results[0], Is.SameAs(alive));
                Assert.That(results[1], Is.SameAs(wrappedAlive));
                Assert.That(results[2], Is.SameAs(managed));
            } finally {
                Object.DestroyImmediate(alive);
            }
        }
    }
}
