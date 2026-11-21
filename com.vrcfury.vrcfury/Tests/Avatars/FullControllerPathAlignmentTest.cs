using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using VF.Builder;
using VF.Feature;
using VF.Model.Feature;
using VF.Service.Compressor;
using VF.Utils;
using VRC.SDK3.Avatars.Components;
using VRC.SDK3.Avatars.ScriptableObjects;
using Object = UnityEngine.Object;

namespace VF.Tests {
    [Category("VRCFury")]
    public class FullControllerPathAlignmentTest {
        private GameObject pc;
        private GameObject mobile;
        private readonly List<VRCExpressionParameters> assets = new List<VRCExpressionParameters>();

        [SetUp]
        public void SetUp() {
            pc = new GameObject("PC");
            mobile = new GameObject("Mobile");
        }

        [TearDown]
        public void TearDown() {
            Object.DestroyImmediate(pc);
            Object.DestroyImmediate(mobile);
            foreach (var asset in assets) Object.DestroyImmediate(asset);
            assets.Clear();
        }

        private static GameObject GetOwner(GameObject root, string path) {
            var owner = root.transform;
            foreach (var part in path.Split('/')) {
                var child = owner.Find(part);
                if (child == null) {
                    child = new GameObject(part).transform;
                    child.SetParent(owner, false);
                }
                owner = child;
            }
            return owner.gameObject;
        }

        private FullControllerBuilder Add(GameObject root, string path, params string[] names) {
            var asset = ScriptableObject.CreateInstance<VRCExpressionParameters>();
            assets.Add(asset);
            asset.parameters = names.Select(n => ExpressionParam.Create(n).Bool()).ToArray();
            var builder = new FullControllerBuilder {
                featureBaseObject = GetOwner(root, path),
                model = new FullController()
            };
            builder.model.prms.Add(new FullController.ParamsEntry { parameters = asset });
            return builder;
        }

        private void Align(FullControllerBuilder[] desktop, params FullControllerBuilder[] local) {
            var saved = FullControllerPathAlignment.Capture(pc, desktop);
            FullControllerPathAlignment.Align(mobile, local, saved);
        }

        [TestCase("Props/Hat Mobile", "Props/Hat", true)]
        [TestCase("Quest/Hat", "Props/Hat", true)]
        [TestCase("Quest/Cap", "Props/Hat", false)]
        [TestCase("Hat", "Props/Hat", false)]
        [TestCase("Props/Head/Hat", "Props/Hat", false)]
        public void OnlyOneSegmentCanChange(string localPath, string desktopPath, bool matches) {
            var desktop = Add(pc, desktopPath, "Color", "PC only");
            var local = Add(mobile, localPath, "Color");
            var obj = local.featureBaseObject;
            var parents = obj.Parents();
            var cache = VRCFuryInjectorBuilder.GetInjector(mobile.AddComponent<VRCAvatarDescriptor>()).GetService<VRCFObjectPathCache>();
            cache.Capture();
            Align(new[] { desktop }, local);
            Assert.That(obj.GetPath(mobile), Is.EqualTo(matches ? desktopPath : localPath));
            Assert.That(obj.Parents(), Is.EqualTo(parents));
            Assert.That(cache.Find(mobile, localPath), Is.EqualTo(obj));
        }

        [Test]
        public void ControllersMayAgreeToRenameASharedAncestor() {
            var a = Add(pc, "Prop/A", "One");
            var b = Add(pc, "Prop/B", "Two");
            var localA = Add(mobile, "Mobile Prop/A", "One");
            var localB = Add(mobile, "Mobile Prop/B", "Two");
            Align(new[] { a, b }, localB, localA);
            Assert.That(localA.featureBaseObject.GetPath(mobile), Is.EqualTo("Prop/A"));
            Assert.That(localB.featureBaseObject.GetPath(mobile), Is.EqualTo("Prop/B"));
        }

        [Test]
        public void AnExistingMatchOrOccupiedSiblingCannotBeDisplaced() {
            var exact = Add(pc, "Quest/A", "One");
            var desktop = Add(pc, "Prop/B", "Two");
            var localA = Add(mobile, "Quest/A", "One");
            var localB = Add(mobile, "Quest/B", "Two");
            Align(new[] { exact, desktop }, localA, localB);
            Assert.That(localB.featureBaseObject.GetPath(mobile), Is.EqualTo("Quest/B"));

            Add(mobile, "Prop/Unrelated", "Other");
            Align(new[] { desktop }, localB);
            Assert.That(localB.featureBaseObject.GetPath(mobile), Is.EqualTo("Quest/B"));
        }

        [Test]
        public void AnExactMatchReservesItsDesktopControllerBeforeFallback() {
            var a = Add(pc, "Prop A", "Color");
            var b = Add(pc, "Prop B", "Color");
            var localA = Add(mobile, "Prop A", "Color");
            var localB = Add(mobile, "Quest", "Color");
            Align(new[] { a, b }, localA, localB);
            Assert.That(localA.featureBaseObject.name, Is.EqualTo("Prop A"));
            Assert.That(localB.featureBaseObject.name, Is.EqualTo("Prop B"));
        }

        [Test]
        public void CompetingMatchesInEitherDirectionAreSkipped() {
            var a = Add(pc, "Prop A", "Color");
            var b = Add(pc, "Prop B", "Color");
            var local = Add(mobile, "Quest", "Color");
            Align(new[] { a, b }, local);
            Assert.That(local.featureBaseObject.name, Is.EqualTo("Quest"));
            var second = Add(mobile, "Quest 2", "Color");
            Align(new[] { a }, local, second);
            Assert.That(local.featureBaseObject.name, Is.EqualTo("Quest"));
            Assert.That(second.featureBaseObject.name, Is.EqualTo("Quest 2"));
        }

        [Test]
        public void OnlyPrivateSyncedParametersProvideMatchingEvidence() {
            var desktop = Add(pc, "Prop", "Color", "Global", "Local");
            desktop.model.globalParams.Add("Global");
            assets.Single().parameters.Last().Unsynced();
            var saved = FullControllerPathAlignment.Capture(pc, new[] { desktop });
            Assert.That(saved.Single().parameters, Is.EqualTo(new[] { "Color" }));
            var local = Add(mobile, "Quest", "Global");
            local.model.globalParams.Add("Global");
            Align(new[] { desktop }, local);
            Assert.That(local.featureBaseObject.name, Is.EqualTo("Quest"));
        }

        private static ToggleBuilder AddToggle(GameObject root, string path, string menuPath, bool global = false) =>
            new ToggleBuilder {
                featureBaseObject = GetOwner(root, path),
                model = new Toggle { name = menuPath, useGlobalParam = global, globalParam = global ? "Shared" : "" }
            };

        [TestCase(false)]
        [TestCase(true)]
        public void OnlyPrivateToggleMenuPathsProvideMatchingEvidence(bool global) {
            var desktop = AddToggle(pc, "Props/Hat", "Clothes/Hat", global);
            var local = AddToggle(mobile, "Props/Quest Hat", "Clothes/Hat", global);
            var controllers = Array.Empty<FullControllerBuilder>();
            var saved = new ParameterPlatformAlignmentService.SavedData {
                fullControllers = FullControllerPathAlignment.Capture(pc, controllers, new[] { desktop })
            };
            saved = JsonUtility.FromJson<ParameterPlatformAlignmentService.SavedData>(JsonUtility.ToJson(saved));
            FullControllerPathAlignment.Align(mobile, controllers, saved.fullControllers, new[] { local });

            Assert.That(local.featureBaseObject.name, Is.EqualTo(global ? "Quest Hat" : "Hat"));
        }

        [Test]
        public void MenuPathsDisambiguateControllersWithTheSameParameters() {
            var hat = Add(pc, "Props/Hat", "Color");
            var coat = Add(pc, "Props/Coat", "Color");
            var local = Add(mobile, "Props/Quest", "Color");
            var hatToggle = AddToggle(pc, "Props/Hat", "Clothes/Hat");
            var coatToggle = AddToggle(pc, "Props/Coat", "Clothes/Coat");
            var localToggle = AddToggle(mobile, "Props/Quest", "Clothes/Hat");
            var mobileOnly = AddToggle(mobile, "Props/Quest", "Clothes/Mobile only");
            var saved = FullControllerPathAlignment.Capture(pc, new[] { hat, coat }, new[] { hatToggle, coatToggle });

            FullControllerPathAlignment.Align(mobile, new[] { local }, saved, new[] { localToggle, mobileOnly });

            Assert.That(local.featureBaseObject.name, Is.EqualTo("Hat"));
        }
    }
}
