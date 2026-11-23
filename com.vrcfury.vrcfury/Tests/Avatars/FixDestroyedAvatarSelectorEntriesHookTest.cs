using System.Collections.Generic;
using System.Runtime.Serialization;
using NUnit.Framework;
using UnityEngine;
using VF.Utils;
using VRC.SDK3.Avatars.Components;
using VRC.SDKBase;

namespace VF.Tests {
    [Category("VRCFury")]
    public class FixDestroyedAvatarSelectorEntriesHookTest {
        [Test]
        public void StaleChoicesDoNotBreakAvatarNames() {
            var selectorType = ReflectionUtils.GetTypeFromAnyAssembly("VRC.SDK3A.Editor.Elements.AvatarSelector");
            var format = selectorType.VFMethod("FormatAvatarName");
            // The formatter only uses its arguments. Skip constructing the SDK popup and its subscriptions.
            var selector = FormatterServices.GetUninitializedObject(selectorType);
            var root = new GameObject("Avatar Selector Test");
            try {
                VRCAvatarDescriptor CreateAvatar() {
                    var obj = new GameObject("Avatar");
                    obj.transform.SetParent(root.transform);
                    return obj.AddComponent<VRCAvatarDescriptor>();
                }

                var first = CreateAvatar();
                var second = CreateAvatar();
                var destroyed = CreateAvatar();
                var choices = new List<VRC_AvatarDescriptor> {
                    first,
                    destroyed,
                    second
                };
                Object.DestroyImmediate(destroyed.gameObject);

                object Format(VRC_AvatarDescriptor choice) => format.Invoke(selector, new object[] { choices, choice });
                Assert.That(Format(first), Is.EqualTo("Avatar"));
                Assert.That(Format(second), Is.EqualTo("Avatar [1]"));
                Assert.That(Format(destroyed), Is.EqualTo("(Missing Avatar)"));
                Assert.That(choices.Count, Is.EqualTo(3));
            } finally {
                Object.DestroyImmediate(root);
            }
        }
    }
}
