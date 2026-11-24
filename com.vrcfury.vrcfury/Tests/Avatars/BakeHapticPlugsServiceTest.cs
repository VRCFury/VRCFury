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

    internal class BakeHapticPlugsMeasurementsTest : SpsAvatarBuildTestBase {
        [TestCase(0.5f)]
        [TestCase(2f)]
        public void PlugBakeAfterResizingReportsLengthAndInjectsConstantDimensions(float resize) {
            if (!BuildTargetUtils.IsDesktop()) Assert.Ignore("Requires desktop SPS baking");
            if (!VF.Menu.HapticsToggleMenuItem.Get()) Assert.Ignore("OGB haptics are disabled");
            var plug = root.AddComponent<VRCFuryHapticPlug>();
            plug.name = "Test";
            plug.autoRenderer = plug.autoLength = plug.autoRadius = plug.enableSps = false;
            plug.unitsInMeters = true;
            plug.length = 0.4f;
            plug.radius = 0.05f;
            injector.Set("componentObject", new Func<VFGameObject>(() => root));
            var globals = injector.GetService<GlobalsService>();
            globals.allBuildersInRun = new List<VF.Feature.Base.FeatureBuilder>();
            globals.allFeaturesInRun = new List<FeatureModel> {
                new SpsOptions { autoModeAvailable = false, legacyModeAvailable = false }
            };
            globals.addOtherFeature = _ => { };
            var constants = new[] {
                (SpsPlugLengthLocal, 0.4f), (SpsPlugRadiusLocal, 0.05f),
                (SpsPlugLengthPlugLengths, 1f), (SpsPlugRadiusPlugLengths, 0.125f)
            };
            foreach (var (source, _) in constants) {
                fx.GetOrCreateFloat("Injected" + source, 77);
                injector.GetService<ParameterInjectService>().Register(new ParameterInjectService.Request {
                    sourceObject = root, sourceParam = source, resolvedParam = "Injected" + source
                });
            }
            var baker = injector.GetService<BakeHapticPlugsService>();
            baker.ApplyEarlyBake();
            root.transform.localScale = Vector3.one * resize;
            baker.Apply();
            var multiplier = injector.GetService<ExpressionParamsService>().Single(p => p.name == "OGB/Plug/Test/LengthMult2");
            Assert.That(multiplier.defaultValue, Is.EqualTo(40 * resize).Within(0.0001f));
            Start();
            Evaluate();
            foreach (var (source, expected) in constants) {
                Assert.That(playable.GetFloat("Injected" + source), Is.EqualTo(expected).Within(0.0001f));
            }
            Assert.That(playable.GetFloat("OGB/Plug/Test/LengthMult1") * multiplier.defaultValue,
                Is.EqualTo(0.4f * resize).Within(0.0001f));
        }

        [Test]
        public void PlugCalculationAndRadiusGatesFollowParentAndSpsToggles(
            [Values(false, true)] bool optimize,
            [Values(false, true)] bool spsToggle,
            [Values(false, true)] bool motionTime
        ) {
            if (!BuildTargetUtils.IsDesktop()) Assert.Ignore("Requires desktop SPS baking");
            optimizeTrees = optimize;
            injector.Set("componentObject", new Func<VFGameObject>(() => root));
            var globals = injector.GetService<GlobalsService>();
            globals.allBuildersInRun = new List<VF.Feature.Base.FeatureBuilder>();
            globals.allFeaturesInRun = new List<FeatureModel> {
                new SpsOptions { autoModeAvailable = false, legacyModeAvailable = false }
            };
            globals.addOtherFeature = _ => { };
            var parent = GameObjects.Create("Plug parent", root);
            var obj = GameObjects.Create("Plug", parent);
            var plug = obj.AddComponent<VRCFuryHapticPlug>();
            plug.name = "Test";
            plug.autoRenderer = plug.autoLength = plug.autoRadius = plug.enableSps = false;
            plug.unitsInMeters = true;
            plug.length = 0.4f;
            plug.radius = 0.05f;
            var action = new VRCFuryHapticSocket.DepthActionNew {
                units = VRCFuryHapticSocket.DepthActionUnits.Meters, smoothingSeconds = 0.2f
            };
            if (motionTime) {
                var clip = VFClip.Create("Depth motion time");
                clip.SetAap("Action output", AnimationCurve.Linear(0, 0, 1, 1));
                action.actionSet.actions.Add(new VF.Model.StateAction.AnimationClipAction { vfClip = clip });
            } else {
                action.actionSet.actions.Add(new VF.Model.StateAction.FxFloatAction { name = "Action output", value = 1 });
            }
            plug.depthActions2.Add(action);
            var output = fx.GetOrCreateFloat("Action output");
            var injected = new[] {
                (SpsDepthMeters, -0.3f, 100f), (SpsVelocityMeters, -0.6f, 0f),
                (SpsDepthPlugLengths, -0.75f, 100f), (SpsPlugLengthMeters, 0.4f, 0.4f),
                (SpsPlugRadiusMeters, 0.05f, 0.05f), (SpsPlugLengthPlugLengths, 1f, 1f)
            };
            foreach (var (source, _, _) in injected) {
                fx.GetOrCreateFloat("Injected" + source, 77);
                injector.GetService<ParameterInjectService>().Register(new ParameterInjectService.Request {
                    sourceObject = obj, sourceParam = source, resolvedParam = "Injected" + source
                });
            }
            var parentEnabled = fx.CreateUniqueFloat("Parent enabled", 1);
            parentEnabled.MakeExpressionFloat(synced: false);
            var parentOn = VFClip.Create("Parent on");
            var parentOff = VFClip.Create("Parent off");
            if (spsToggle) {
                var builder = injector.GetService<VF.Actions.SpsOnActionBuilder>();
                var enable = new VF.Model.StateAction.SpsOnAction { target = plug };
                parentOn = builder.Build(enable);
                parentOff = builder.BuildOff(enable);
            } else {
                parentOn.SetEnabled(parent, true);
                parentOff.SetEnabled(parent, false);
            }
            fx.NewLayer("Parent toggle").NewState("Parent toggle").WithAnimation(
                VFBlendTree1D.CreateWithData("Parent toggle", parentEnabled, (0, parentOff), (1, parentOn)));
            var baker = injector.GetService<BakeHapticPlugsService>();
            baker.ApplyEarlyBake();
            baker.Apply();
            baker.ApplySpsRewrites();
            var receivers = injector.GetService<SpsPlugReceiverService>().GetReceivers(plug);
            var othersName = receiverAnimator.GetParameter(receivers, Party.Others).name;
            var mergedName = receiverAnimator.GetParameter(receivers, Party.Merged).name;
            Start();
            var others = root.GetComponentsInChildren<VRCContactReceiver>(true).Single(r => r.parameter == othersName);
            var merged = root.GetComponentsInChildren<VRCContactReceiver>(true).First(r => r.parameter == mergedName);
            var position = fx.parameters.Single(p => p.name.EndsWith("Test - Socket position"));
            var smoothingValues = fx.parameters.Where(p => p.name.Contains("/Mapped") || p.name.Contains("/Smoothed")).ToArray();
            void Contact(float proximity, float separation) {
                Set(others, proximity, separation);
                Set(merged, proximity, separation);
                playable.SetFloat(othersName + "_SenderVelocityZ", -0.6f);
                playable.SetFloat(mergedName + "_SenderVelocityZ", -0.6f);
            }
            Contact(1, 0.1f);
            Evaluate(120);
            Assert.That(playable.GetFloat(output), Is.EqualTo(1).Within(0.002f));
            Assert.That(playable.GetFloat(position), Is.EqualTo(0.25f).Within(0.002f));
            if (spsToggle) {
                playable.SetFloat(parentEnabled, .25f);
                Evaluate(30);
                Assert.That(injector.GetService<PlugFrameService>().Get(plug).active, Is.True,
                    "Fractional SPS weights must keep contacts and calculations enabled.");
                Assert.That(playable.GetFloat(output), Is.EqualTo(.25f).Within(.002f));
                playable.SetFloat(parentEnabled, 1);
                Evaluate(30);
            }
            foreach (var (source, active, _) in injected) {
                Assert.That(playable.GetFloat("Injected" + source), Is.EqualTo(active).Within(0.002f));
            }
            playable.SetFloat(parentEnabled, 0);
            // Include activation-delta decoding before the calculation gate and its dependent outputs.
            Evaluate(4);
            Assert.That(playable.GetFloat(output), Is.Zero.Within(0.001f));
            Assert.That(playable.GetFloat(position), Is.EqualTo(1).Within(0.001f));
            foreach (var (source, _, disabled) in injected) {
                Assert.That(playable.GetFloat("Injected" + source), Is.EqualTo(disabled).Within(0.002f));
            }
            foreach (var value in smoothingValues) Assert.That(playable.GetFloat(value), Is.Zero.Within(0.001f));
            foreach (var receiver in root.GetComponentsInChildren<VRCContactReceiver>(true).Where(r => !string.IsNullOrEmpty(r.parameter))) {
                Assert.That(playable.IsParameterControlledByCurve(receiver.parameter), Is.False);
            }
            Evaluate(20);
            Contact(0, -1);
            playable.SetFloat(parentEnabled, 1);
            Evaluate(120);
            Assert.That(playable.GetFloat(output), Is.Zero.Within(0.001f));
            Assert.That(playable.GetFloat("Injected" + SpsDepthMeters), Is.EqualTo(100).Within(0.001f));
        }
    }
}
#endif

namespace VF.Tests {
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

    internal class BakeHapticPlugsPathReceiverTest : SpsPlugReceiverTestBase {
#if VRCSDK_HAS_MATERIAL_CONTACTS
        [Test]
        public void AvatarPathReceiverRunsOnlyWhileAnEntranceIsInRange() {
            if (!BuildTargetUtils.IsDesktop()) Assert.Ignore("Requires desktop SPS baking");
            var avatar = root.AddComponent<VRCAvatarDescriptor>();
            avatar.customizeAnimationLayers = true;
            avatar.baseAnimationLayers = new[] {
                new VRCAvatarDescriptor.CustomAnimLayer { type = VRCAvatarDescriptor.AnimLayerType.FX }
            };
            avatar.specialAnimationLayers = Array.Empty<VRCAvatarDescriptor.CustomAnimLayer>();
            injector = VRCFuryInjectorBuilder.GetInjector(avatar);
            injector.Set("componentObject", new Func<VFGameObject>(() => root));
            var globals = injector.GetService<GlobalsService>();
            globals.allFeaturesInRun = new List<FeatureModel>();
            globals.addOtherFeature = _ => { };
            var controllers = injector.GetService<ControllersService>();
            controllers.ClearCache(applyBaseMask: false);
            var fx = controllers.GetFx();
            fx.GetOrCreateFloat(VFBlendTreeDirect.AlwaysOneParam, 1);
            var mesh = new Mesh {
                vertices = new[] { Vector3.zero, Vector3.right * 0.02f, Vector3.forward * 0.2f },
                triangles = new[] { 0, 1, 2 }
            };
            mesh.RecalculateNormals();
            var source = VrcfObjectFactory.CreateMaterial(Shader.Find("Unlit/Color"));
            var assets = new List<Object> { mesh, source };
            PlayableGraph graph = default;
            try {
                var obj = GameObjects.Create("Mesh", root);
                obj.AddComponent<MeshFilter>().sharedMesh = mesh;
                var renderer = obj.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = source;
                plug.configureTpsMesh.Add(renderer);
                plug.enableSps = true;
                plug.spsAutorig = false;
                var baker = injector.GetService<BakeHapticPlugsService>();
                baker.ApplyEarlyBake();
                baker.Apply();
                injector.GetService<ChangeSpsChannelsService>().Apply();
                injector.GetService<IsObjectEnabledService>().Apply();
                injector.GetService<ObjectTreeService>().Apply();
                injector.GetService<NoBadControllerParamsService>().Apply();

                var contacts = root.GetComponentsInChildren<VRCContactReceiver>(true);
                var path = contacts.Single(c => c.separateShaderParameter == "SPS_Path");
                var self = contacts.Single(c => c.separateShaderParameter == "SPS_Self");
                var others = contacts.Single(c => c.separateShaderParameter == "SPS_Others");
                Assert.That(path.gameObject.activeInHierarchy, Is.False);
                Assert.That(self.localOnly || others.localOnly, Is.False);
                var context = new VFSaveContext(root, false);
                var controller = new AnimatorController {
                    parameters = fx.parameters.Select(p => p.Save()).ToArray(),
                    layers = fx.GetLayers().Select(layer => layer.Save(context)).ToArray()
                };
                assets.Add(controller);
                assets.AddRange(context.NewAssets);
                var animator = root.AddComponent<Animator>();
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                graph = PlayableGraph.Create("SPS path receiver activation");
                graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
                var playable = AnimatorControllerPlayable.Create(graph, controller);
                AnimationPlayableOutput.Create(graph, "FX", animator).SetSourcePlayable(playable);
                graph.Play();

                void Check(float selfValue, float othersValue, bool expected) {
                    playable.SetFloat(self.parameter, selfValue);
                    playable.SetFloat(others.parameter, othersValue);
                    graph.Evaluate(1f / 60);
                    Assert.That(path.gameObject.activeInHierarchy, Is.EqualTo(expected));
                }
                Check(0, 0, false);
                Check(0.5f, 0, true);
                Check(0, 0.5f, true);
                Check(0, 0, false);
            } finally {
                if (graph.IsValid()) graph.Destroy();
                foreach (var asset in assets.Distinct()) if (asset != null) Object.DestroyImmediate(asset);
            }
        }
#endif
    }
}

namespace VF.Tests {
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
    using VF.Component;
    using VF.Injector;
    using VF.Inspector;
    using VF.Model.StateAction;
    using VF.Service;
    using VF.Utils;
    using VF.Utils.Controller;
    using VRC.SDK3.Avatars.Components;
    using VRC.SDK3.Dynamics.Contact.Components;
    using Object = UnityEngine.Object;
    using Flags = VF.Component.VRCFuryHapticSocket.AddLight;
    using ScaleFlags = VF.Builder.Haptics.SpsTags.SocketScaleFlagsX;
    using Party = VF.Builder.Haptics.SelfAndOthersReceiverService.Party;

    internal class BakeHapticPlugsEnableAnimationTest : SpsSenderBakeTestBase {
        [TestCase(false, false)]
        [TestCase(false, true)]
        [TestCase(true, false)]
        [TestCase(true, true)]
        public void EnableAnimationsReachPlugRendererAndBakeRoot(bool componentBinding, bool skinned) {
            var avatar = root.AddComponent<VRCAvatarDescriptor>();
            avatar.customizeAnimationLayers = true;
            avatar.baseAnimationLayers = new[] {
                new VRCAvatarDescriptor.CustomAnimLayer { type = VRCAvatarDescriptor.AnimLayerType.FX }
            };
            avatar.specialAnimationLayers = Array.Empty<VRCAvatarDescriptor.CustomAnimLayer>();
            injector.Set("avatarObject", root.asVf());
            injector.Set("componentObject", new Func<VFGameObject>(() => root));
            injector.Set(typeof(VRCAvatarDescriptor), avatar);
            injector.Set(new GlobalsService { avatarObject = root });
            var plug = root.AddComponent<VRCFuryHapticPlug>();
            var rendererObject = GameObjects.Create("Renderer", root);
            Renderer renderer = skinned
                ? (Renderer)rendererObject.AddComponent<SkinnedMeshRenderer>()
                : rendererObject.AddComponent<MeshRenderer>();
            var bakeRoot = GameObjects.Create("Baked", root);
            var sourceCurve = AnimationCurve.Linear(0, 0, 1, 1);
            var clip = VFClip.Create("SPS Enable Test");
            clip.SetCurve(componentBinding ? (UnityEngine.Object)plug : renderer,
                componentBinding ? "spsAnimatedEnabled" : "material._TPS_AnimatedToggle", sourceCurve);
            var unrelated = GameObjects.Create("Unrelated", root);
            clip.SetCurve((Transform)unrelated, "m_LocalPosition.x", sourceCurve);
            clip.SetAap("Unrelated AAP", 0.3f);
            injector.GetService<AllClipsService>().AddAdditionalManagedClip(clip);
            injector.GetService<ControllersService>().ClearCache(applyBaseMask: false);
            var service = injector.GetService<BakeHapticPlugsService>();
            ((Dictionary<VRCFuryHapticPlug, VRCFuryHapticPlugEditor.BakeResult>)
                typeof(BakeHapticPlugsService).VFField("bakeResults").GetValue(service))
                [plug] = new VRCFuryHapticPlugEditor.BakeResult { bakeRoot = bakeRoot };
            var rewrites = (List<BakeHapticPlugsService.SpsRewriteToDo>)typeof(BakeHapticPlugsService)
                .GetField("spsRewritesToDo", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                .GetValue(service);
            rewrites.Add(new BakeHapticPlugsService.SpsRewriteToDo {
                plugObject = root,
                skin = renderer,
                bakeRoot = bakeRoot,
                spsBlendshapes = Array.Empty<string>(),
                configureMaterial = (_, material) => material
            });
            service.ApplySpsRewrites();
            Assert.That(clip.GetFloatCurves().Single(pair => pair.Item1.Targets(unrelated)).Item2.keys, Is.EqualTo(sourceCurve.keys));
            Assert.That(clip.GetFloatCurves().Single(pair => pair.Item1.propertyName == "Unrelated AAP").Item2.Evaluate(0), Is.EqualTo(0.3f));
            var curves = clip.GetFloatCurves();
            var enabledCurve = curves.Single(pair => pair.Item1.Targets(rendererObject)
                && pair.Item1.type == renderer.GetType() && pair.Item1.propertyName == "material._SPS_Enabled").Item2;
            var weight = ((Dictionary<VRCFuryHapticPlug, VFAParam>)
                typeof(BakeHapticPlugsService).VFField("enabledWeights").GetValue(service))[plug];
            var activeCurve = curves.Single(pair => pair.Item1.propertyName == weight.name).Item2;
            foreach (var time in new[] { 0f, 0.5f, 1f }) {
                Assert.That(enabledCurve.Evaluate(time), Is.EqualTo(sourceCurve.Evaluate(time)));
                Assert.That(activeCurve.Evaluate(time), Is.EqualTo(sourceCurve.Evaluate(time)));
            }
        }
    }
}

namespace VF.Tests {
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

    internal class BakeHapticPlugsPlatformAndMaterialTest : SpsChannelBuildTestBase {
        [TestCase(false)]
        [TestCase(true)]
        public void NonDesktopSpsOnlyCreatesMenusAndParameters(bool configuredRenderer) {
            if (BuildTargetUtils.IsDesktop()) Assert.Ignore("Requires a non-desktop build target");
            injector.Set("componentObject", new System.Func<VFGameObject>(() => root));
            var globals = injector.GetService<GlobalsService>();
            globals.allBuildersInRun = new List<VF.Feature.Base.FeatureBuilder>();
            globals.allFeaturesInRun = new List<FeatureModel> {
                new SpsOptions { addDpsTipLight = true, tpsDepthAnimationCompatibility = true, saveSockets = true }
            };
            globals.addOtherFeature = _ => Assert.Fail("Non-desktop SPS must not add runtime features");
            var plug = GameObjects.Create("Plug", root).AddComponent<VRCFuryHapticPlug>();
            var socket = GameObjects.Create("Socket", root).AddComponent<VRCFuryHapticSocket>();
            socket.name = "Socket One";
            var secondSocket = GameObjects.Create("Second Socket", root).AddComponent<VRCFuryHapticSocket>();
            secondSocket.name = "Socket Two";
            var untouched = GameObjects.Create("Animation target", root);
            var animation = new State {
                actions = { new ObjectToggleAction { obj = untouched, mode = ObjectToggleAction.Mode.TurnOff } }
            };
            plug.postBakeActions = animation;
            plug.depthActions2.Add(new VRCFuryHapticSocket.DepthActionNew { actionSet = animation });
            socket.depthActions2.Add(new VRCFuryHapticSocket.DepthActionNew { actionSet = animation });
            socket.plugEntryRadiusActions.Add(new VRCFuryHapticSocket.PlugEntryRadiusAction { actionSet = animation });
            socket.activeActions = animation;
            MeshFilter filter = null;
            MeshRenderer renderer = null;
            Mesh mesh = null;
            Material material = null;
            string materialBefore = null;
            if (configuredRenderer) {
                mesh = new Mesh { vertices = new[] { Vector3.zero, Vector3.right, Vector3.forward }, triangles = new[] { 0, 1, 2 } };
                material = new Material(Shader.Find("Unlit/Color"));
                assets.Add(mesh);
                assets.Add(material);
                filter = plug.gameObject.AddComponent<MeshFilter>();
                filter.sharedMesh = mesh;
                renderer = plug.gameObject.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = material;
                plug.autoRenderer = false;
                plug.configureTpsMesh.Add(renderer);
                materialBefore = EditorJsonUtility.ToJson(material);
            }
            var physbone = root.AddComponent<VRCPhysBone>();
            // Neither a missing deformation renderer nor invalid scale should reach the bakers.
            plug.owner().localScale = socket.owner().localScale = new Vector3(1, 2, 3);
            var channels = injector.GetService<SpsChannelsService>();
            var fx = injector.GetService<ControllersService>().GetFx();
            var layers = fx.GetLayers().Count();
            var controllers = injector.GetService<ControllersService>();
            var allLayers = controllers.GetAllUsedControllers().Sum(c => c.GetLayers().Count());
            var components = root.GetComponentsInChildren<UnityEngine.Component>(true);
            var initialParams = fx.parameters.Select(p => p.name).ToArray();
            Assert.That(injector.GetService<VRCFuryHapticPlugBaker>().Bake(plug), Is.Null);
            Assert.That(injector.GetService<VRCFuryHapticSocketBaker>().Bake(socket), Is.Null);
            injector.GetService<BakeHapticPlugsService>().ApplyEarlyBake();
            injector.GetService<BakeHapticPlugsService>().Apply();
            injector.GetService<BakeHapticSocketsService>().Apply();
            injector.GetService<ChangeSpsChannelsService>().Apply();
            injector.GetService<SpsOptionsService>().MoveMenus();
            Assert.That(fx.GetLayers().Count(), Is.EqualTo(layers));
            Assert.That(controllers.GetAllUsedControllers().Sum(c => c.GetLayers().Count()), Is.EqualTo(allLayers),
                "No calculation, toggle, or socket exclusivity layers in any controller");
            Assert.That(root.GetComponentsInChildren<UnityEngine.Component>(true), Is.EquivalentTo(components));
            Assert.That(root.GetComponentsInChildren<VRCContactSender>(true), Is.Empty);
            Assert.That(root.GetComponentsInChildren<VRCContactReceiver>(true), Is.Empty);
            Assert.That(root.GetComponentsInChildren<Light>(true), Is.Empty);
            Assert.That(untouched.active, Is.True, "Post-bake and depth actions must not run");
            if (configuredRenderer) {
                Assert.That(filter.sharedMesh, Is.SameAs(mesh), "No mesh baking or autorigging");
                Assert.That(renderer.sharedMaterial, Is.SameAs(material), "No deformation material replacement");
                Assert.That(EditorJsonUtility.ToJson(material), Is.EqualTo(materialBefore), "No shader patching or material changes");
            }
            Assert.That(channels.Baked, Is.Empty);
            Assert.That(physbone.ignoreTransforms, Does.Contain((Transform)plug.owner()));
            Assert.That(physbone.ignoreTransforms, Does.Contain((Transform)socket.owner()));
            Assert.That(physbone.ignoreTransforms, Does.Contain((Transform)secondSocket.owner()));
            var menu = injector.GetService<MenuService>().GetReadOnlyMenu().controls.Single(c => c.name == "SPS").subMenu;
            Assert.That(menu.controls.Count, Is.EqualTo(4), "Both sockets, Auto, and Legacy must remain");
            Assert.That(menu.controls.Count(c => c.name == "Socket One" || c.name == "Socket Two"), Is.EqualTo(2));
            Assert.That(menu.controls.Count(c => c.name.Contains("Auto")), Is.EqualTo(1));
            Assert.That(menu.controls.Count(c => c.name.Contains("Legacy Support")), Is.EqualTo(1));
            var menuParams = menu.controls.Select(c => c.parameter.name).ToArray();
            Assert.That(injector.GetService<ExpressionParamsService>().Select(p => p.name), Is.EquivalentTo(menuParams),
                "Only menu expression parameters should be created, with no OGB outputs");
            Assert.That(fx.parameters.Select(p => p.name).Except(initialParams), Is.EquivalentTo(menuParams),
                "No measurement, haptic, smoothing, or deformation parameters");
        }

#if VRCSDK_HAS_MATERIAL_CONTACTS
        [Test]
        public void DisabledPlugHierarchyIgnoresStaleContactMaterialData(
            [Values] bool toggleParent,
            [Values] bool initiallyActive,
            [Values] bool writeDefaults,
            [Values] bool animatedObject
        ) {
            if (!BuildTargetUtils.IsDesktop()) Assert.Ignore("Requires desktop SPS baking");
            GameObject parent = GameObjects.Create("Plug parent", root);
            GameObject obj = GameObjects.Create("Plug", parent);
            var target = toggleParent ? parent : obj;
            target.SetActive(initiallyActive);
            var plug = Plug(obj, false);
            plug.enableSps = true;
            plug.spsAutorig = false;
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
            assets.Add(mesh);
            mesh.RecalculateNormals();
            GameObject meshObject = GameObjects.Create("Visible mesh", root);
            meshObject.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = meshObject.AddComponent<MeshRenderer>();
            var material = new Material(Shader.Find("Unlit/Color"));
            assets.Add(material);
            renderer.sharedMaterial = material;
            plug.configureTpsMesh.Add(renderer);

            injector.Set("componentObject", new System.Func<VFGameObject>(() => root));
            var globals = injector.GetService<GlobalsService>();
            globals.addOtherFeature = _ => { };
            globals.allFeaturesInRun.Add(new FixWriteDefaults {
                mode = writeDefaults ? FixWriteDefaults.FixWriteDefaultsMode.ForceOn : FixWriteDefaults.FixWriteDefaultsMode.ForceOff
            });
            var defaults = injector.GetService<FixWriteDefaultsService>();
            defaults.DetermineWriteDefaultsStrategy();
            var deformOn = Track(VFClip.Create("Deformation on"));
            var deformOff = Track(VFClip.Create("Deformation off"));
            deformOn.SetCurve(plug, "spsAnimatedEnabled", 1);
            deformOff.SetCurve(plug, "spsAnimatedEnabled", 0);
            var fx = injector.GetService<ControllersService>().GetFx();
            var objectOn = fx.CreateUniqueFloat("Plug object on", initiallyActive);
            var deformationOn = fx.CreateUniqueFloat("Deformation on", true);
            var controls = VFBlendTreeDirect.Create("Plug controls");
            if (animatedObject) {
                var on = Track(VFClip.Create("Plug object on"));
                var off = Track(VFClip.Create("Plug object off"));
                on.SetEnabled(target, true);
                off.SetEnabled(target, false);
                controls.Add(VFBlendTree1D.CreateWithData("Object", objectOn, (0, off), (1, on)));
            }
            controls.Add(VFBlendTree1D.CreateWithData("Deformation", deformationOn, (0, deformOff), (1, deformOn)));
            var plugs = injector.GetService<BakeHapticPlugsService>();
            plugs.ApplyEarlyBake();
            plugs.Apply();
            plugs.ApplySpsRewrites();
            injector.GetService<ChangeSpsChannelsService>().Apply();
            injector.GetService<IsObjectEnabledService>().Apply();
            injector.GetService<ObjectTreeService>().Apply();
            animatorPrepared = true;
            defaults.RecordAllDefaults();
            defaults.AdjustWriteDefaults();
            injector.GetService<FixEmptyMotionService>().Apply();

            Assert.That(renderer.sharedMaterial.HasProperty("_SPS_ObjectEnabled"), Is.True);
            var contactProperty = "_VRChatContact_Data_SPS_Self";
            var properties = new MaterialPropertyBlock();
            properties.SetVectorArray(contactProperty, new[] { new Vector4(1, 2, 3, 4) });
            renderer.SetPropertyBlock(properties);
            RunAnimation(controls, (graph, playable) => {
                foreach (var (active, deformation) in new[] {
                    (initiallyActive, true),
                    (false, true),
                    (true, true),
                    (true, false),
                    (false, false),
                    (false, true),
                    (true, true)
                }) {
                    playable.SetFloat(objectOn, active ? 1 : 0);
                    playable.SetFloat(deformationOn, deformation ? 1 : 0);
                    for (var i = 0; i < 10; i++) graph.Evaluate(1f / 60);
                    renderer.GetPropertyBlock(properties);
                    var expected = animatedObject ? active : initiallyActive;
                    Assert.That(obj.activeInHierarchy, Is.EqualTo(expected));
                    Assert.That(renderer.gameObject.activeInHierarchy, Is.True);
                    Assert.That(properties.GetFloat("_SPS_ObjectEnabled"), Is.EqualTo(expected && deformation ? 1 : 0));
                    Assert.That(properties.GetFloat("_SPS_Enabled"), Is.EqualTo(deformation ? 1 : 0));
                    Assert.That(properties.GetVectorArray(contactProperty)[0], Is.EqualTo(new Vector4(1, 2, 3, 4)),
                        "Contact data stays populated while deformation is independently disabled");
                }
            });
        }
#endif
    }
}
