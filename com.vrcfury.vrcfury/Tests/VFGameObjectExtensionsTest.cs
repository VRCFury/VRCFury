using NUnit.Framework;
using UnityEngine;
using VF.Model;
using VF.Utils;

namespace VF.Tests {
    internal class VFGameObjectExtensionsTest {
        [Test]
        public void OwnerUsesVrcfuryComponentOverride() {
            var root = new GameObject("Root");
            var other = new GameObject("Override");
            try {
                var component = root.AddComponent<VRCFury>();
                Assert.That(component.owner(), Is.EqualTo(root.asVf()));

                component.gameObjectOverride = other;
                Assert.That(component.owner(), Is.EqualTo(other.asVf()));
                Assert.That(root.transform.owner(), Is.EqualTo(root.asVf()));
            } finally {
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(other);
            }
        }
    }
}
