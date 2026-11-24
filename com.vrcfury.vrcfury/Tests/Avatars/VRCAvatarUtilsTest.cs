using NUnit.Framework;
using UnityEngine;
using VF.Builder;
using VRC.SDK3.Avatars.Components;

namespace VF.Tests {
    internal class VRCAvatarUtilsTest {
        [TestCase(false)]
        [TestCase(true)]
        public void GetAllControllersSkipsUninitializedLayerArrays(bool missingBaseLayers) {
            var root = new GameObject("Avatar");
            try {
                var avatar = root.AddComponent<VRCAvatarDescriptor>();
                var layerType = missingBaseLayers
                    ? VRCAvatarDescriptor.AnimLayerType.Sitting
                    : VRCAvatarDescriptor.AnimLayerType.FX;
                var layers = new[] {
                    new VRCAvatarDescriptor.CustomAnimLayer { type = layerType }
                };
                avatar.baseAnimationLayers = missingBaseLayers ? null : layers;
                avatar.specialAnimationLayers = missingBaseLayers ? layers : null;

                var controllers = VRCAvatarUtils.GetAllControllers(avatar);

                Assert.That(controllers.Count, Is.EqualTo(1));
                Assert.That(controllers[0].type, Is.EqualTo(layerType));
                Assert.That(missingBaseLayers ? avatar.baseAnimationLayers : avatar.specialAnimationLayers, Is.Null);
            } finally {
                Object.DestroyImmediate(root);
            }
        }
    }
}
