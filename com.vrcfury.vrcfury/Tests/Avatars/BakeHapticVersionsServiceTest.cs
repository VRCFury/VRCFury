namespace VF.Tests {
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;
    using NUnit.Framework;
    using UnityEngine;
    using VF.Builder;
    using VF.Service;
    using VF.Service.Compressor;
    using VF.Utils;
    using VRC.SDK3.Avatars.Components;
    using VRC.SDK3.Avatars.ScriptableObjects;
    using Object = UnityEngine.Object;
    using VF.Utils.Controller;

    internal class BakeHapticVersionsServiceTest : ExpressionParameterTestBase {
        [TestCase("plug", true)]
        [TestCase("socket", true)]
        [TestCase("touch", true)]
        [TestCase("none", false)]
        public void DisabledHapticsMarkerRequiresARequestedHapticFeature(string feature, bool requested) {
            var root = new GameObject("Haptics availability");
            try {
                var avatar = root.AddComponent<VRCAvatarDescriptor>();
                if (feature == "plug") root.AddComponent<VF.Component.VRCFuryHapticPlug>();
                if (feature == "socket") root.AddComponent<VF.Component.VRCFuryHapticSocket>();
                if (feature == "touch") root.AddComponent<VF.Component.VRCFuryHapticTouchReceiver>();
                var injector = VRCFuryInjectorBuilder.GetInjector(avatar);
                injector.GetService<BakeHapticVersionsService>().Apply();
                var marker = avatar.expressionParameters?.FindParameter("OGB/HapticsDisabled");
                Assert.That(marker != null, Is.EqualTo(requested && !VF.Menu.HapticsToggleMenuItem.Get()));
                if (marker != null) {
                    Assert.That(marker.valueType, Is.EqualTo(VRCExpressionParameters.ValueType.Bool));
                    Assert.That(marker.defaultValue, Is.EqualTo(1));
                    Assert.That(marker.IsNetworkSynced(), Is.False);
                    Assert.That(marker.saved, Is.False);
                }
                Assert.That(avatar.expressionParameters?.FindParameter("OGB/Version/11") != null, Is.EqualTo(requested));
            } finally {
                Object.DestroyImmediate(root);
            }
        }
    }
}
