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

    internal class VRCFuryHapticPlugTest : SpsOptionsTestBase {
        [Test]
        public void PlugMigratesSettingsOnceAndKeepsLegacyDataIndependent() {
            var root = new GameObject("Plug channel migration");
            try {
                var plug = root.AddComponent<VRCFuryHapticPlug>();
#pragma warning disable 0612
                plug.useSharedTag = plug.useHipAvoidance = false;
                plug.includeTags.Add(new VRCFuryHapticPlug.TagRule { tag = "legacy", allowSelf = false });
#pragma warning restore 0612
                typeof(VRCFuryHapticPlug).VFField("tagsUpgradedToChannels").SetValue(plug, false);
                plug.Upgrade(10);
                Assert.That(plug.GetLatestVersion(), Is.EqualTo(10));
                var rule = plug.channelSettings.channels.Single();
                Assert.That(rule.id, Is.EqualTo("legacy"));
                Assert.That(rule.allowSelf, Is.False);
                Assert.That(rule.allowOthers, Is.True);
                Assert.That(plug.channelSettings.useSharedTag || plug.channelSettings.useHipAvoidance, Is.False);
                rule.id = "new";
#pragma warning disable 0612
                Assert.That(plug.includeTags.Single().tag, Is.EqualTo("legacy"));
#pragma warning restore 0612
                plug.channelSettings.channels.Clear();
                plug.channelSettings.useSharedTag = true;
                UnityEditor.EditorJsonUtility.FromJsonOverwrite(UnityEditor.EditorJsonUtility.ToJson(plug), plug);
                plug.Upgrade(10);
                Assert.That(plug.channelSettings.channels, Is.Empty);
                Assert.That(plug.channelSettings.useSharedTag, Is.True, "Later edits must survive repeated upgrades");
            } finally { Object.DestroyImmediate(root); }
        }
    }
}
