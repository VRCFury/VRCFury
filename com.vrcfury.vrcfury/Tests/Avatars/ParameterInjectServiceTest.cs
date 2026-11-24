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

    internal class ParameterInjectServiceTest : SpsAvatarBuildTestBase {
        [TestCase("socket", "__sps_unknown_meters")]
        [TestCase("socket", "__sps_depth_unknown")]
        [TestCase("plug", "__sps_unknown_meters")]
        [TestCase("plug", SpsPlugEntryRadiusMeters)]
        [TestCase("none", SpsDepthMeters)]
        public void UnsupportedParameterInjectionsFailBuild(string sourceType, string parameter) {
            if (!BuildTargetUtils.IsDesktop()) Assert.Ignore("Requires desktop SPS baking");
            if (sourceType == "socket") root.AddComponent<VRCFuryHapticSocket>().addMenuItem = false;
            if (sourceType == "plug") {
                var plug = root.AddComponent<VRCFuryHapticPlug>();
                plug.autoRenderer = plug.autoLength = plug.autoRadius = plug.enableSps = false;
                plug.length = 0.4f;
                plug.radius = 0.05f;
            }
            injector.Set("componentObject", new Func<VFGameObject>(() => root));
            var globals = injector.GetService<GlobalsService>();
            globals.allBuildersInRun = new List<VF.Feature.Base.FeatureBuilder>();
            globals.allFeaturesInRun = new List<FeatureModel> { new SpsOptions { autoModeAvailable = false } };
            globals.addOtherFeature = _ => { };
            var injections = injector.GetService<ParameterInjectService>();
            injections.Register(new ParameterInjectService.Request {
                sourceObject = root, sourceParam = parameter, resolvedParam = "Injected output"
            });
            var error = Assert.Catch<Exception>(() => {
                if (sourceType == "plug") {
                    var baker = injector.GetService<BakeHapticPlugsService>();
                    baker.ApplyEarlyBake();
                    baker.Apply();
                } else {
                    injector.GetService<BakeHapticSocketsService>().Apply();
                }
                injections.Validate();
            });
            Assert.That(error.Message, Does.Contain(parameter).And.Contain("Injected output")
                .And.Contain(root.name).And.Contain("does not support"));
        }
    }
}
#endif
