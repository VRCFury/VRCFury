namespace VF.Tests {
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using NUnit.Framework;
    using UnityEditor;
    using UnityEditor.UIElements;
    using UnityEngine;
    using UnityEngine.UIElements;
    using VF.Builder;
    using VF.Feature;
    using VF.Inspector;
    using VF.Model;
    using VF.Model.Feature;
    using VF.Service;
    using VF.Utils;
    using Object = UnityEngine.Object;

    internal class ArmatureLinkBuilderTest : ObjectReferenceTestBase {
        [TestCase(false, false)]
        [TestCase(true, false)]
        [TestCase(false, true)]
        public void ArmatureShowsOffsetsForAllTargetsWhenRequestedOrAlreadyUsed(bool showOffset, bool existingOffset) {
            var owner = ScriptableObject.CreateInstance<Fields>();
            try {
                owner.targets.Add(new ObjectReference());
                owner.targets.Add(new ObjectReference { offset = existingOffset ? "Child" : "" });
                using (var serialized = new SerializedObject(owner)) {
                    var gui = ArmatureLinkBuilder.LinkTargetsEditor(serialized.FindProperty("targets"), showOffset);
                    var list = gui.Q<ListView>();
                    for (var i = 0; i < 2; i++) {
                        var row = list.makeItem();
                        list.bindItem(row, i);
                        Assert.That(row.Query<Label>().ToList().Any(label => label.text == "Offset Path"),
                            Is.EqualTo(showOffset || existingOffset));
                        list.unbindItem(row, i);
                    }
                }
            } finally {
                Object.DestroyImmediate(owner);
            }
        }

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(3)]
        public void ArmatureTargetsOnlyUseAListForMultipleOrMissingEntries(int count) {
            var owner = ScriptableObject.CreateInstance<Fields>();
            try {
                for (var i = 0; i < count; i++) owner.targets.Add(new ObjectReference());
                using (var serialized = new SerializedObject(owner)) {
                    var gui = ArmatureLinkBuilder.LinkTargetsEditor(serialized.FindProperty("targets"));
                    var list = gui.Q<ListView>();
                    Assert.That(list == null, Is.EqualTo(count == 1));
                    if (count == 1) {
                        Assert.That(gui.Query<PopupField<string>>().ToList()
                            .Count(field => field.choices.Contains("Object Reference")), Is.EqualTo(1));
                    } else {
                        var row = list.makeItem();
                        for (var i = 0; i < count; i++) {
                            list.bindItem(row, i);
                            Assert.That(row.Query<Label>().ToList()
                                .Where(label => label.text.StartsWith("Fallback #")).Select(label => label.text),
                                Is.EqualTo(i == 0 ? new string[0] : new[] { $"Fallback #{i}" }));
                            list.unbindItem(row, i);
                        }
                    }
                }
            } finally {
                Object.DestroyImmediate(owner);
            }
        }

        [Test]
        public void BoneOffsetsAndArmatureLinkShareTheCachedResolver() {
            var root = new GameObject("Avatar");
            var target = new GameObject("Target");
            var prop = new GameObject("Prop");
            target.transform.SetParent(root.transform);
            prop.transform.SetParent(root.transform);
            Avatar rig = null;
            try {
                var (humanoid, _, hips) = HumanoidTestRig.Create(root);
                rig = humanoid;
                target.transform.SetParent(hips, false);
                root.AddComponent<Animator>().avatar = rig;
                var bones = VRCFArmatureCache.GetPerFrame(root);
                var reference = new ObjectReference { offset = "Target" };
                var paths = VRCFObjectPathCache.GetPerFrame(root);
                Assert.That((GameObject)reference.Resolve(root, paths, bones), Is.SameAs(target));
                var model = new ArmatureLink { propBone = prop };
                model.linkTo.Clear();
                model.linkTo.Add(reference);
                var links = ArmatureLinkService.GetLinks(model, root, paths, bones);
                Assert.That((GameObject)links.mergeBones.Single().Item2, Is.SameAs(target));
            } finally {
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(rig);
            }
        }
    }
}
