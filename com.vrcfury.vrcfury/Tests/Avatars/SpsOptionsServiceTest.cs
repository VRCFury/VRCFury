#if VRCSDK_HAS_CONTACT_PARAMETER_FLAGS
namespace VF.Tests {
    using static VF.Inspector.VRCFuryHapticPlugEditor;
    using Party = VF.Builder.Haptics.SelfAndOthersReceiverService.Party;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;
    using NUnit.Framework;
    using UnityEditor.Animations;
    using UnityEngine;
    using UnityEngine.Animations;
    using UnityEngine.Playables;
    using VF.Builder;
    using VF.Builder.Haptics;
    using VF.Component;
    using VF.Injector;
    using VF.Inspector;
    using VF.Model.Feature;
    using VF.Service;
    using VF.Utils;
    using VF.Utils.Controller;
    using VRC.SDK3.Avatars.Components;
    using VRC.SDK3.Dynamics.Contact.Components;
    using VRC.Dynamics;
    using VRC.SDKBase;
    using Object = UnityEngine.Object;

    internal class SpsOptionsServiceTest : SpsAvatarBuildTestBase {
        [TestCase(false, false)]
        [TestCase(false, true)]
        [TestCase(true, false)]
        [TestCase(true, true)]
        public void AvatarBuildAppliesLegacySocketCompatibilityIndependentlyOfLegacyMode(bool enabled, bool legacyAvailable) {
            if (!BuildTargetUtils.IsDesktop()) Assert.Ignore("Contact senders are desktop-only.");
            injector.Set("componentObject", new Func<VFGameObject>(() => root));
            var globals = injector.GetService<GlobalsService>();
            globals.allBuildersInRun = new List<VF.Feature.Base.FeatureBuilder>();
            globals.allFeaturesInRun = new List<FeatureModel> {
                new SpsOptions { legacySocketCompatibility = enabled, legacyModeAvailable = legacyAvailable }
            };
            globals.addOtherFeature = _ => { };
            var plug = root.AddComponent<VRCFuryHapticPlug>();
            plug.autoRenderer = plug.autoLength = plug.autoRadius = plug.enableSps = false;
            plug.unitsInMeters = true;
            plug.length = 0.4f;
            plug.radius = 0.04f;
            injector.GetService<BakeHapticPlugsService>().ApplyEarlyBake();
            var main = injector.GetService<SpsPlugReceiverService>().GetReceivers(plug);
            receiverAnimator.GetParameter(main, Party.Merged);
            injector.GetService<ChangeSpsChannelsService>().Apply();
            var senders = root.GetComponentsInChildren<VRCContactSender>(true);
            Assert.That(senders.Single(s => s.name == "Sender - Sphere").collisionTags.Contains(SpsTags.LegacyPlugSphere), Is.EqualTo(enabled));
            Assert.That(senders.Count(s => s.name == "Sender - Legacy Width" || s.name == "Sender - Legacy Root"), Is.EqualTo(enabled ? 2 : 0));
            foreach (var receiver in root.GetComponentsInChildren<VRCContactReceiver>(true)) {
                foreach (var tag in new[] { SpsTags.LegacySocketHole, SpsTags.LegacySocketRing, SpsTags.LegacySocket }) {
                    Assert.That(receiver.collisionTags.Contains(tag), Is.EqualTo(enabled && receiver.allowOthers), receiver.name + ": " + tag);
                }
            }
        }

        [TestCase(false, false, true, true)]
        [TestCase(false, true, true, true)]
        [TestCase(true, false, true, true)]
        [TestCase(true, true, true, true)]
        [TestCase(false, false, false, true)]
        [TestCase(true, false, false, true)]
        [TestCase(false, true, true, false)]
        [TestCase(true, true, true, false)]
        [TestCase(true, false, true, true, true)]
        [TestCase(true, true, true, true, true)]
        public void LegacyToggleControlsSocketLightsTpsFrontAndDpsTipIncludingPlugOnlyAvatars(bool withSocket, bool defaultOn, bool available, bool shared, bool legacyServiceFirst = false) {
            if (!BuildTargetUtils.IsDesktop()) Assert.Ignore("Requires desktop SPS baking");
            injector.Set("componentObject", new Func<VFGameObject>(() => root));
            var globals = injector.GetService<GlobalsService>();
            globals.allBuildersInRun = new List<VF.Feature.Base.FeatureBuilder>();
            globals.allFeaturesInRun = new List<FeatureModel> {
                new SpsOptions { autoModeAvailable = withSocket, legacyModeAvailable = available,
                    legacyModeEnabledOnAvatarLoad = defaultOn, addDpsTipLight = available, tpsDepthAnimationCompatibility = available }
            };
            globals.addOtherFeature = _ => { };
            var plug = GameObjects.Create("Plug", root).AddComponent<VRCFuryHapticPlug>();
            plug.autoRenderer = plug.autoLength = plug.autoRadius = plug.enableSps = false;
            plug.unitsInMeters = true;
            plug.length = 0.4f;
            plug.radius = 0.04f;
            plug.channelSettings.useSharedTag = shared;
            if (!shared) plug.channelSettings.channels.Add(new VF.Model.SpsChannelSettings.Channel { id = "private" });
            if (withSocket) {
                var socket = GameObjects.Create("Socket", root).AddComponent<VRCFuryHapticSocket>();
                socket.name = "Socket";
                socket.addLight = VRCFuryHapticSocket.AddLight.Hole;
                socket.addMenuItem = true;
                socket.channelSettings.useSharedTag = shared;
                if (!shared) socket.channelSettings.channels.Add(new VF.Model.SpsChannelSettings.Channel { id = "private" });
            }
            var serviceTypes = injector.GetServices<object>().Select(service => service.GetType()).ToArray();
            int SortPosition<T>() => Array.IndexOf(serviceTypes, typeof(T)) + 1;
            globals.currentMenuSortPosition = SortPosition<BakeHapticPlugsService>();
            injector.GetService<BakeHapticPlugsService>().ApplyEarlyBake();
            injector.GetService<BakeHapticPlugsService>().Apply();
            globals.currentMenuSortPosition = SortPosition<BakeHapticSocketsService>();
            injector.GetService<BakeHapticSocketsService>().Apply();
            var options = injector.GetService<SpsOptionsService>();
            injector.GetService<ChangeSpsChannelsService>().Apply();
            globals.currentMenuSortPosition = legacyServiceFirst ? 0 : SortPosition<SpsOptionsService>();
            options.MoveMenus();
            var menus = injector.GetService<MenuService>();
            if (menus.GetReadOnlyMenu() != null) menus.GetMenu().SortMenu();
            injector.GetService<ChangeSpsChannelsService>().Apply();
            var legacyObjects = root.transform.GetComponentsInChildren<Transform>(true)
                .Where(t => t.name == "LegacyLights" || t.name == "Light - Legacy DPS Trigger" || t.name == "Sender - Legacy Front").ToArray();
            Assert.That(legacyObjects, Has.Length.EqualTo(!available || !shared ? 0 : withSocket ? 3 : 1));
            if (!shared) return;
            var legacy = options.GetLegacyParameter();
            var menu = injector.GetService<MenuService>().GetReadOnlyMenu();
            if (!available) {
                Assert.That(legacy, Is.Null);
                Assert.That(menu == null || menu.controls.Single(c => c.name == "SPS").subMenu.controls
                    .All(c => !c.name.Contains("Legacy Support")), Is.True);
                return;
            }
            var controls = menu.controls.Single(c => c.name == "SPS").subMenu.controls;
            Assert.That(controls.Count(c => c.parameter?.name == legacy.name), Is.EqualTo(1));
            Assert.That(controls.Last().parameter.name, Is.EqualTo(legacy.name), "Legacy is created after the socket menu items");
            if (withSocket) Assert.That(controls.First().parameter.name, Is.EqualTo(options.GetAutoParameter().name));
            Assert.That(legacyObjects.All(t => t.parent.name == "Legacy Mode" && !t.parent.gameObject.activeSelf), Is.True);
            Start();
            Evaluate(5);
            Assert.That(legacyObjects.All(t => t.parent.gameObject.activeSelf == defaultOn), Is.True, "Legacy default on avatar load");
            foreach (var enabled in new[] { true, false, true, false }) {
                playable.SetFloat(legacy, enabled ? 1 : 0);
                Evaluate(3);
                Assert.That(legacyObjects.All(t => t.parent.gameObject.activeSelf == enabled), Is.True);
                Assert.That(root.GetComponentsInChildren<VRCContactSender>(true)
                    .Where(sender => sender.name == "Sender - Root" || sender.name == "Sender - Sphere")
                    .All(sender => sender.gameObject.activeSelf), Is.True, "Main senders remain available in both legacy modes");
            }
        }

        [Test]
        public void CompatibilityFeaturesRespectDependenciesAndUseLegacyToggle(
            [Values(false, true)] bool legacyPlugs,
            [Values(false, true)] bool legacySockets,
            [Values(false, true)] bool tpsAnimations,
            [Values(false, true)] bool dpsAnimations) {
            if (!BuildTargetUtils.IsDesktop()) Assert.Ignore("Requires desktop SPS baking");
            injector.Set("componentObject", new Func<VFGameObject>(() => root));
            var globals = injector.GetService<GlobalsService>();
            globals.allBuildersInRun = new List<VF.Feature.Base.FeatureBuilder>();
            globals.allFeaturesInRun = new List<FeatureModel> {
                new SpsOptions {
                    autoModeAvailable = false,
                    legacyModeAvailable = legacyPlugs,
                    legacySocketCompatibility = legacySockets,
                    tpsDepthAnimationCompatibility = tpsAnimations,
                    addDpsTipLight = dpsAnimations,
                    legacyModeEnabledOnAvatarLoad = true
                }
            };
            globals.addOtherFeature = _ => { };
            var plug = GameObjects.Create("Plug", root).AddComponent<VRCFuryHapticPlug>();
            plug.autoRenderer = plug.autoLength = plug.autoRadius = plug.enableSps = false;
            plug.unitsInMeters = true;
            plug.length = 0.4f;
            plug.radius = 0.04f;
            var socket = GameObjects.Create("Socket", root).AddComponent<VRCFuryHapticSocket>();
            socket.addLight = VRCFuryHapticSocket.AddLight.Hole;
            socket.addMenuItem = true;
            injector.GetService<BakeHapticPlugsService>().ApplyEarlyBake();
            injector.GetService<BakeHapticPlugsService>().Apply();
            injector.GetService<BakeHapticSocketsService>().Apply();
            injector.GetService<ChangeSpsChannelsService>().Apply();
            var transforms = root.GetComponentsInChildren<Transform>(true);
            Assert.That(transforms.Any(t => t.name == "LegacyLights"), Is.EqualTo(legacyPlugs));
            Assert.That(transforms.Any(t => t.name == "Sender - Legacy Front"), Is.EqualTo(legacyPlugs && tpsAnimations));
            Assert.That(transforms.Any(t => t.name == "Light - Legacy DPS Trigger"), Is.EqualTo(legacySockets && dpsAnimations));
            var options = injector.GetService<SpsOptionsService>();
            var legacy = options.GetLegacyParameter();
            Assert.That(legacy != null, Is.EqualTo(legacyPlugs || (legacySockets && dpsAnimations)));
            options.MoveMenus();
            var menu = injector.GetService<MenuService>().GetReadOnlyMenu();
            Assert.That(menu.controls.Single(c => c.name == "SPS").subMenu.controls
                .Count(c => c.name.Contains("Legacy Support")), Is.EqualTo(legacy != null ? 1 : 0));
            if (legacy == null) return;
            var legacyObjects = transforms.Where(t => t.name == "LegacyLights"
                || t.name == "Sender - Legacy Front" || t.name == "Light - Legacy DPS Trigger").ToArray();
            Start();
            Evaluate(5);
            Assert.That(legacyObjects.All(t => t.parent.gameObject.activeSelf), Is.True);
            playable.SetFloat(legacy, 0);
            Evaluate(3);
            Assert.That(legacyObjects.All(t => !t.parent.gameObject.activeSelf), Is.True);
        }
    }
}
#endif

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

    internal class SpsOptionsServiceDefaultsTest : SpsOptionsTestBase {
        [TestCase(false, false, false)]
        [TestCase(false, true, true)]
        [TestCase(true, false, false)]
        [TestCase(true, false, true)]
        [TestCase(true, true, false)]
        [TestCase(true, true, true)]
        public void ModesRespectAvailabilityDefaultsAndIndependentSaving(bool available, bool defaultOn, bool saved) {
            var root = new GameObject("SPS options");
            try {
                var avatar = root.AddComponent<VRCAvatarDescriptor>();
                avatar.baseAnimationLayers = new[] { new VRCAvatarDescriptor.CustomAnimLayer { type = VRCAvatarDescriptor.AnimLayerType.FX } };
                avatar.specialAnimationLayers = Array.Empty<VRCAvatarDescriptor.CustomAnimLayer>();
                var injector = VRCFuryInjectorBuilder.GetInjector(avatar);
                var options = new SpsOptions {
                    autoModeAvailable = available, autoModeDefaultOn = defaultOn, autoModeSaved = saved,
                    legacyModeAvailable = available, legacyModeEnabledOnAvatarLoad = defaultOn, legacyModeSaved = saved,
                    saveSockets = !saved
                };
                injector.GetService<GlobalsService>().allFeaturesInRun = new List<FeatureModel> { options };
                var controllers = injector.GetService<ControllersService>();
                controllers.ClearCache(applyBaseMask: false);
                var service = injector.GetService<SpsOptionsService>();
                var auto = service.GetAutoParameter();
                var legacy = service.GetLegacyParameter();
                if (!available) {
                    Assert.That(auto, Is.Null);
                    Assert.That(legacy, Is.Null);
                    Assert.That(service.GetLegacyOnClip(), Is.Null);
                    Assert.That(avatar.expressionParameters, Is.Null);
                    Assert.That(avatar.expressionsMenu, Is.Null);
                    return;
                }
                Assert.That(service.GetAutoParameter(), Is.SameAs(auto));
                Assert.That(auto.name, Is.EqualTo("VF_" + UniqueParamService.Hash(injector.GetService<PipelineIdService>().Get()) + "_SpsAutoMode"));
                Assert.That(avatar.expressionParameters.parameters.Single(p => p.name == auto.name).IsNetworkSynced(), Is.False);
                Assert.That(service.GetLegacyParameter(), Is.SameAs(legacy));
                Assert.That(service.GetLegacyOnClip(), Is.Not.Null);
                foreach (var parameter in new[] { auto, legacy }) {
                    Assert.That(parameter.GetDefault() != 0, Is.EqualTo(defaultOn));
                    var expression = avatar.expressionParameters.parameters.Single(p => p.name == parameter.name);
                    Assert.That(expression.saved, Is.EqualTo(saved));
                    Assert.That(expression.defaultValue, Is.EqualTo(defaultOn ? 1 : 0));
                }
                Assert.That(avatar.expressionParameters.parameters, Has.Length.EqualTo(2));
            } finally {
                Object.DestroyImmediate(root);
            }
        }

        [TestCase(true)]
        [TestCase(false)]
        public void GlobalLegacyAvailabilityOverridesObsoleteSocketSetting(bool available) {
            if (!VF.Utils.BuildTargetUtils.IsDesktop()) Assert.Ignore("Socket lights are desktop-only.");
            var root = new GameObject("Socket legacy options");
            try {
                var socket = root.AddComponent<VRCFuryHapticSocket>();
                socket.addLight = VRCFuryHapticSocket.AddLight.Hole;
                socket.useLights = !available;
                var injector = new VRCFuryInjector();
                injector.ImportScan(typeof(VFServiceAttribute));
                var result = injector.GetService<VF.Inspector.VRCFuryHapticSocketBaker>().Bake(socket, legacyPlugCompatibility: available);
                injector.GetService<SpsChannelsService>().Apply(result.channelContacts);
                Assert.That(result.legacyLights != null, Is.EqualTo(available));
            } finally {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void ModesAreAvailableByDefault() {
            var options = new SpsOptions();
            Assert.That(options.autoModeAvailable && options.legacyModeAvailable, Is.True);
            Assert.That(options.tpsDepthAnimationCompatibility, Is.False);
            Assert.That(options.legacySocketCompatibility, Is.True);
        }

        [TestCase(false, false)]
        [TestCase(false, true)]
        [TestCase(true, false)]
        [TestCase(true, true)]
        public void LegacySocketCompatibilityControlsLegacyPlugSenders(bool enabled, bool shared) {
            if (!BuildTargetUtils.IsDesktop()) Assert.Ignore("Contact senders are desktop-only.");
            var root = new GameObject("Legacy socket compatibility");
            try {
                var plug = root.AddComponent<VRCFuryHapticPlug>();
                plug.autoRenderer = plug.autoLength = plug.autoRadius = plug.enableSps = false;
                plug.unitsInMeters = true;
                plug.length = 0.4f;
                plug.radius = 0.03f;
                plug.channelSettings.useSharedTag = shared;
                plug.channelSettings.channels.Add(new VF.Model.SpsChannelSettings.Channel { id = "custom" });
                var injector = new VRCFuryInjector();
                injector.ImportScan(typeof(VFServiceAttribute));
                var bake = injector.GetService<VRCFuryHapticPlugBaker>().Bake(plug, legacySocketCompatibility: enabled);
                injector.GetService<SpsChannelsService>().Apply(bake.channelContacts);
                var senders = bake.bakeRoot.GetComponentsInSelfAndChildren<VRCContactSender>();
                Assert.That(senders.Count, Is.EqualTo(2 + (enabled && shared ? 2 : 0)));
                var main = senders.Single(s => s.name == "Sender - Sphere");
                Assert.That(main.collisionTags.Contains(SpsTags.LegacyPlugSphere), Is.EqualTo(enabled && shared));
                Assert.That(main.collisionTags, Does.Contain(SpsTags.FormatPlug("custom")));
                Assert.That(main.collisionTags.Contains(SpsTags.FormatPlug(SpsTags.PublicChannel)), Is.EqualTo(shared));
                Assert.That(senders.Any(s => s.collisionTags.Contains(SpsTags.LegacyPlugWidth)), Is.EqualTo(enabled && shared));
                Assert.That(senders.Any(s => s.collisionTags.Contains(SpsTags.LegacyPlugRoot)), Is.EqualTo(enabled && shared));
                Assert.That(senders.Single(s => s.name == "Sender - Envelope").collisionTags, Does.Contain(SpsTags.PlugClose));
            } finally {
                Object.DestroyImmediate(root);
            }
        }

        [TestCase(false, false)]
        [TestCase(false, true)]
        [TestCase(true, false)]
        [TestCase(true, true)]
        public void TpsDepthCompatibilityRequiresLegacyPlugCompatibility(bool enabled, bool legacy) {
            if (!VF.Utils.BuildTargetUtils.IsDesktop()) Assert.Ignore("Contact senders are desktop-only.");
            var root = new GameObject("TPS depth compatibility");
            try {
                var socket = root.AddComponent<VRCFuryHapticSocket>();
                socket.addLight = VRCFuryHapticSocket.AddLight.Hole;
                var injector = new VRCFuryInjector();
                injector.ImportScan(typeof(VFServiceAttribute));
                var result = injector.GetService<VF.Inspector.VRCFuryHapticSocketBaker>().Bake(socket,
                    legacyPlugCompatibility: legacy, tpsDepthAnimationCompatibility: enabled);
                injector.GetService<SpsChannelsService>().Apply(result.channelContacts);
                var fronts = result.bakeRoot.GetComponentsInSelfAndChildren<VRC.SDK3.Dynamics.Contact.Components.VRCContactSender>()
                    .Where(sender => sender.collisionTags.Contains(SpsTags.LegacySocketFront)).ToArray();
                Assert.That(fronts.Length, Is.EqualTo(enabled && legacy ? 1 : 0));
                Assert.That(result.legacyLights != null, Is.EqualTo(legacy));
                var rootSender = result.bakeRoot.GetComponentsInSelfAndChildren<VRCContactSender>()
                    .Single(sender => sender.name == "Sender - Root");
                Assert.That(rootSender.collisionTags.Contains(SpsTags.LegacySocket2), Is.EqualTo(legacy),
                    "SPS2 depth animations must not be triggered when legacy plug compatibility is unavailable");
                Assert.That(rootSender.collisionTags.Contains(SpsTags.LegacySocket), Is.EqualTo(legacy));
                Assert.That(rootSender.collisionTags, Does.Contain(SpsTags.FormatSocket(SpsTags.PublicChannel)));
                if (enabled && legacy) {
                    var front = fronts.Single();
                    Assert.That(front.radius, Is.EqualTo(0.001f).Within(0.00001f));
                    Assert.That(front.position, Is.EqualTo(Vector3.forward * 0.01f));
                    Assert.That(front.collisionTags, Does.Not.Contain(SpsTags.LegacySocketFront + "_SelfNotOnHips"));
                }
            } finally {
                Object.DestroyImmediate(root);
            }
        }
    }
}
