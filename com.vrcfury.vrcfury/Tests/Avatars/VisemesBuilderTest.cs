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
using VF.Inspector;
using VF.Model;
using VF.Model.Feature;
using VF.Model.StateAction;
using VF.Service;
using VF.Utils;
using VF.Utils.Controller;
using VRC.SDK3.Avatars.Components;
using Object = UnityEngine.Object;

namespace VF.Tests {
    [Category("VRCFury")]
    internal class VisemesBuilderTest {
        [TestCase(true)]
        [TestCase(false)]
        public void SharedLookupSelectsTheExactViseme(bool instant) {
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
                var target = new GameObject("Viseme target");
                target.transform.SetParent(root.transform, false);
                target.SetActive(false);
                var model = new Visemes { instant = instant };
                foreach (var field in typeof(Visemes).GetFields().Where(field => field.FieldType == typeof(State))) {
                    field.SetValue(model, new State());
                }
                model.state_PP.actions.Add(new ObjectToggleAction { obj = target });
                var injector = VRCFuryInjectorBuilder.GetInjector(avatar);
                injector.Set("componentObject", new Func<VFGameObject>(() => root));
                var globals = injector.GetService<GlobalsService>();
                globals.allFeaturesInRun = new List<FeatureModel> { model };
                var controllers = injector.GetService<ControllersService>();
                controllers.ClearCache(applyBaseMask: false);
                var fx = controllers.GetFx();
                fx.GetOrCreateFloat(VFBlendTreeDirect.AlwaysOneParam, 1);
                fx.GetOrCreateFloat("Viseme", 1);
                var actionOutput = fx.GetOrCreateFloat("VisemeActionOutput");
                model.state_PP.actions.Add(new FxFloatAction { name = actionOutput.name, value = 1 });
                model.state_FF.actions.Add(new FxFloatAction { name = actionOutput.name, value = 2 });
                var builder = injector.CreateAndFillObject<VisemesBuilder>();
                builder.model = model;
                builder.featureBaseObject = root;
                builder.Apply();

                var trees = new AnimatorIterator.Trees().From(fx);
                var lookup = trees.Single(tree => tree.BlendParameter == "Viseme");
                Assert.That(lookup.children, Has.Count.EqualTo(45));
                var names = new[] { "sil", "PP", "FF", "TH", "DD", "kk", "CH", "SS", "nn", "RR", "aa", "E", "I", "O", "U" };
                var weights = names.Select(name => fx.parameters.SingleOrDefault(parameter =>
                    parameter.name.EndsWith(name + "Raw", StringComparison.Ordinal))).ToArray();
                if (instant) {
                    Assert.That(weights.All(weight => weight == null), Is.True);
                    var parent = trees.Single(tree => tree.children.Any(child => ReferenceEquals(child.motion, lookup)));
                    Assert.That(parent.BlendParameter, Is.EqualTo("Voice"));
                } else {
                    for (var i = 0; i < weights.Length; i++) Assert.That(weights[i].GetDefault(), Is.EqualTo(i == 1 ? 1 : 0));
                }

                injector.GetService<NoBadControllerParamsService>().Apply();
                injector.GetService<FixPartiallyWeightedAapsService>().Apply();
                var defaults = injector.GetService<FixWriteDefaultsService>();
                defaults.DetermineWriteDefaultsStrategy();
                defaults.RecordAllDefaults();
                defaults.PositionDefaultsLayer();
                injector.GetService<TreeFlatteningService>().Optimize();
                var context = new VFSaveContext(root, false);
                var controller = new AnimatorController { parameters = fx.parameters.Select(parameter => parameter.Save()).ToArray() };
                controller.layers = fx.GetLayers().Select(layer => layer.Save(context)).ToArray();
                assets.Add(controller);
                assets.AddRange(context.NewAssets);
                var animator = root.AddComponent<Animator>();
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                graph = PlayableGraph.Create("Viseme lookup");
                graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
                var playable = AnimatorControllerPlayable.Create(graph, controller);
                AnimationPlayableOutput.Create(graph, "FX", animator).SetSourcePlayable(playable);
                graph.Play();
                playable.SetFloat("Voice", 1);
                graph.Evaluate(1f / 60);

                void Check(float value, int selected) {
                    playable.SetFloat("Viseme", value);
                    graph.Evaluate(1f / 60);
                    if (instant) {
                        var expected = selected == 1 ? 1 : selected == 2 ? 2 : 0;
                        Assert.That(playable.GetFloat(actionOutput), Is.EqualTo(expected), $"Viseme {value:R}");
                        Assert.That(target.activeSelf, Is.EqualTo(selected == 1), $"Viseme {value:R} object activation");
                    } else {
                        for (var i = 0; i < weights.Length; i++) {
                            Assert.That(playable.GetFloat(weights[i]), Is.EqualTo(i == selected ? 1 : 0), $"Viseme {value:R}, weight {i}");
                        }
                    }
                }
                Check(1, 1);
                for (var i = 0; i < weights.Length; i++) {
                    Check(i, i);
                    if (i > 0) {
                        Check(VRCFuryEditorUtils.NextFloatDown(i), -1);
                        Check(i, i);
                        Check(VRCFuryEditorUtils.NextFloatUp(i), -1);
                    }
                }
                Check(1, 1);
                Check(-1, -1);
                Check(14, 14);
                Check(15, -1);
                Check(2, 2);
                Check(2.5f, -1);
                if (instant) {
                    Check(1, 1);
                    playable.SetFloat("Voice", 0.1f);
                    graph.Evaluate(1f / 60);
                    Assert.That(playable.GetFloat(actionOutput), Is.Zero);
                    Assert.That(target.activeSelf, Is.False);
                    playable.SetFloat("Voice", VRCFuryEditorUtils.NextFloatUp(0.1f));
                    Check(1, 1);
                }
            } finally {
                if (graph.IsValid()) graph.Destroy();
                Object.DestroyImmediate(root);
                foreach (var asset in assets.Distinct()) Object.DestroyImmediate(asset);
            }
        }
    }
}
