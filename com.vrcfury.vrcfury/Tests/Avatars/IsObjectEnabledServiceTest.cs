using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;
using VF.Builder;
using VF.Injector;
using VF.Service;
using VF.Utils;
using VF.Utils.Controller;
using VRC.SDK3.Avatars.Components;
using Object = UnityEngine.Object;

namespace VF.Tests {
    public class IsObjectEnabledServiceTest {
        [TestCase(true, false)]
        [TestCase(false, false)]
        [TestCase(true, true)]
        [TestCase(false, true)]
        public void PartialActiveAnimationPreservesTheInitialState(bool initiallyActive, bool animatedParent) {
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
                var parent = new GameObject("Parent");
                parent.transform.SetParent(root.transform, false);
                var obj = new GameObject("Plug");
                obj.transform.SetParent(parent.transform, false);
                obj.SetActive(initiallyActive);

                var weight = fx.CreateUniqueFloat("Reset weight");
                var parentWeight = fx.CreateUniqueFloat("Parent off weight");
                var clip = VFClip.Create("Reset");
                clip.SetEnabled(obj, !initiallyActive);
                var tree = VFBlendTreeDirect.Create("Partial animation");
                tree.Add(weight, clip);
                if (animatedParent) {
                    var parentClip = VFClip.Create("Parent off");
                    parentClip.SetEnabled(parent, false);
                    tree.Add(parentWeight, parentClip);
                }
                fx.NewLayer("Reset").NewState("Reset").WithAnimation(tree);
                var enabledService = injector.GetService<IsObjectEnabledService>();
                var enabled = enabledService.Get(obj);
                var observed = fx.CreateUniqueFloat("Observed enabled");
                var observe = VFBlendTreeDirect.Create("Observe");
                observe.Add(BlendtreeMath.GreaterThan(enabled, 0).create(observed.MakeSetter(1), observed.MakeSetter(0)));
                fx.NewLayer("Observe").NewState("Observe").WithAnimation(observe);
                enabledService.Apply();
                injector.GetService<NoBadControllerParamsService>().Apply();
                injector.GetService<FixPartiallyWeightedAapsService>().Apply();
                var context = new VFSaveContext(root, false);
                var controller = new AnimatorController { parameters = fx.parameters.Select(p => p.Save()).ToArray() };
                controller.layers = fx.GetLayers().Select(layer => layer.Save(context)).ToArray();
                assets.Add(controller);
                assets.AddRange(context.NewAssets);
                var animator = root.AddComponent<Animator>();
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                graph = PlayableGraph.Create("Object enabled");
                var playable = AnimatorControllerPlayable.Create(graph, controller);
                AnimationPlayableOutput.Create(graph, "FX", animator).SetSourcePlayable(playable);
                graph.Play();

                void Check(float resetWeight, float offWeight, bool expected) {
                    playable.SetFloat(weight, resetWeight);
                    playable.SetFloat(parentWeight, offWeight);
                    for (var frame = 0; frame < 10; frame++) graph.Evaluate(1f / 60);
                    Assert.That(obj.activeInHierarchy, Is.EqualTo(expected), "Actual object activation");
                    Assert.That(playable.GetFloat(observed), Is.EqualTo(expected ? 1 : 0), "Mirrored activation");
                }
                Check(0, 0, initiallyActive);
                Check(1, 0, !initiallyActive);
                Check(0, 0, initiallyActive);
                if (animatedParent) {
                    Check(initiallyActive ? 0 : 1, 1, false);
                    Check(initiallyActive ? 0 : 1, 0, true);
                }
            } finally {
                if (graph.IsValid()) graph.Destroy();
                Object.DestroyImmediate(root);
                foreach (var asset in assets.Distinct()) if (asset != null) Object.DestroyImmediate(asset);
            }
        }
    }
}
