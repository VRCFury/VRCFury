using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;
using VF.Actions;
using VF.Builder;
using VF.Component;
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
    public class BakeHapticPlugsServiceTest {
#if VRCSDK_HAS_MATERIAL_CONTACTS
        [TestCase("Clip loaded before baking")]
        [TestCase("Animation Clip action")]
        [TestCase("Material Swap action")]
        public void MaterialSwapSurvivesMeshRendererConversion(string source) {
            if (!BuildTargetUtils.IsDesktop()) Assert.Ignore("Requires desktop SPS baking");

            var root = new GameObject("Avatar");
            var mesh = new Mesh {
                vertices = new[] {
                    new Vector3(-0.015f, 0, 0),
                    new Vector3(0.015f, 0, 0),
                    new Vector3(-0.015f, 0, 0.4f),
                    new Vector3(0.015f, 0, 0.4f)
                },
                triangles = new[] { 0, 2, 1, 1, 2, 3 },
                uv = new[] { Vector2.zero, Vector2.right, Vector2.up, Vector2.one }
            };
            var original = new Material(Shader.Find("Unlit/Color"));
            var resting = new Material(original);
            resting.SetColor("_Color", Color.green);
            var replacement = new Material(original);
            replacement.SetColor("_Color", Color.red);
            var clip = new AnimationClip();
            AnimationClip output = null;
            try {
                mesh.RecalculateNormals();
                var deltas = new Vector3[mesh.vertexCount];
                deltas[0] = Vector3.right * .01f;
                mesh.AddBlendShapeFrame("Inflate", 100, deltas, null, null);
                var descriptor = root.AddComponent<VRCAvatarDescriptor>();
                descriptor.customizeAnimationLayers = true;
                descriptor.baseAnimationLayers = new[] {
                    new VRCAvatarDescriptor.CustomAnimLayer { type = VRCAvatarDescriptor.AnimLayerType.FX }
                };
                descriptor.specialAnimationLayers = Array.Empty<VRCAvatarDescriptor.CustomAnimLayer>();
                var plug = GameObjects.Create("Plug", root).AddComponent<VRCFuryHapticPlug>();
                var renderer = plug.gameObject.AddComponent<MeshRenderer>();
                plug.gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
                renderer.sharedMaterial = original;
                plug.enableSps = true;
                plug.spsAutorig = true;
                plug.spsBlendshapes.Add("Inflate");
                plug.autoRenderer = false;
                plug.configureTpsMesh.Add(renderer);
                plug.postBakeActions = new State {
                    actions = {
                        new MaterialAction { renderer = renderer, mat = resting },
                        new BlendShapeAction {
                            blendShape = "Inflate",
                            blendShapeValue = 25,
                            allRenderers = true
                        }
                    }
                };

                const string property = "m_Materials.Array.data[0]";
                AnimationUtility.SetObjectReferenceCurve(
                    clip,
                    EditorCurveBinding.PPtrCurve("Plug", typeof(MeshRenderer), property),
                    new[] { new ObjectReferenceKeyframe { value = replacement } }
                );
                var materialAction = new MaterialAction { renderer = renderer, mat = replacement };
                var clipAction = new AnimationClipAction { motion = clip };
                var injector = VRCFuryInjectorBuilder.GetInjector(descriptor);
                injector.Set("componentObject", new Func<VFGameObject>(() => root));
                var globals = injector.GetService<GlobalsService>();
                globals.allFeaturesInRun = new List<FeatureModel>();
                globals.addOtherFeature = _ => { };
                injector.GetService<ControllersService>().ClearCache(applyBaseMask: false);
                var animationBuilder = injector.GetService<AnimationClipActionBuilder>();
                var materialBuilder = injector.GetService<MaterialSwapActionBuilder>();
                var swap = source == "Clip loaded before baking" ? (VFClip)animationBuilder.Build(clipAction, root) : null;

                var baker = injector.GetService<BakeHapticPlugsService>();
                baker.ApplyEarlyBake();
                var skin = plug.GetComponent<SkinnedMeshRenderer>();
                Assert.That(skin != null, Is.True);
                injector.GetService<RestingStateService>().OnPhaseChanged();
                Assert.That(renderer != null, Is.True);
                Assert.That(renderer.sharedMaterial, Is.SameAs(resting));
                Assert.That(skin.GetBlendShapeWeight(0), Is.EqualTo(25));
                swap ??= source == "Material Swap action"
                    ? materialBuilder.Build(materialAction)
                    : (VFClip)animationBuilder.Build(clipAction, root);
                injector.GetService<AllClipsService>().AddAdditionalManagedClip(swap);
                baker.Apply();
                baker.ApplySpsRewrites();
                Assert.That(plug.GetComponent<SkinnedMeshRenderer>(), Is.SameAs(skin));
                Assert.That(renderer == null, Is.True);
                Assert.That(plug.GetComponent<MeshFilter>() == null, Is.True);
                Assert.That(skin.sharedMaterial.GetColor("_Color"), Is.EqualTo(Color.green));

                output = (AnimationClip)swap.Save(root);
                var bindings = AnimationUtility.GetObjectReferenceCurveBindings(output);
                Assert.That(bindings.Length, Is.EqualTo(1));
                Assert.That(bindings[0].type, Is.EqualTo(typeof(SkinnedMeshRenderer)));
                Assert.That(bindings[0].path, Is.EqualTo("Plug"));
                var keys = AnimationUtility.GetObjectReferenceCurve(output, bindings[0]);
                var swapMaterial = (Material)keys[0].value;
                Assert.That(swapMaterial.HasProperty("_SPS_Enabled"), Is.True);
                var bakedTexture = (Texture2D)swapMaterial.GetTexture("_SPS_Bake");
                var firstBlendshapeDelta = 1 + 10 * mesh.vertexCount + 1;
                Assert.That(bakedTexture.GetRawTextureData<float>()[firstBlendshapeDelta], Is.EqualTo(.01f).Within(1e-5f));
                var graph = PlayableGraph.Create();
                try {
                    var animator = root.GetComponent<Animator>();
                    if (animator == null) animator = root.AddComponent<Animator>();
                    animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                    var playable = AnimationClipPlayable.Create(graph, output);
                    AnimationPlayableOutput.Create(graph, "Material swap", animator).SetSourcePlayable(playable);
                    graph.Play();
                    graph.Evaluate(1f / 60);
                } finally {
                    graph.Destroy();
                }
                Assert.That(skin.sharedMaterial, Is.SameAs(swapMaterial));
                Assert.That(skin.sharedMaterial.GetColor("_Color"), Is.EqualTo(Color.red));
            } finally {
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(mesh);
                Object.DestroyImmediate(original);
                Object.DestroyImmediate(resting);
                Object.DestroyImmediate(replacement);
                Object.DestroyImmediate(clip);
                if (output != null) Object.DestroyImmediate(output);
            }
        }
#endif
    }
}
