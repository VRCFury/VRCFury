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

    internal class VRCFuryHapticSocketTest : SpsOptionsTestBase {
        [Test]
        public void SocketMigratesTagsOnceWithoutChangingVersionOrRemovingOriginals() {
            var root = new GameObject("Socket channel migration");
            try {
                var socket = root.AddComponent<VRCFuryHapticSocket>();
#pragma warning disable 0612
                socket.tags.AddRange(new[] { "first", "second" });
                socket.useSharedTag = socket.useHipAvoidance = false;
                typeof(VRCFuryHapticSocket).VFField("tagsUpgradedToChannels").SetValue(socket, false);
                socket.Upgrade(8);
                Assert.That(socket.GetLatestVersion(), Is.EqualTo(8));
                Assert.That(socket.tags, Is.EqualTo(new[] { "first", "second" }));
                Assert.That(socket.channelSettings.channels.Select(r => r.id), Is.EqualTo(socket.tags));
                Assert.That(socket.channelSettings.channels.All(r => r.allowSelf && r.allowOthers), Is.True);
                Assert.That(socket.channelSettings.useSharedTag || socket.channelSettings.useHipAvoidance, Is.False);
                socket.channelSettings.channels.Clear();
                UnityEditor.EditorJsonUtility.FromJsonOverwrite(UnityEditor.EditorJsonUtility.ToJson(socket), socket);
                socket.Upgrade(8);
                Assert.That(socket.channelSettings.channels, Is.Empty, "Deleted rules must not be restored by another upgrade");
                Assert.That(socket.tags, Is.EqualTo(new[] { "first", "second" }));
#pragma warning restore 0612
            } finally { Object.DestroyImmediate(root); }
        }
    }
}
