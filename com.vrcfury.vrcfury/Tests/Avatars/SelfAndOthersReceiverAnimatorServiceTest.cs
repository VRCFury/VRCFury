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
        public void OnlyComparesWhenBothContactsArePresent() {
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
                receiverAnimator.GetParameter(receivers, Party.Merged);
                var self = receiverAnimator.GetParameter(receivers, true);
                var others = receiverAnimator.GetParameter(receivers, false);
                var mergedSelf = receivers.Find(true, true).obj;
                var mergedOthers = receivers.Find(false, true).obj;
                var difference = fx.parameters.Single(param => param.name.EndsWith("/MergeDifference", StringComparison.Ordinal));
                injector.GetService<SpsChannelsService>().Apply(channels, _ => { });
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

                void Check(float selfProx, float othersProx, float selfDistance, float othersDistance, int frames,
                    bool expectSelf, float expectDifference) {
                    playable.SetFloat(self, selfProx);
                    playable.SetFloat(others, othersProx);
#if VRCSDK_HAS_CONTACT_PARAMETER_FLAGS
                    playable.SetFloat(self.name + "_PointSeparation", selfDistance);
                    playable.SetFloat(others.name + "_PointSeparation", othersDistance);
#endif
                    for (var i = 0; i < frames; i++) graph.Evaluate(1f / 60);
                    Assert.That(mergedSelf.active, Is.EqualTo(expectSelf));
                    Assert.That(mergedOthers.active, Is.EqualTo(!expectSelf));
                    Assert.That(playable.GetFloat(difference), Is.EqualTo(expectDifference).Within(0.00001f));
                }

                Check(0, 0, .25f, .75f, 2, false, 0);
                Check(.75f, 0, .25f, .75f, 1, true, 0);
                Check(0, .75f, .25f, .75f, 1, false, 0);
#if VRCSDK_HAS_CONTACT_PARAMETER_FLAGS
                // Saturated proximity should still choose the nearest point, including zero separation.
                Check(1, 1, 0, .5f, 1, false, .5f);
                Check(1, 1, 0, .5f, 1, true, .5f);
                Check(1, 1, .5f, .5f, 2, false, 0);
#else
                Check(.75f, .25f, 0, .5f, 1, false, .5f);
                Check(.75f, .25f, 0, .5f, 1, true, .5f);
                Check(.75f, .75f, .5f, .5f, 2, false, 0);
#endif
                Check(0, 0, .25f, .75f, 1, false, 0);
            } finally {
                if (graph.IsValid()) graph.Destroy();
                Object.DestroyImmediate(root);
                foreach (var asset in assets.Distinct()) if (asset != null) Object.DestroyImmediate(asset);
            }
        }
    }
}
