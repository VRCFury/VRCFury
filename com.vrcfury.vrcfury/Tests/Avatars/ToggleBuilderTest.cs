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
using VF.Exceptions;
using VF.Feature;
using VF.Feature.Base;
using VF.Model.Feature;
using VF.Model.StateAction;
using VF.Service;
using VF.Utils;
using VF.Utils.Controller;
using VRC.SDK3.Avatars.Components;
using Object = UnityEngine.Object;

namespace VF.Tests {
    [Category("VRCFury")]
    public class ToggleBuilderTest {
        [TestCase(0, false, true, false, 3, false, 0)]
        [TestCase(0, false, true, true, 3, false, 0)]
#if VRCSDK_HAS_DYNAMIC_PARAMETER_BIT_WIDTH
        [TestCase(0, false, true, false, 4, false, 0)]
        [TestCase(0, false, true, false, 2, true, 0)]
        [TestCase(0, false, true, false, 3, true, 0)]
        [TestCase(2, false, true, false, 2, false, 0)]
        [TestCase(2, false, true, false, 3, false, 0)]
        [TestCase(4, false, true, false, 3, false, 0)]
        [TestCase(4, true, true, false, 3, false, 0)]
        [TestCase(2, false, false, false, 3, false, 0)]
        [TestCase(0, false, true, false, 3, false, 5)]
        [TestCase(0, false, true, false, 4, false, 2)]
        [TestCase(4, true, true, false, 3, false, 4)]
        [TestCase(4, true, true, false, 3, false, 3)]
#endif
        public void SliderFlipbookUsesTheParameterNetworkGrid(
            int existingBits,
            bool unsigned,
            bool synced,
            bool objectsOnly,
            int pages,
            bool inactiveAtZero,
            int overrideBits
        ) {
            var root = new GameObject("Avatar");
            var assets = new List<Object>();
            PlayableGraph graph = default;
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
                var expressionParams = injector.GetService<ExpressionParamsService>();
                const string parameterName = "Presets";
#if VRCSDK_HAS_DYNAMIC_PARAMETER_BIT_WIDTH
                if (existingBits > 0) {
                    var existing = ExpressionParam.Create(parameterName).Float().Synced(synced);
                    existing.bitWidth = (byte)existingBits;
                    existing.flippedRepresentation = unsigned;
                    expressionParams.AddOrMerge(existing);
                }
#endif

                var target = GameObjects.Create("Target", root);
                var renderer = target.AddComponent<MeshRenderer>();
                var shader = Shader.Find("Standard");
                var toggle = new Toggle {
                    slider = true,
                    sliderBitWidthOverride = overrideBits,
                    sliderInactiveAtZero = inactiveAtZero,
                    name = parameterName,
                    usePrefixOnParam = false,
                    addMenuItem = false
                };
                var flipbook = new FlipBookBuilderAction();
                toggle.state.actions.Add(flipbook);
                var materials = new Material[pages];
                for (var i = 0; i < pages; i++) {
                    var page = new FlipBookBuilderAction.FlipBookPage();
                    var pose = VFClip.Create("Page " + i);
                    if (!objectsOnly) pose.SetCurve(target.GetComponent<Transform>(), "m_LocalPosition.x", i + 1);
                    var material = materials[i] = new Material(shader) { name = "Material " + i };
                    assets.Add(material);
                    pose.SetCurve(renderer, "m_Materials.Array.data[0]", material);
                    page.state.actions.Add(new AnimationClipAction { vfClip = pose });
                    flipbook.pages.Add(page);
                }
                renderer.sharedMaterial = materials[pages - 1];
                var builder = (ToggleBuilder)FeatureFinder.GetBuilder(toggle, root, injector);
                builder.featureBaseObject = root;
                var first = inactiveAtZero ? 1 : 0;
#if VRCSDK_HAS_DYNAMIC_PARAMETER_BIT_WIDTH
                if (existingBits > 0 && overrideBits > 0 && (overrideBits != existingBits || !unsigned)) {
                    var error = Assert.Throws<Exception>(() => builder.Apply());
                    Assert.That(error.Message, Does.Contain("conflicts with the existing parameter"));
                    return;
                }
                var width = Math.Max(existingBits, overrideBits);
                if (width == 0) width = Math.Max(2, Mathf.CeilToInt(Mathf.Log(pages + first + 1, 2)));
                var isUnsigned = existingBits == 0 || unsigned;
                var intervals = synced ? ((1 << width) - 2) / (isUnsigned ? 1 : 2) : 0;
#else
                const int intervals = 127;
#endif
                if (intervals > 0 && pages > intervals - first + 1) {
                    var error = Assert.Throws<TargetInvocationException>(() => builder.Apply());
                    Assert.That(error.InnerException, Is.TypeOf<VRCFBuilderException>());
                    Assert.That(error.InnerException.Message, Does.Contain($"this slider can sync only {intervals - first + 1}"));
                    return;
                }
                builder.Apply();
                var parameter = expressionParams.Single(p => p.name == parameterName);
#if VRCSDK_HAS_DYNAMIC_PARAMETER_BIT_WIDTH
                Assert.That(parameter.bitWidth, Is.EqualTo(width));
                Assert.That(parameter.flippedRepresentation, Is.EqualTo(isUnsigned));
#endif
                Assert.That(parameter.IsNetworkSynced(), Is.EqualTo(synced));
#if VRCSDK_HAS_PARAMETER_INTERPOLATION
                Assert.That(parameter.useCustomLerpDuration, Is.EqualTo(synced));
                if (synced) Assert.That(parameter.customLerpDuration, Is.Zero);
#endif

                var context = new VFSaveContext(root, false);
                var controller = new AnimatorController {
                    parameters = fx.parameters.Select(p => p.Save()).ToArray(),
                    layers = fx.GetLayers().Select(layer => layer.Save(context)).ToArray()
                };
                assets.Add(controller);
                assets.AddRange(context.NewAssets);
                var animator = root.AddComponent<Animator>();
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                graph = PlayableGraph.Create("Flipbook slider");
                graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
                var playable = AnimatorControllerPlayable.Create(graph, controller);
                AnimationPlayableOutput.Create(graph, "Animation", animator).SetSourcePlayable(playable);
                graph.Play();

                void Check(float slider, int page) {
                    playable.SetFloat(parameterName, slider);
                    graph.Evaluate(1f / 60);
                    graph.Evaluate(1f / 60);
                    if (!objectsOnly) Assert.That(target.localPosition.x, Is.EqualTo(page).Within(0.001f), $"Slider at {slider:R}");
                    Assert.That(renderer.sharedMaterial, Is.SameAs(materials[page - 1]), $"Slider at {slider:R}");
                }
                if (inactiveAtZero) {
                    playable.SetFloat(parameterName, 0.5f / intervals);
                    graph.Evaluate(1f / 60);
                    graph.Evaluate(1f / 60);
                    Assert.That(playable.GetCurrentAnimatorStateInfo(0).IsName("Off"), Is.True);
                }
                Check(inactiveAtZero ? 1f / intervals : 0, 1);
                Check(1, pages);
                for (var page = 1; page < pages; page++) {
                    var boundary = (float)page / pages;
                    if (intervals > 0) {
                        var firstCode = Math.Max(first + page, Math.Ceiling((double)page * intervals / pages));
                        boundary = (float)((firstCode - 0.5) / intervals);
                        foreach (var slider in new[] {
                                     VF.Inspector.VRCFuryEditorUtils.NextFloatDown(boundary),
                                     boundary,
                                     VF.Inspector.VRCFuryEditorUtils.NextFloatUp(boundary)
                                 }) {
                            Check(slider, Mathf.RoundToInt(slider * intervals) >= firstCode ? page + 1 : page);
                        }
                    }
                    Check(boundary - 0.0001f, page);
                    Check(boundary + 0.0001f, page + 1);
                }
            } finally {
                if (graph.IsValid()) graph.Destroy();
                var avatar = root.GetComponent<VRCAvatarDescriptor>();
                if (avatar.expressionParameters != null) assets.Add(avatar.expressionParameters);
                Object.DestroyImmediate(root);
                foreach (var asset in assets.Distinct()) if (asset != null) Object.DestroyImmediate(asset);
            }
        }
    }
}
