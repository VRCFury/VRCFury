using NUnit.Framework;
using UnityEngine;
using VF.Exceptions;

namespace VF.Tests {
    public class VRCFArmatureCacheTest {
        [Test]
        public void HipsLookupDoesNotFallBackToNestedClothes() {
            var root = new GameObject("Avatar");
            Avatar avatar = null;
            try {
                var (rig, armature, hips) = HumanoidTestRig.Create(root);
                avatar = rig;
                var clothes = new GameObject("Clothes").transform;
                clothes.SetParent(root.transform, false);
                clothes.SetAsFirstSibling();
                var clothesArmature = Object.Instantiate(armature.gameObject, clothes);
                clothesArmature.name = armature.name;
                var clothesHips = clothesArmature.transform.Find("Pelvis");

                // Leave a matching skeleton under Clothes while the original bone path is missing.
                armature.name = "Renamed Armature";
                var animator = root.AddComponent<Animator>();
                animator.avatar = avatar;
                Assert.That(animator.GetBoneTransform(HumanBodyBones.Hips), Is.EqualTo(clothesHips));

                var cache = HumanoidTestRig.CreateCache(root);
                Assert.That(
                    Assert.Throws<VRCFBuilderException>(() => cache.FindBoneOnArmatureOrException(HumanBodyBones.Hips)).Message,
                    Does.Contain("Armature/Pelvis")
                );

                armature.name = "Armature";
                cache.Capture();
                Assert.That((Transform)cache.FindBoneOnArmatureOrException(HumanBodyBones.Hips), Is.EqualTo(hips));

                armature.name = "Renamed Armature";
                cache.Capture();
                Assert.That(cache.FindBoneOnArmatureOrNull(HumanBodyBones.Hips), Is.Null);
                Assert.That(cache.GetAllBones().ContainsKey(HumanBodyBones.Hips), Is.False);
            } finally {
                Object.DestroyImmediate(root);
                if (avatar != null) Object.DestroyImmediate(avatar);
            }
        }
    }
}
