#if VRCSDK_HAS_ACTIVE_BUILD_TYPE
using NUnit.Framework;
using UnityEngine;
using VF.Exceptions;
using VF.Service;
using VRC.Core;
using VRC.SDKBase.Editor;

namespace VF.Tests {
    [Category("VRCFury")]
    public class PipelineIdServiceTest {
        [TestCase(null)]
        [TestCase("")]
        [TestCase(" ")]
        public void GetRequiresAnIdOnlyWhenPublishing(string missingId) {
            var avatar = new GameObject("Test avatar");
            var previousBuildType = VRC_SdkBuilder.ActiveBuildType;
            try {
                if (missingId != null) avatar.AddComponent<PipelineManager>().blueprintId = missingId;
                VRC_SdkBuilder.ActiveBuildType = VRC_SdkBuilder.BuildType.Publish;

                var service = new PipelineIdService(avatar);
                var error = Assert.Throws<VRCFBuilderException>(() => service.Get());
                StringAssert.Contains("no Blueprint ID", error.Message);

                VRC_SdkBuilder.ActiveBuildType = VRC_SdkBuilder.BuildType.Test;
                Assert.That(service.Get(), Is.Null);
            } finally {
                VRC_SdkBuilder.ActiveBuildType = previousBuildType;
                Object.DestroyImmediate(avatar);
            }
        }
    }
}
#endif
