using NUnit.Framework;
using UnityEngine;
using VRC.SDK3.Dynamics.Contact;
using VRC.SDK3.Dynamics.Contact.Components;
using VRC.SDKBase.Validation.Performance.Stats;

namespace VF.Tests {
    public class ContactPerformanceStatsTest {
        [Test]
        public void EditorOnlyContactsDoNotCountTowardRankOrLimit() {
            var avatar = new GameObject("Avatar");
            try {
                avatar.AddComponent<VRCContactSender>();
                avatar.AddComponent<VRCContactReceiver>().localOnly = true;

                var editorOnly = new GameObject("Editor Only") { tag = "EditorOnly" };
                editorOnly.transform.SetParent(avatar.transform);
                editorOnly.AddComponent<VRCContactSender>();

                var child = new GameObject("Child");
                child.transform.SetParent(editorOnly.transform);
                child.AddComponent<VRCContactReceiver>();

                var stats = new AvatarPerformanceStats(false);
                new ContactPerformanceScanner().RunPerformanceScan(avatar, stats, _ => false);

                Assert.That(stats.contactCount, Is.EqualTo(1));
                Assert.That(stats.contactCompleteCount, Is.EqualTo(2));
            } finally {
                Object.DestroyImmediate(avatar);
            }
        }
    }
}
