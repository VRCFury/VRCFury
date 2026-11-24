using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine.Animations;
using UnityEngine.Playables;
using VF.Actions;
using VF.Builder;
using VF.Builder.Haptics;
using VF.Component;
using VF.Injector;
using VF.Inspector;
using VF.Model;
using VF.Model.Feature;
using VF.Model.StateAction;
using VF.Service;
using VF.Utils;
using VF.Utils.Controller;
using VRC.SDK3.Avatars.Components;
using VRC.SDK3.Dynamics.Contact.Components;
using VRC.SDK3.Dynamics.PhysBone.Components;
using Object = UnityEngine.Object;
using Party = VF.Builder.Haptics.SelfAndOthersReceiverService.Party;

namespace VF.Tests {
    internal abstract class SpsChannelBuildTestBase {
        protected GameObject root;
        protected VRCFuryInjector injector;

        protected string SelfTag(string tag) => tag + injector.GetService<SpsConfigurer>().selfTagSuffix;
        protected readonly List<Object> assets = new();
        protected bool animatorPrepared;

        [SetUp]
        public void SetUp() {
            animatorPrepared = false;
            root = new GameObject("Channel tests");
            var avatar = root.AddComponent<VRCAvatarDescriptor>();
            avatar.customizeAnimationLayers = true;
            avatar.baseAnimationLayers = new[] { new VRCAvatarDescriptor.CustomAnimLayer { type = VRCAvatarDescriptor.AnimLayerType.FX } };
            avatar.specialAnimationLayers = System.Array.Empty<VRCAvatarDescriptor.CustomAnimLayer>();
            injector = VRCFuryInjectorBuilder.GetInjector(avatar);
            injector.GetService<GlobalsService>().allFeaturesInRun = new List<FeatureModel>();
            injector.GetService<ControllersService>().ClearCache(applyBaseMask: false);
            injector.GetService<ControllersService>().GetFx().GetOrCreateFloat(VFBlendTreeDirect.AlwaysOneParam, 1);
        }

        [TearDown]
        public void TearDown() {
            Object.DestroyImmediate(root);
            foreach (var asset in assets) if (asset != null) Object.DestroyImmediate(asset);
            assets.Clear();
        }

        protected void Sample(VFClip clip, float time = 0) {
            if (clip.GetFloatBindings().Any(b => b.IsAnimatorBinding())) {
                RunAnimation(clip, (graph, playable) => {
                    playable.SetFloat("Test motion time", time / Mathf.Max(clip.GetLengthInSeconds(), 1));
                    for (var i = 0; i < 5; i++) graph.Evaluate(1f / 60);
                }, motionTime: clip.GetLengthInSeconds() > 0);
                return;
            }
            var raw = (AnimationClip)clip.Save(root);
            assets.Add(raw);
            raw.SampleAnimation(root, time);
        }

        protected VFClip Track(VFClip clip) {
            injector.GetService<AllClipsService>().AddAdditionalManagedClip(clip);
            return clip;
        }

        protected void RunAnimation(VFMotion motion, System.Action<PlayableGraph, AnimatorControllerPlayable> check,
            bool motionTime = false) {
            var fx = injector.GetService<ControllersService>().GetFx();
            fx.GetOrCreateFloat(VFBlendTreeDirect.AlwaysOneParam, 1);
            var layer = fx.NewLayer("Channel regression");
            var action = VFBlendTreeDirect.Create("Channel action");
            action.Add(motion);
            var state = layer.NewState("Animation").WithAnimation(action);
            if (motionTime) state.MotionTime(fx.GetOrCreateFloat("Test motion time"));
            if (!animatorPrepared) {
                injector.GetService<IsObjectEnabledService>().Apply();
                injector.GetService<ObjectTreeService>().Apply();
                animatorPrepared = true;
            }
            injector.GetService<NoBadControllerParamsService>().Apply();
            injector.GetService<FixPartiallyWeightedAapsService>().Apply();
            var context = new VFSaveContext(root, false);
            var controller = new AnimatorController { parameters = fx.parameters.Select(p => p.Save()).ToArray() };
            controller.layers = fx.GetLayers().Select(layer => layer.Save(context)).ToArray();
            assets.Add(controller);
            assets.AddRange(context.NewAssets);
            var graph = PlayableGraph.Create("SPS channel regression");
            try {
                graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
                var playable = AnimatorControllerPlayable.Create(graph, controller);
                var animator = root.GetComponent<Animator>();
                if (animator == null) animator = root.AddComponent<Animator>();
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                AnimationPlayableOutput.Create(graph, "Channels", animator).SetSourcePlayable(playable);
                graph.Play();
                check(graph, playable);
            } finally {
                graph.Destroy();
                fx.ReplaceLayers(fx.GetLayers().Where(l => l != layer));
            }
        }

        protected VFClip Defaults(VFClip clip) {
            var defaults = injector.GetService<FixWriteDefaultsService>();
            foreach (var binding in clip.GetFloatBindings()) defaults.RecordDefaultNow(binding, true, force: true);
            return defaults.GetDefaultClip();
        }

        protected VRCFuryHapticPlug Plug(GameObject obj, bool shared) {
            var plug = obj.AddComponent<VRCFuryHapticPlug>();
            plug.autoRenderer = plug.autoLength = plug.autoRadius = plug.enableSps = false;
            plug.unitsInMeters = true;
            plug.length = 0.4f;
            plug.radius = 0.03f;
            plug.channelSettings.useSharedTag = shared;
            plug.channelSettings.channels.Add(new VF.Model.SpsChannelSettings.Channel { id = "old" });
            return plug;
        }

        protected VRCFuryHapticPlugEditor.BakeResult BakePlug(VRCFuryHapticPlug plug) {
            var bake = injector.GetService<VRCFuryHapticPlugBaker>().Bake(plug, deferMaterialConfig: true);
            RegisterTree(plug, bake.bakeRoot);
            return bake;
        }

        protected void RegisterTree(UnityEngine.Component component, VFGameObject frame) =>
            injector.GetService<SpsChannelsService>().Register(component,
                new System.Lazy<VFBlendTreeDirect>(() => injector.GetService<ObjectTreeService>().Get(frame)));

        protected ChangeSpsChannelsAction Action(GameObject target, bool shared, string channel = "new") {
            var action = new ChangeSpsChannelsAction {
                target = target, channelSettings = new SpsChannelSettings {
                    useSharedTag = shared, channels = { new SpsChannelSettings.Channel { id = channel } }
                }
            };
            return action;
        }
    }
}
