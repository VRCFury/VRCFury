using NUnit.Framework;
using UnityEngine;
using VF.Builder.Haptics;
using VF.Utils;

namespace VF.Tests {
    internal class SpsUpgradeContextTest {
        [Test]
        public void BlocksRelatedTargetsAndTargetsInBranchesBeingRemoved() {
            var root = new GameObject("Avatar");
            try {
                var parent = new GameObject("Parent");
                parent.transform.SetParent(root.transform);
                var child = new GameObject("Child");
                child.transform.SetParent(parent.transform);
                child.SetActive(false);
                var sibling = new GameObject("Sibling");
                sibling.transform.SetParent(root.transform);
                var context = new SpsUpgradeContext(root, true, _ => false);

                Assert.That(context.AlreadyExistsAboveOrBelow(parent, new[] { child.asVf() }), Is.True);
                Assert.That(context.AlreadyExistsAboveOrBelow(child, new[] { parent.asVf() }), Is.True);
                Assert.That(context.AlreadyExistsAboveOrBelow(child, new[] { child.asVf() }), Is.True);
                Assert.That(context.AlreadyExistsAboveOrBelow(sibling, new[] { child.asVf() }), Is.False);

                context = new SpsUpgradeContext(root, true, obj => obj == parent.asVf());
                Assert.That(context.AlreadyExistsAboveOrBelow(child, new VFGameObject[0]), Is.True);
            } finally {
                Object.DestroyImmediate(root);
            }
        }
    }
}
