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
using VRC.Dynamics;
using VRC.SDK3.Avatars.Components;
using VRC.SDK3.Dynamics.Contact.Components;
using Object = UnityEngine.Object;

namespace VF.Tests {
    internal abstract class SpsPlugReceiverTestBase {
        protected GameObject root;
        protected VRCFuryHapticPlug plug;
        protected VRCFuryInjector injector;

        protected string SelfTag(string tag) => tag + injector.GetService<SpsConfigurer>().selfTagSuffix;
        protected SpsPlugReceiverService service;

        [SetUp]
        public void SetUp() {
            root = new GameObject("SPS3 Receiver Test");
            root.transform.SetPositionAndRotation(new Vector3(12, -7, 4), Quaternion.Euler(31, 47, 13));
            root.transform.localScale = Vector3.one * 2;
            plug = root.AddComponent<VRCFuryHapticPlug>();
            plug.autoRenderer = plug.autoLength = plug.autoRadius = false;
            plug.unitsInMeters = true;
            plug.length = 0.4f;
            plug.radius = 0.01f;
            injector = new VRCFuryInjector();
            injector.ImportScan(typeof(VFServiceAttribute));
            injector.Set("autoChannelGenerator", new HipsChannels());
            service = injector.GetService<SpsPlugReceiverService>();
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(root);

        protected IEnumerable<string> SocketTags(bool self) {
            var channels = injector.GetService<SpsChannelsService>();
            var main = service.GetReceivers(plug);
            return main.request.getTags(channels.Get(plug).Default, self);
        }

        protected IEnumerable<string> PathTags() {
            var channels = injector.GetService<SpsChannelsService>();
            return channels.Get(plug).Default.paths.Select(SpsTags.FormatSocketPath);
        }

        protected void FinalizeChannels() {
            var channels = injector.GetService<SpsChannelsService>();
            channels.Apply(channels.Get(plug));
        }

        protected class HipsChannels : SpsAutoChannelGenerator {
            public HumanBodyBones? GetClosestBone(VFGameObject obj) => HumanBodyBones.Hips;
            public VFGameObject GetBone(VFGameObject obj, HumanBodyBones bone) => null;
            public Vector3? GetAvatarViewPosition(VFGameObject obj) => null;
        }

    }
}
