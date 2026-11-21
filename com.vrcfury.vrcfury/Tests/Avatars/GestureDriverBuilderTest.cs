using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;
using VF.Builder;
using VF.Feature;
using VF.Model.Feature;
using VF.Service;
using VF.Utils;
using VF.Utils.Controller;
using VRC.SDK3.Avatars.Components;
using Object = UnityEngine.Object;

namespace VF.Tests {
    [Category("VRCFury")]
    internal class GestureDriverBuilderTest {
        [Test]
        public void EitherFistWritesTheLargerWeightDirectlyWhileActive() {
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
                injector.Set("componentObject", new Func<VFGameObject>(() => root));
                var controllers = injector.GetService<ControllersService>();
                controllers.ClearCache(applyBaseMask: false);
                var fx = controllers.GetFx();
                fx.GetOrCreateFloat(VFBlendTreeDirect.AlwaysOneParam, 1);
                var builder = injector.CreateAndFillObject<GestureDriverBuilder>();
                builder.featureBaseObject = root;
                builder.model = new GestureDriver();
                builder.model.gestures.Add(new GestureDriver.Gesture {
                    hand = GestureDriver.Hand.EITHER,
                    sign = GestureDriver.HandSign.FIST,
                    enableWeight = true
                });
                builder.Apply();

                var target = fx.parameters.Single(param => param.name.EndsWith("/Target", StringComparison.Ordinal));
                var targetLayer = fx.GetLayers().Single(layer => layer.name.EndsWith(" Target", StringComparison.Ordinal));
                var comparison = new AnimatorIterator.Trees().From(targetLayer).Single(tree =>
                    tree.BlendParameter == "GestureLeftWeight" && tree.BlendParameterY == "GestureRightWeight");
                Assert.That(targetLayer.allStates.Single(state => state.name == "On").motion, Is.SameAs(comparison));

                injector.GetService<NoBadControllerParamsService>().Apply();
                injector.GetService<FixPartiallyWeightedAapsService>().Apply();
                var context = new VFSaveContext(root, false);
                var controller = new AnimatorController { parameters = fx.parameters.Select(param => param.Save()).ToArray() };
                controller.layers = fx.GetLayers().Select(layer => layer.Save(context)).ToArray();
                assets.Add(controller);
                assets.AddRange(context.NewAssets);
                var animator = root.AddComponent<Animator>();
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                graph = PlayableGraph.Create("Gesture weight");
                graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
                var playable = AnimatorControllerPlayable.Create(graph, controller);
                AnimationPlayableOutput.Create(graph, "FX", animator).SetSourcePlayable(playable);
                graph.Play();

                void Check(float left, float right, float expected, int frames) {
                    playable.SetFloat("GestureLeftWeight", left);
                    playable.SetFloat("GestureRightWeight", right);
                    for (var i = 0; i < frames; i++) graph.Evaluate(1f / 60);
                    Assert.That(playable.GetFloat(target), Is.EqualTo(expected).Within(0.000001f));
                }

                Check(.3f, .8f, 0, 3);
                playable.SetInteger("GestureRight", (int)GestureDriver.HandSign.FIST);
                Check(.3f, .8f, .8f, 3);
                Check(.9f, .2f, .9f, 1);
                playable.SetInteger("GestureRight", (int)GestureDriver.HandSign.NEUTRAL);
                Check(.9f, .2f, 0, 3);
            } finally {
                if (graph.IsValid()) graph.Destroy();
                Object.DestroyImmediate(root);
                foreach (var asset in assets.Distinct()) if (asset != null) Object.DestroyImmediate(asset);
            }
        }
    }
}
