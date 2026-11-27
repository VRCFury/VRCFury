using System;
using NUnit.Framework;
using UnityEngine;
using VF.Feature;
using VF.Feature.Base;
using VF.Utils;
using VRC.SDK3.Avatars.Components;
using Object = UnityEngine.Object;

namespace VF.Tests {
    internal class VRCFuryAvatarHookTest {
        [Test]
        public void OnlyRootFeaturesRequireACleanChildHierarchy() {
            var avatar = new GameObject("Avatar");
            try {
                avatar.AddComponent<VRCAvatarDescriptor>();
                var featureGroup = new GameObject("Features");
                featureGroup.transform.SetParent(avatar.transform);
                var featureOwner = new GameObject("Feature");
                featureOwner.transform.SetParent(featureGroup.transform);
                var validate = FeatureFinder.onGetBuilder;

                Assert.DoesNotThrow(() => validate(featureOwner.asVf(), typeof(SpsOptionsBuilder), "SPS Options"));
                featureGroup.AddComponent<BoxCollider>();
                Assert.That(
                    () => validate(featureOwner.asVf(), typeof(SpsOptionsBuilder), "SPS Options"),
                    Throws.TypeOf<Exception>().With.Message.Contains("Features/Feature")
                );
                Assert.DoesNotThrow(() => validate(featureOwner.asVf(), typeof(ToggleBuilder), "Toggle"));
                Assert.DoesNotThrow(() => validate(avatar.asVf(), typeof(SpsOptionsBuilder), "SPS Options"));
            } finally {
                Object.DestroyImmediate(avatar);
            }
        }
    }
}
