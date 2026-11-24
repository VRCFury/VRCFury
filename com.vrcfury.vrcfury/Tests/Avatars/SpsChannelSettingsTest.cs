namespace VF.Tests {
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using NUnit.Framework;
    using UnityEngine;
    using UnityEngine.UIElements;
    using VF.Builder;
    using VF.Builder.Haptics;
    using VF.Component;
    using VF.Inspector;
    using VF.Feature.Base;
    using VF.Injector;
    using VF.Model.Feature;
    using VF.Service;
    using VF.Utils;
    using VRC.SDK3.Avatars.Components;
    using VRC.SDK3.Dynamics.Contact.Components;
    using Object = UnityEngine.Object;

    internal class SpsChannelSettingsTest : SpsOptionsTestBase {
        [TestCase(false)]
        [TestCase(true)]
        public void FreshChannelSettingsAreNotOverwrittenByLegacyDefaults(bool isPlug) {
            var root = new GameObject("Fresh channels");
            try {
                VRCFuryComponent component = isPlug ? (VRCFuryComponent)root.AddComponent<VRCFuryHapticPlug>()
                    : root.AddComponent<VRCFuryHapticSocket>();
                var settings = isPlug ? ((VRCFuryHapticPlug)component).channelSettings : ((VRCFuryHapticSocket)component).channelSettings;
                settings.useSharedTag = settings.useHipAvoidance = false;
                settings.channels.Add(new VF.Model.SpsChannelSettings.Channel { id = "fresh" });
                component.Upgrade(component.GetLatestVersion());
                Assert.That(settings.useSharedTag || settings.useHipAvoidance, Is.False);
                Assert.That(settings.channels.Single().id, Is.EqualTo("fresh"));
            } finally { Object.DestroyImmediate(root); }
        }
    }
}
