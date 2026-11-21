using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;
using VF.Builder;
using VF.Builder.Haptics;
using VF.Service;
using VF.Utils;
using VF.Utils.Controller;
using VRC.SDK3.Avatars.Components;
using Object = UnityEngine.Object;
using Party = VF.Builder.Haptics.SelfAndOthersReceiverService.Party;

namespace VF.Tests {
    [Category("VRCFury")]
    internal class SelfAndOthersReceiverAnimatorServiceTest {
        [Test]
        public void SelectsTheClosestContactAndDisablesBothReceiversAtIdle() {
            var root = new GameObject("Avatar");
            var assets = new List<Object>();
            var graph = default(PlayableGraph);
            try {
                var avatar = root.AddComponent<VRCAvatarDescriptor>();
                avatar.customizeAnimationLayers = true;
                avatar.baseAnimationLayers = new[] {
                    new VRCAvatarDescriptor.CustomAnimLayer { type = VRCAvatarDescriptor.AnimLayerType.FX }
                };
                avatar.specialAnimationLayers = Array.Empty<VRCAvatarDescriptor.CustomAnimLayer>();
                var injector = VRCFuryInjectorBuilder.GetInjector(avatar);
                var controllers = injector.GetService<ControllersService>();
                controllers.ClearCache(applyBaseMask: false);
                var fx = controllers.GetFx();
                fx.GetOrCreateFloat(VFBlendTreeDirect.AlwaysOneParam, 1);
                var tags = new[] { "Test" };
                var channels = new SpsChannelContacts(avatar, _ => new SpsChannelsService.ChannelSet(false, tags, tags, tags));
                var receivers = injector.GetService<SelfAndOthersReceiverService>().Create(new SelfAndOthersReceiverService.Request {
                    frame = root,
                    name = "Test contact",
                    worldRadius = 1,
                    channels = channels,
                    getTags = (_, _) => tags
                });
                var receiverAnimator = injector.GetService<SelfAndOthersReceiverAnimatorService>();
#if VRCSDK_HAS_CONTACT_PARAMETER_FLAGS
                receiverAnimator.GetParameter(receivers, Party.Merged);
                var self = receiverAnimator.GetAdvancedParam(receivers, Party.Self, "PointSeparation");
                var others = receiverAnimator.GetAdvancedParam(receivers, Party.Others, "PointSeparation");
                Assert.That(self.GetDefault(), Is.EqualTo(-1));
                Assert.That(others.GetDefault(), Is.EqualTo(-1));
                var selector = new AnimatorIterator.Trees().From(fx).Single(tree => tree.BlendParameter == others.name);
                Assert.That(selector.children.Count, Is.EqualTo(5));
                var mergedSelf = receivers.Find(true, true).obj;
                var mergedOthers = receivers.Find(false, true).obj;
                injector.GetService<SpsChannelsService>().Apply(channels, _ => { });
                Assert.That(mergedSelf.active || mergedOthers.active, Is.False);
                injector.GetService<IsObjectEnabledService>().Apply();
                injector.GetService<ObjectTreeService>().Apply();
                injector.GetService<NoBadControllerParamsService>().Apply();
                injector.GetService<FixPartiallyWeightedAapsService>().Apply();
                var context = new VFSaveContext(root, false);
                var controller = new AnimatorController { parameters = fx.parameters.Select(param => param.Save()).ToArray() };
                controller.layers = fx.GetLayers().Select(layer => layer.Save(context)).ToArray();
                assets.Add(controller);
                assets.AddRange(context.NewAssets);
                var animator = root.AddComponent<Animator>();
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                graph = PlayableGraph.Create("Merged receiver selector");
                graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
                var playable = AnimatorControllerPlayable.Create(graph, controller);
                AnimationPlayableOutput.Create(graph, "FX", animator).SetSourcePlayable(playable);
                graph.Play();

                void Check(float selfValue, float othersValue, bool expectSelf, bool expectOthers, int frames = 1) {
                    playable.SetFloat(self, selfValue);
                    playable.SetFloat(others, othersValue);
                    for (var i = 0; i < frames; i++) graph.Evaluate(1f / 60);
                    Assert.That(mergedSelf.active, Is.EqualTo(expectSelf));
                    Assert.That(mergedOthers.active, Is.EqualTo(expectOthers));
                }

                graph.Evaluate(1f / 60);
                Assert.That(mergedSelf.active || mergedOthers.active, Is.False);
                Check(0, -1, true, false);
                Check(-1, 0, false, true);
                Check(0, .5f, true, false);
                Check(.5f, 0, false, true);
                Check(.5f, .5f, false, true);
                Check(0, 0, false, true);
                Check(-1, -1, false, false);
#else
                Assert.That(() => receiverAnimator.GetParameter(receivers, Party.Merged),
                    Throws.TypeOf<InvalidOperationException>().With.Message.EqualTo(VF.Exceptions.VRCFExceptionUtils.NewerVrcsdkRequired));
                Assert.That(receivers.IsCreated, Is.False);
#endif
            } finally {
                if (graph.IsValid()) graph.Destroy();
                Object.DestroyImmediate(root);
                foreach (var asset in assets.Distinct()) if (asset != null) Object.DestroyImmediate(asset);
            }
        }
    }
}
