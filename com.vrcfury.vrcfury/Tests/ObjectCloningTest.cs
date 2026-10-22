using NUnit.Framework;
using UnityEngine;
using VF.Utils;

namespace VF.Tests {
    [Category("VRCFury")]
    public class ObjectCloningTest {
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
