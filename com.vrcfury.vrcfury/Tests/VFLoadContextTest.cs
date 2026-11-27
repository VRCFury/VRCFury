using NUnit.Framework;
using UnityEngine;
using VF.Builder;
using VF.Utils;
using VF.Utils.Controller;

namespace VF.Tests {
    internal class VFLoadContextTest {
        [Test]
        public void BindingsWithTheSamePathCanResolveToDifferentComponents() {
            var avatar = new GameObject("Avatar");
            try {
                var prop = new GameObject("Prop");
                prop.transform.SetParent(avatar.transform);
                var onProp = new GameObject("Shared");
                onProp.transform.SetParent(prop.transform);
                var onAvatar = new GameObject("Shared");
                onAvatar.transform.SetParent(avatar.transform);
                onAvatar.AddComponent<BoxCollider>();

                var context = new VFLoadContext {
                    AnimatorObject = avatar,
                    OwnerObject = prop,
                    ObjectPaths = new VRCFObjectPathCache()
                };
                Assert.That(context.ResolveTarget("Shared", typeof(Transform)), Is.EqualTo(onProp.asVf()));
                Assert.That(context.ResolveTarget("Shared", typeof(BoxCollider)), Is.EqualTo(onAvatar.asVf()));
                Assert.That(context.ResolveTarget("Shared", typeof(SphereCollider)), Is.Null);
                Assert.That(context.ResolveTarget("Shared", typeof(Transform)), Is.EqualTo(onProp.asVf()));
            } finally {
                Object.DestroyImmediate(avatar);
            }
        }
    }
}
