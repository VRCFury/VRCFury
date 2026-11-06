using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
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

namespace VF.Tests {
    [Category("VRCFury")]
    public class SpsOptionsTest {
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
                plug.useSharedTag = shared;
                plug.includeTags.Add(new VRCFuryHapticPlug.TagRule { tag = "custom" });
                var injector = new VRCFuryInjector();
                injector.ImportScan(typeof(VFServiceAttribute));
                var bake = injector.GetService<VRCFuryHapticPlugBaker>().Bake(plug, legacySocketCompatibility: enabled);
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
                var fronts = result.bakeRoot.GetComponentsInSelfAndChildren<VRC.SDK3.Dynamics.Contact.Components.VRCContactSender>()
                    .Where(sender => sender.collisionTags.Contains(SpsTags.LegacySocketFront)).ToArray();
                Assert.That(fronts.Length, Is.EqualTo(enabled && legacy ? 1 : 0));
                Assert.That(result.legacyLights != null, Is.EqualTo(legacy));
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
