namespace VF.Tests {
    using System.Linq;
    using NUnit.Framework;
    using UnityEngine;
    using VF.Utils;
    using VF.Utils.Controller;

    internal class VrcfObjectClonerTest {
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
