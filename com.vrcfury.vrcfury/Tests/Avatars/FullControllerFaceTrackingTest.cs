#if VRCSDK_HAS_FACE_TRACKING
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using VF.Feature;
using VF.Builder;
using VF.Feature.Base;
using VF.Model;
using VF.Model.Feature;
using VF.Model.StateAction;
using VF.Service;
using VF.Utils;
using VF.Utils.Controller;
using VRC.SDK3.Avatars.Components;
using FtBehaviourControl = VRC.SDKBase.VRC_AnimatorFaceTrackingBlendshapesSet.BlendSet;

namespace VF.Tests {
    [Category("VRCFury")]
    public class FullControllerFaceTrackingTest {
        private static void SetMapping(VRCAvatarDescriptor.FaceTrackingBlendshapesSet set, int index, string name, bool visemes = false, float multiplier = 1) {
            var count = visemes ? (int)VRC.SDKBase.VRC_AvatarDescriptor.Viseme.Count : (int)VRCAvatarDescriptor.FaceTrackingKey.Max;
            var originalNames = visemes ? set.visemeBlendshapeNames : set.faceTrackingBlendshapeNames;
            var originalMultipliers = visemes ? set.visemeBlendshapeMultipliers : set.faceTrackingBlendshapeMultipliers;
            var names = Enumerable.Range(0, count).Select(i => (i == index ? name : originalNames?.ElementAtOrDefault(i)) ?? "").ToArray();
            var multipliers = Enumerable.Range(0, count).Select(i => i == index ? multiplier
                : originalMultipliers != null && i < originalMultipliers.Length ? originalMultipliers[i] : 1).ToArray();
            if (visemes) {
                set.visemeBlendshapeNames = names;
                set.visemeBlendshapeMultipliers = multipliers;
            } else {
                set.faceTrackingBlendshapeNames = names;
                set.faceTrackingBlendshapeMultipliers = multipliers;
            }
        }

        private sealed class Build : System.IDisposable {
            public readonly GameObject root = new GameObject("Avatar");
            public readonly VRCAvatarDescriptor avatar;
            public readonly GlobalsService globals;
            public readonly FaceTrackingService faceTracking = new FaceTrackingService();
            private readonly ControllersService controllers = new ControllersService();
            private readonly List<Mesh> meshes = new List<Mesh>();

            public Build() {
                avatar = root.AddComponent<VRCAvatarDescriptor>();
                avatar.baseAnimationLayers = System.Array.Empty<VRCAvatarDescriptor.CustomAnimLayer>();
                avatar.specialAnimationLayers = System.Array.Empty<VRCAvatarDescriptor.CustomAnimLayer>();
                globals = new GlobalsService {
                    avatarObject = root,
                    allBuildersInRun = new List<FeatureBuilder>(),
                    allFeaturesInRun = new List<FeatureModel>()
                };
                Inject(controllers, "avatar", avatar);
                Inject(controllers, "globals", globals);
                var parameters = new ParamsService();
                Inject(parameters, "avatar", avatar);
                Inject(controllers, "paramsService", parameters);
                Inject(controllers, "parameterSourceService", new ParameterSourceService());
                Inject(faceTracking, "avatar", avatar);
                Inject(faceTracking, "controllers", controllers);
            }

            private static void Inject(object target, string field, object value) => target.GetType().VFField(field).SetValue(target, value);

            public void NativeMesh(SkinnedMeshRenderer skin) {
                if (avatar.faceTrackingMeshes == null) avatar.faceTrackingMeshes = new List<SkinnedMeshRenderer>();
                FaceTrackingUtils.EnsureMesh(avatar.faceTrackingMeshes, skin);
            }

            public VRCAvatarDescriptor.FaceTrackingBlendshapesSet NativeSet(string name) {
                if (avatar.faceTrackingBlendshapesSetList == null)
                    avatar.faceTrackingBlendshapesSetList = new List<VRCAvatarDescriptor.FaceTrackingBlendshapesSet>();
                name = name.MakeUnique(avatar.faceTrackingBlendshapesSetList.Select(set => set.setName), FaceTrackingUtils.MaxSetNameLength);
                var set = FaceTrackingUtils.CreateBlendshapeSet(name);
                avatar.faceTrackingBlendshapesSetList.Add(set);
                return set;
            }

            public SkinnedMeshRenderer Skin(string name) {
                var obj = new GameObject(name);
                obj.transform.SetParent(root.transform);
                var skin = obj.AddComponent<SkinnedMeshRenderer>();
                var mesh = new Mesh { vertices = new[] { Vector3.zero } };
                mesh.AddBlendShapeFrame("Smile", 100, new[] { Vector3.up }, null, null);
                meshes.Add(mesh);
                skin.sharedMesh = mesh;
                return skin;
            }

            public FullControllerBuilder Full(GameObject owner, VRCAvatarDescriptor.FaceTrackingKey key = VRCAvatarDescriptor.FaceTrackingKey.JawOpen,
                bool visemes = false, string setName = "Face", float setWeight = 1,
                SkinnedMeshRenderer[] skins = null,
                string path = null,
                string blendshape = "Smile",
                params VRCAvatarDescriptor.FaceTrackingParameterBehavior[] behaviors) {
                var set = new VRCAvatarDescriptor.FaceTrackingBlendshapesSet { setName = setName, setWeight = setWeight };
                if (visemes) SetMapping(set, 1, blendshape, visemes: true);
                else SetMapping(set, (int)key, blendshape);
                var builder = new FullControllerBuilder {
                    featureBaseObject = owner,
                    model = new FullController {
                        faceTrackingMeshes = path != null
                            ? new List<ComponentOrPath<SkinnedMeshRenderer>> { new ComponentOrPath<SkinnedMeshRenderer> { usePath = true, path = path } }
                            : (skins ?? new[] { owner.GetComponent<SkinnedMeshRenderer>() })
                                .Select(skin => new ComponentOrPath<SkinnedMeshRenderer> { component = skin }).ToList(),
                        faceTrackingBlendshapeSets = { set },
                        faceTrackingParameterBehaviors = behaviors.ToList()
                    }
                };
                Add(builder, builder.model);
                Inject(builder, "animatorLayerControlManager", new AnimatorLayerControlOffsetService());
                globals.currentFeatureNum = globals.allBuildersInRun.Count;
                builder.Apply();
                return builder;
            }

            public void Link(SkinnedMeshRenderer source, SkinnedMeshRenderer target, string sourceName = "Smile", string targetName = "Smile") {
                var builder = new BlendShapeLinkBuilder {
                    model = new BlendShapeLink {
                        baseObj = source.name,
                        includeAll = false,
                        includes = { new BlendShapeLink.Include { nameOnBase = sourceName, nameOnLinked = targetName } },
                        linkSkins = { new BlendShapeLink.LinkSkin { renderer = target } }
                    }
                };
                Add(builder, builder.model);
                Inject(builder, "avatarObject", (VFGameObject)root);
            }

            public VF.Utils.Controller.VFBehaviour ControlSet(string name) {
                var layerSource = new LayerSourceService();
                Inject(layerSource, "globals", globals);
                var controller = new ControllerManager(VF.Utils.Controller.VFController.Create(), null,
                    VRCAvatarDescriptor.AnimLayerType.FX, () => 0, () => "", value => value, layerSource);
                var cache = (Dictionary<VRCAvatarDescriptor.AnimLayerType, ControllerManager>)typeof(ControllersService)
                    .VFField("_controllers").GetValue(controllers);
                cache[VRCAvatarDescriptor.AnimLayerType.FX] = controller;
                return controller.NewLayer("Control").NewState("Control").behaviours.AddBehaviour<VRCAnimatorFaceTrackingBlendshapesSet>(behavior => {
                    behavior.blendSets = new List<FtBehaviourControl> {
                        new FtBehaviourControl {
                            blendshapesSetName = name, sourceParameter = "VF_Weight", weight = 0.4f, lerpSpeed = 0.25f,
                            type = VRC.SDKBase.VRC_AnimatorFaceTrackingBlendshapesSet.SourceType.Parameter
                        }
                    };
                });
            }

            public void Animate(VFClip clip) {
                ControlSet("Unused");
                controllers.GetFx().NewLayer("Animation").NewState("Animation").WithAnimation(clip);
            }

            public bool MigrateVisemes(Visemes model) {
                var builder = new VisemesBuilder { model = model, featureBaseObject = root };
                Add(builder, model);
                Inject(builder, "avatarObject", (VFGameObject)root);
                globals.currentFeatureNum = globals.allBuildersInRun.Count;
                return (bool)typeof(VisemesBuilder).VFMethod("MigrateBlendshapeVisemesToFaceTracking").Invoke(builder, null);
            }

            private void Add(FeatureBuilder builder, FeatureModel model) {
                globals.allBuildersInRun.Add(builder);
                globals.allFeaturesInRun.Add(model);
                Inject(builder, "globals", globals);
                Inject(builder, "avatar", avatar);
                Inject(builder, "controllers", controllers);
                Inject(builder, "faceTracking", faceTracking);
            }

            public void Run() {
                var actions = globals.allBuildersInRun.SelectMany(builder => builder.GetType().GetMethods()
                    .Where(method => method.GetCustomAttribute<FeatureBuilderActionAttribute>() != null)
                    .Where(method => builder is BlendShapeLinkBuilder)
                    .Select(method => (builder, method, attribute: method.GetCustomAttribute<FeatureBuilderActionAttribute>())));
                foreach (var action in actions.OrderBy(action => action.attribute.GetPriority()).ThenBy(action => action.attribute.GetPriorityOffset()))
                    action.method.Invoke(action.builder, null);
                faceTracking.Apply();
            }

            public void Dispose() {
                meshes.AddRange(root.GetComponentsInChildren<SkinnedMeshRenderer>(true).Select(skin => skin.sharedMesh));
                Object.DestroyImmediate(root);
                foreach (var mesh in meshes.Distinct()) Object.DestroyImmediate(mesh);
            }
        }

        private sealed class AuthoringFields : ScriptableObject {
            public List<ComponentOrPath<SkinnedMeshRenderer>> meshes = new List<ComponentOrPath<SkinnedMeshRenderer>>();
            public VRCAvatarDescriptor.FaceTrackingBlendshapesSet set = new VRCAvatarDescriptor.FaceTrackingBlendshapesSet();
            public VRCAvatarDescriptor.FaceTrackingParameterBehavior behavior = new VRCAvatarDescriptor.FaceTrackingParameterBehavior();
        }

        private static Visemes NewVisemes() {
            var model = new Visemes();
            foreach (var field in typeof(Visemes).GetFields().Where(field => field.FieldType == typeof(State)))
                field.SetValue(model, new State());
            return model;
        }

        [Test]
        public void FullBuildKeepsFaceTrackingAndToggleCurvesThroughLinkingAndOptimization() {
            using (var fixture = new Build()) {
                var root = fixture.root;
                root.name = "FtMergeRegression_" + System.Guid.NewGuid().ToString("N");
                var avatar = fixture.avatar;
                avatar.customizeAnimationLayers = true;
                avatar.baseAnimationLayers = new[] {
                    VRCAvatarDescriptor.AnimLayerType.Base, VRCAvatarDescriptor.AnimLayerType.Additive,
                    VRCAvatarDescriptor.AnimLayerType.Gesture, VRCAvatarDescriptor.AnimLayerType.Action, VRCAvatarDescriptor.AnimLayerType.FX
                }.Select(type => new VRCAvatarDescriptor.CustomAnimLayer { type = type, isDefault = false }).ToArray();
                var body = fixture.Skin("Base");
                var accessory = fixture.Skin("Accessory");
                foreach (var skin in new[] { body, accessory })
                    skin.sharedMesh.AddBlendShapeFrame("Unused", 100, new[] { Vector3.right }, null, null);
                var marker = new GameObject("Marker");
                marker.transform.SetParent(root.transform);
                var set = FaceTrackingUtils.CreateBlendshapeSet("Face");
                SetMapping(set, (int)VRCAvatarDescriptor.FaceTrackingKey.JawOpen, "Smile");
                var sourceController = new UnityEditor.Animations.AnimatorController();
                sourceController.AddLayer("FT");
                var sourceBehavior = sourceController.layers[0].stateMachine.AddState("FT")
                    .AddStateMachineBehaviour<VRCAnimatorFaceTrackingBlendshapesSet>();
                sourceBehavior.blendSets = new List<FtBehaviourControl> { new FtBehaviourControl { blendshapesSetName = "Face", weight = 0.6f } };
                body.gameObject.AddComponent<VRCFury>().content = new FullController {
                    controllers = { new FullController.ControllerEntry { controller = sourceController } },
                    faceTrackingMeshes = { new ComponentOrPath<SkinnedMeshRenderer> { component = body } },
                    faceTrackingBlendshapeSets = { set }
                };
                root.AddComponent<VRCFury>().content = new BlendShapeLink {
                    baseObj = "Base", linkSkins = { new BlendShapeLink.LinkSkin { renderer = accessory } }
                };
                var toggle = new Toggle { name = "Smile" };
                toggle.state.actions.Add(new BlendShapeAction { renderer = body, allRenderers = false, blendShape = "Smile", blendShapeValue = 75 });
                root.AddComponent<VRCFury>().content = toggle;
                var visemes = NewVisemes();
                visemes.state_aa.actions.Add(new BlendShapeAction { renderer = body, allRenderers = false, blendShape = "Smile", blendShapeValue = 65 });
                visemes.state_aa.actions.Add(new ObjectToggleAction { obj = marker });
                root.AddComponent<VRCFury>().content = visemes;
                root.AddComponent<VRCFury>().content = new BlendshapeOptimizer();
                // Exercise the real scheduler without showing the editor's progress popup.
                var progress = ScriptableObject.CreateInstance<VRCFProgressWindow>();
                string outputFolder = null;
                try {
                    typeof(VRCFuryBuilder).GetMethod("ApplyFuryConfigs", BindingFlags.Static | BindingFlags.NonPublic)
                        .Invoke(null, new object[] { (VFGameObject)root, progress });
                    var fx = (UnityEditor.Animations.AnimatorController)VRCAvatarUtils.GetAvatarController(avatar, VRCAvatarDescriptor.AnimLayerType.FX).Item2;
                    outputFolder = VRCFuryAssetDatabase.GetDirectoryName(AssetDatabase.GetAssetPath(fx));
                    var ft = avatar.faceTrackingBlendshapesSetList.Single(entry => entry.faceTrackingBlendshapeNames[(int)VRCAvatarDescriptor.FaceTrackingKey.JawOpen] != "");
                    var name = ft.faceTrackingBlendshapeNames[(int)VRCAvatarDescriptor.FaceTrackingKey.JawOpen];
                    Assert.That(name, Is.Not.EqualTo("Smile"));
                    foreach (var skin in new[] { body, accessory }) {
                        Assert.That(skin.HasBlendshape(name), Is.True);
                        Assert.That(skin.HasBlendshape("Unused"), Is.False, "The optimizer must actually have run");
                        var path = AnimationUtility.CalculateTransformPath(skin.transform, root.transform);
                        Assert.That(fx.animationClips.SelectMany(AnimationUtility.GetCurveBindings)
                            .Any(binding => binding.path == path && binding.propertyName == "blendShape." + name), Is.True);
                    }
                    var migrated = avatar.faceTrackingBlendshapesSetList.Single(entry => entry.visemeBlendshapeNames[10] != "");
                    Assert.That(migrated.visemeBlendshapeNames[0], Is.Empty);
                    Assert.That(fx.animationClips.SelectMany(AnimationUtility.GetCurveBindings)
                        .Any(binding => binding.path == "Marker" && binding.propertyName == "m_IsActive"), Is.True,
                        "The non-blendshape action must still be present in the animator");
                    Assert.That(fx.layers.SelectMany(layer => layer.stateMachine.states).SelectMany(state => state.state.behaviours)
                        .OfType<VRCAnimatorFaceTrackingBlendshapesSet>().SelectMany(behavior => behavior.blendSets)
                        .Any(control => control.blendshapesSetName == ft.setName), Is.True);
                    Assert.That(sourceBehavior.blendSets.Single().blendshapesSetName, Is.EqualTo("Face"));
                } finally {
                    Object.DestroyImmediate(progress);
                    Object.DestroyImmediate(sourceController);
                    // The scheduler saves generated assets; leave their destruction to AssetDatabase, not Build.Dispose.
                    foreach (var skin in new[] { body, accessory }) if (skin != null) skin.sharedMesh = null;
                    if (outputFolder != null && outputFolder.StartsWith("Packages/com.vrcfury.temp/Builds/FtMergeRegression_", System.StringComparison.Ordinal))
                        AssetDatabase.DeleteAsset(outputFolder);
                }
            }
        }

        [TestCase("cycle", false)]
        [TestCase("cycle", true)]
        [TestCase("diamond", false)]
        [TestCase("diamond", true)]
        [TestCase("converging", false)]
        [TestCase("converging", true)]
        public void LinkGraphsPropagateEachDriverExactlyOnce(string topology, bool reverse) {
            using (var build = new Build()) {
                var skins = Enumerable.Range(0, 4).Select(i => build.Skin("Mesh" + i)).ToArray();
                build.Full(skins[0].gameObject);
                if (topology == "converging") build.Full(skins[1].gameObject);
                var links = topology == "cycle" ? new[] { (0, 1), (1, 2), (2, 0) }
                    : topology == "diamond" ? new[] { (0, 1), (0, 2), (1, 3), (2, 3) }
                    : new[] { (0, 2), (1, 2), (2, 3) };
                foreach (var (source, target) in reverse ? links.Reverse() : links)
                    build.Link(skins[source], skins[target]);
                build.Run();
                var sets = build.avatar.faceTrackingBlendshapesSetList;
                Assert.That(sets, Has.Count.EqualTo(topology == "converging" ? 4 : 1));
                for (var driver = 0; driver < (topology == "converging" ? 2 : 1); driver++) {
                    var names = sets.Where(set => set.setName.StartsWith("VF" + (driver + 1) + "_Face"))
                        .Select(set => set.faceTrackingBlendshapeNames[(int)VRCAvatarDescriptor.FaceTrackingKey.JawOpen]).ToArray();
                    for (var mesh = 0; mesh < skins.Length; mesh++) {
                        var expected = topology == "cycle" ? mesh != 3 : topology == "diamond" || mesh == driver || mesh >= 2;
                        Assert.That(names.Count(skins[mesh].HasBlendshape), Is.EqualTo(expected ? 1 : 0),
                            $"Driver {driver} on mesh {mesh} must neither disappear nor be duplicated");
                    }
                }
            }
        }

        [TestCase(VRCAvatarDescriptor.ParameterBehaviorTargetMode.ApplyToBlendshape)]
        [TestCase(VRCAvatarDescriptor.ParameterBehaviorTargetMode.LimitBlendshapeMin)]
        [TestCase(VRCAvatarDescriptor.ParameterBehaviorTargetMode.LimitBlendshapeMax)]
        public void ShapeBehaviorsExpandWithTheirTargetsAndKeepTheirWeightGate(VRCAvatarDescriptor.ParameterBehaviorTargetMode mode) {
            using (var build = new Build()) {
                var source = build.Skin("Source");
                var target = build.Skin("Target");
                var unrelated = build.Skin("Unrelated");
                var behavior = new VRCAvatarDescriptor.FaceTrackingParameterBehavior {
                    targetMode = mode, targetBlendshapeName = "Smile", weightBlendSetName = "Face",
                    combineMode = VRCAvatarDescriptor.ParameterBehaviorCombineMode.Custom,
                    customExpression = "Voice * 0.7", inputTrackingKeyNames = new[] { "Voice" }, inputInverted = new[] { true }
                };
                build.Full(source.gameObject, setWeight: 0.35f, behaviors: new[] { behavior });
                build.Full(target.gameObject, setName: "Independent");
                var control = build.ControlSet("VF1_Face");
                build.Link(source, target);
                build.Run();
                var copies = build.avatar.faceTrackingParameterBehaviors;
                Assert.That(copies, Has.Count.EqualTo(2));
                foreach (var skin in new[] { source, target })
                    Assert.That(copies.Count(copy => skin.HasBlendshape(copy.targetBlendshapeName)), Is.EqualTo(1));
                foreach (var copy in copies) {
                    Assert.That(copy.targetMode, Is.EqualTo(mode));
                    Assert.That(copy.combineMode, Is.EqualTo(behavior.combineMode));
                    Assert.That(copy.customExpression, Is.EqualTo(behavior.customExpression));
                    Assert.That(copy.inputTrackingKeyNames, Is.EqualTo(behavior.inputTrackingKeyNames));
                    Assert.That(copy.inputInverted, Is.EqualTo(behavior.inputInverted));
                    Assert.That(copy.weightBlendSetName, Is.EqualTo("VF1_Face"));
                    Assert.That(unrelated.HasBlendshape(copy.targetBlendshapeName), Is.False);
                }
                var expanded = build.avatar.faceTrackingBlendshapesSetList.Where(set => set.setName.StartsWith("VF1_Face")).ToArray();
                Assert.That(expanded, Has.Length.EqualTo(2));
                Assert.That(expanded.All(set => set.setWeight == 0.35f), Is.True);
                Assert.That(control.Read<VRCAnimatorFaceTrackingBlendshapesSet>().blendSets.Select(entry => entry.blendshapesSetName),
                    Is.EquivalentTo(expanded.Select(set => set.setName)));
            }
        }

        [TestCase(false)]
        [TestCase(true)]
        public void AnimationCurvesFollowRenamesWithoutChangingOtherBindings(bool independentlyDrivenTarget) {
            using (var build = new Build()) {
                var source = build.Skin("Source");
                var target = build.Skin("Target");
                var unrelated = build.Skin("Unrelated");
                build.Full(source.gameObject);
                if (independentlyDrivenTarget) build.Full(target.gameObject);
                build.Link(source, target);
                var clip = VFClip.Create("Toggle");
                var curve = new AnimationCurve(new Keyframe(0, 12, 3, 4), new Keyframe(1, 87, 5, 6));
                var otherCurve = AnimationCurve.Linear(0, 23, 1, 45);
                clip.SetCurve(source, "blendShape.Smile", curve);
                clip.SetCurve(unrelated, "blendShape.Smile", otherCurve);
                clip.SetCurve(source, "m_Enabled", otherCurve);
                build.Animate(clip);
                build.Run();
                foreach (var skin in new[] { source, target }) {
                    var name = skin.sharedMesh.GetBlendShapeName(0);
                    Assert.That(name, Is.Not.EqualTo("Smile"));
                    var bindings = clip.GetFloatBindings().Where(binding => binding.Targets(skin.owner())).ToArray();
                    Assert.That(bindings.Select(binding => binding.propertyName), Has.No.Member("blendShape.Smile"));
                    var binding = bindings.Single(entry => entry.propertyName == "blendShape." + name);
                    Assert.That(clip.GetFloatCurve(binding).keys, Is.EqualTo(curve.keys));
                }
                var untouched = clip.GetFloatBindings().Single(binding => binding.Targets(unrelated.owner()));
                Assert.That(untouched.propertyName, Is.EqualTo("blendShape.Smile"));
                Assert.That(clip.GetFloatCurve(untouched).keys, Is.EqualTo(otherCurve.keys));
                Assert.That(clip.GetFloatCurve(clip.GetFloatBindings().Single(binding => binding.propertyName == "m_Enabled")).keys,
                    Is.EqualTo(otherCurve.keys));
            }
        }

        [Test]
        public void UndrivenNameCollisionsOnBothFaceTrackingMeshesRemainIsolated() {
            using (var build = new Build()) {
                var native = build.Skin("Native");
                var imported = build.Skin("Imported");
                foreach (var skin in new[] { native, imported })
                    skin.sharedMesh.AddBlendShapeFrame("Other", 100, new[] { Vector3.right }, null, null);
                build.avatar.enableFaceTracking = true;
                build.avatar.useAllTrackingKeys = true;
                build.NativeMesh(native);
                SetMapping(build.NativeSet("Native"), (int)VRCAvatarDescriptor.FaceTrackingKey.JawOpen, "Smile");
                build.Full(imported.gameObject, blendshape: "Other");
                build.Run();
                var sets = build.avatar.faceTrackingBlendshapesSetList;
                var nativeName = sets.Single(set => set.setName == "Native").faceTrackingBlendshapeNames[(int)VRCAvatarDescriptor.FaceTrackingKey.JawOpen];
                var importedName = sets.Single(set => set.setName == "VF1_Face").faceTrackingBlendshapeNames[(int)VRCAvatarDescriptor.FaceTrackingKey.JawOpen];
                Assert.That(nativeName, Is.Not.EqualTo("Smile"));
                Assert.That(native.HasBlendshape(nativeName), Is.True);
                Assert.That(imported.HasBlendshape(nativeName), Is.False);
                Assert.That(imported.HasBlendshape(importedName), Is.True);
                Assert.That(native.HasBlendshape(importedName), Is.False);
                Assert.That(imported.HasBlendshape("Smile"), Is.True);
                Assert.That(native.HasBlendshape("Other"), Is.True);
            }
        }

        [Test]
        public void AdvancedAaVisemeMigratesAaWithoutPopulatingSilence() {
            using (var build = new Build()) {
                var skin = build.Skin("Mouth");
                var model = NewVisemes();
                model.state_aa.actions.Add(new BlendShapeAction { renderer = skin, allRenderers = false, blendShape = "Smile", blendShapeValue = 65 });
                Assert.That(build.MigrateVisemes(model), Is.False);
                build.Run();
                var set = build.avatar.faceTrackingBlendshapesSetList.Single();
                Assert.That(set.visemeBlendshapeNames[0], Is.Empty);
                Assert.That(set.visemeBlendshapeNames[10], Is.Not.Empty);
                Assert.That(skin.HasBlendshape(set.visemeBlendshapeNames[10]), Is.True);
                Assert.That(set.visemeBlendshapeMultipliers[10], Is.EqualTo(0.65f));
            }
        }

        [TestCase(false)]
        [TestCase(true)]
        public void LegacyVisemeMigrationPreservesExplicitSilence(bool onlySilence) {
            using (var build = new Build()) {
                var skin = build.Skin("Mouth");
                build.avatar.lipSync = VRC.SDKBase.VRC_AvatarDescriptor.LipSyncStyle.VisemeBlendShape;
                build.avatar.VisemeSkinnedMesh = skin;
                var names = Enumerable.Repeat("", FaceTrackingUtils.VisemeCount).ToArray();
                names[0] = "Smile";
                if (!onlySilence) names[10] = "Smile";
                build.avatar.VisemeBlendShapes = names;
                build.faceTracking.EnsureVisemesEnabled();
                build.Run();
                var sets = build.avatar.faceTrackingBlendshapesSetList;
                Assert.That(sets, Has.Count.EqualTo(1));
                Assert.That(skin.HasBlendshape(sets.Single().visemeBlendshapeNames[0]), Is.True);
                Assert.That(skin.HasBlendshape(sets.Single().visemeBlendshapeNames[10]), Is.EqualTo(!onlySilence));
                Assert.That(build.avatar.faceTrackingMeshes, Does.Contain(skin));
                Assert.That(names[0], Is.EqualTo("Smile"), "Do not mutate legacy input arrays");
            }
        }

        [Test]
        public void AdvancedVisemesKeepNonBlendshapeAndUnresolvedActions() {
            using (var build = new Build()) {
                var skin = build.Skin("Mouth");
                var model = NewVisemes();
                var toggle = new ObjectToggleAction { obj = skin.gameObject };
                var unresolved = new BlendShapeAction { renderer = skin, allRenderers = false, blendShape = "Missing" };
                model.state_PP.actions.Add(new BlendShapeAction { renderer = skin, allRenderers = false, blendShape = "Smile" });
                model.state_PP.actions.Add(toggle);
                model.state_FF.actions.Add(unresolved);
                Assert.That(build.MigrateVisemes(model), Is.True);
                Assert.That(model.state_PP.actions, Is.EqualTo(new[] { toggle }));
                Assert.That(model.state_FF.actions, Is.EqualTo(new[] { unresolved }));
                build.Run();
                Assert.That(build.avatar.faceTrackingBlendshapesSetList.Single().visemeBlendshapeNames[1], Is.Not.Empty);
            }
        }

        [TestCase(false)]
        [TestCase(true)]
        public void AdvancedVisemeSetsPreserveIndependentActionsAndBuilders(bool separateBuilders) {
            using (var build = new Build()) {
                var a = build.Skin("A");
                var b = build.Skin("B");
                var first = NewVisemes();
                var second = separateBuilders ? NewVisemes() : first;
                first.state_PP.actions.Add(new BlendShapeAction { renderer = a, allRenderers = false, blendShape = "Smile", blendShapeValue = 20 });
                second.state_PP.actions.Add(new BlendShapeAction { renderer = b, allRenderers = false, blendShape = "Smile", blendShapeValue = 80 });
                Assert.That(build.MigrateVisemes(first), Is.False);
                if (separateBuilders) Assert.That(build.MigrateVisemes(second), Is.False);
                build.Run();
                var sets = build.avatar.faceTrackingBlendshapesSetList;
                Assert.That(sets, Has.Count.EqualTo(2));
                Assert.That(sets.Select(set => set.setName).Distinct().Count(), Is.EqualTo(2));
                foreach (var pair in new[] { (a, 0.2f), (b, 0.8f) }) {
                    var set = sets.Single(entry => pair.Item1.HasBlendshape(entry.visemeBlendshapeNames[1]));
                    Assert.That(set.visemeBlendshapeMultipliers[1], Is.EqualTo(pair.Item2));
                }
            }
        }

        [TestCase(false)]
        [TestCase(true)]
        public void AdvancedVisemesSkipLinkedTargetsButKeepOtherAllRendererTargets(bool allRenderers) {
            using (var build = new Build()) {
                var source = build.Skin("Source");
                var target = build.Skin("Target");
                var other = build.Skin("Other");
                build.Full(source.gameObject);
                build.Link(source, target);
                var model = NewVisemes();
                model.state_PP.actions.Add(new BlendShapeAction { renderer = target, allRenderers = allRenderers, blendShape = "Smile" });
                Assert.That(build.MigrateVisemes(model), Is.False);
                Assert.That(model.state_PP.actions, Is.Empty);
                build.Run();
                var visemes = build.avatar.faceTrackingBlendshapesSetList.Where(set => set.setName.StartsWith("_vrcf_advanced_visemes")).ToArray();
                Assert.That(visemes, Has.Length.EqualTo(allRenderers ? 2 : 0));
                if (allRenderers) {
                    Assert.That(visemes.Count(set => target.HasBlendshape(set.visemeBlendshapeNames[1])), Is.EqualTo(1));
                    Assert.That(visemes.Count(set => other.HasBlendshape(set.visemeBlendshapeNames[1])), Is.EqualTo(1));
                }
            }
        }

        [TestCase(false, 0)]
        [TestCase(false, 1)]
        [TestCase(false, 2)]
        [TestCase(true, 0)]
        [TestCase(true, 1)]
        [TestCase(true, 2)]
        public void ImportAndOutputBoundariesHandleMalformedArrays(bool visemes, int state) {
            using (var build = new Build()) {
                var skin = build.Skin("Native");
                build.avatar.enableFaceTracking = true;
                build.avatar.useAllTrackingKeys = true;
                build.NativeMesh(skin);
                var input = build.NativeSet("Native");
                var count = visemes ? (int)VRC.SDKBase.VRC_AvatarDescriptor.Viseme.Count : (int)VRCAvatarDescriptor.FaceTrackingKey.Max;
                var names = state == 0 ? null : state == 1 ? new[] { "Smile", null }
                    : Enumerable.Repeat("", count).Concat(new[] { "Ignored" }).ToArray();
                if (names != null) names[0] = "Smile";
                var multipliers = state == 1 ? null : Enumerable.Repeat(0.25f, state == 0 ? 1 : count + 1).ToArray();
                if (visemes) {
                    input.visemeBlendshapeNames = names;
                    input.visemeBlendshapeMultipliers = multipliers;
                } else {
                    input.faceTrackingBlendshapeNames = names;
                    input.faceTrackingBlendshapeMultipliers = multipliers;
                }
                var original = JsonUtility.ToJson(input);
                build.faceTracking.CaptureInitialState();
                build.Run();
                var output = build.avatar.faceTrackingBlendshapesSetList.Single();
                var outputNames = visemes ? output.visemeBlendshapeNames : output.faceTrackingBlendshapeNames;
                var outputMultipliers = visemes ? output.visemeBlendshapeMultipliers : output.faceTrackingBlendshapeMultipliers;
                Assert.That(outputNames, Has.Length.EqualTo(count));
                Assert.That(outputNames[0], Is.EqualTo(state == 0 ? "" : "Smile"));
                Assert.That(outputNames.Skip(1), Is.All.Empty);
                Assert.That(outputMultipliers, Is.EqualTo(Enumerable.Range(0, count)
                    .Select(i => multipliers != null && i < multipliers.Length ? multipliers[i] : 1)));
                Assert.That(JsonUtility.ToJson(input), Is.EqualTo(original));
            }
        }

        [TestCase(false)]
        [TestCase(true)]
        public void RemovingAnInactiveInputFamilyResetsItsOutputMultipliers(bool visemes) {
            using (var build = new Build()) {
                build.avatar.enableFaceTracking = true;
                build.NativeMesh(build.Skin("Native"));
                var input = build.NativeSet("Native");
                var index = visemes ? 1 : (int)VRCAvatarDescriptor.FaceTrackingKey.JawOpen;
                SetMapping(input, index, "Smile", visemes, 0.25f);
                if (visemes) build.faceTracking.EnsureVisemesEnabled();
                else build.Full(build.Skin("Imported").gameObject);
                build.Run();
                var output = build.avatar.faceTrackingBlendshapesSetList.Single(set => set.setName == "Native");
                Assert.That((visemes ? output.visemeBlendshapeNames : output.faceTrackingBlendshapeNames)[index], Is.Empty);
                Assert.That((visemes ? output.visemeBlendshapeMultipliers : output.faceTrackingBlendshapeMultipliers)[index], Is.EqualTo(1));
                Assert.That((visemes ? input.visemeBlendshapeMultipliers : input.faceTrackingBlendshapeMultipliers)[index], Is.EqualTo(0.25f));
            }
        }

        [TestCase(false)]
        [TestCase(true)]
        public void UpdatingASlotDoesNotDuplicateItsMapping(bool clear) {
            using (var build = new Build()) {
                var skin = build.Skin("Source");
                build.faceTracking.EnsureVisemesEnabled();
                var pending = build.faceTracking.AddSet("Visemes");
                build.faceTracking.SetVisemeMapping(pending, 1, skin, "Smile", 0.25f);
                build.faceTracking.SetVisemeMapping(pending, 1, skin, clear ? "" : "Smile", 0.75f);
                Assert.That(pending.mappings, Has.Count.EqualTo(1));
                build.Run();
                var output = build.avatar.faceTrackingBlendshapesSetList.Single();
                Assert.That(output.visemeBlendshapeNames[1], clear ? Is.Empty : Is.Not.Empty);
                Assert.That(output.visemeBlendshapeMultipliers[1], Is.EqualTo(0.75f));
            }
        }

        [Test]
        public void FaceTrackingRenamesPreserveLegacyEyelidIndicesAndGeometry() {
            using (var build = new Build()) {
                var skin = build.Skin("LegacyEyelids");
                var original = skin.sharedMesh;
                var names = new[] { "Smile", "Up", "Down" };
                var deltas = new[] { Vector3.up, Vector3.right, Vector3.down };
                var weights = new[] { 17f, 42f, 73f };
                var imported = new VRCAvatarDescriptor.FaceTrackingBlendshapesSet { setName = "Imported", setWeight = 1 };
                for (var i = 0; i < names.Length; i++) {
                    if (i != 0) original.AddBlendShapeFrame(names[i], 100, new[] { deltas[i] }, null, null);
                    original.AddBlendShapeFrame(names[i], 200, new[] { deltas[i] * 2 }, new[] { deltas[i] }, new[] { Vector3.forward });
                    skin.SetBlendShapeWeight(i, weights[i]);
                    SetMapping(imported, i + 1, names[i], visemes: true);
                }
                build.avatar.enableEyeLook = true;
                build.avatar.customEyeLookSettings.eyelidType = VRCAvatarDescriptor.EyelidType.Blendshapes;
                build.avatar.customEyeLookSettings.eyeGazeMode = VRCAvatarDescriptor.CustomEyeLookSettings.EyeGazeMode.BoneRotation;
                build.avatar.customEyeLookSettings.eyelidsSkinnedMesh = skin;
                build.avatar.customEyeLookSettings.eyelidsBlendshapes = new[] { 2, 0, 1 };

                // Import unrelated FT visemes on the eyelid mesh without requesting an eyelid upgrade.
                build.faceTracking.RegisterImportedSet(imported, new[] { skin });
                build.Run();

                Assert.That(build.avatar.enableEyeLook, Is.True);
                var settings = build.avatar.customEyeLookSettings;
                Assert.That(settings.eyelidType, Is.EqualTo(VRCAvatarDescriptor.EyelidType.Blendshapes));
                Assert.That(settings.eyeGazeMode, Is.EqualTo(VRCAvatarDescriptor.CustomEyeLookSettings.EyeGazeMode.BoneRotation));
                Assert.That(settings.eyelidsSkinnedMesh, Is.SameAs(skin));
                Assert.That(settings.eyelidsBlendshapes, Is.EqualTo(new[] { 2, 0, 1 }));
                var renamed = skin.sharedMesh;
                Assert.That(renamed, Is.Not.SameAs(original));
                Assert.That(renamed.blendShapeCount, Is.EqualTo(original.blendShapeCount));
                foreach (var index in settings.eyelidsBlendshapes) {
                    Assert.That(renamed.GetBlendShapeName(index), Is.Not.EqualTo(names[index]), "The eyelid shape must actually have been renamed");
                    Assert.That(skin.GetBlendShapeWeight(index), Is.EqualTo(weights[index]));
                    Assert.That(renamed.GetBlendShapeFrameCount(index), Is.EqualTo(original.GetBlendShapeFrameCount(index)));
                    for (var frame = 0; frame < original.GetBlendShapeFrameCount(index); frame++) {
                        Assert.That(renamed.GetBlendShapeFrameWeight(index, frame), Is.EqualTo(original.GetBlendShapeFrameWeight(index, frame)));
                        var expectedVertices = new Vector3[original.vertexCount];
                        var expectedNormals = new Vector3[original.vertexCount];
                        var expectedTangents = new Vector3[original.vertexCount];
                        original.GetBlendShapeFrameVertices(index, frame, expectedVertices, expectedNormals, expectedTangents);
                        var vertices = new Vector3[renamed.vertexCount];
                        var normals = new Vector3[renamed.vertexCount];
                        var tangents = new Vector3[renamed.vertexCount];
                        renamed.GetBlendShapeFrameVertices(index, frame, vertices, normals, tangents);
                        Assert.That(vertices, Is.EqualTo(expectedVertices));
                        Assert.That(normals, Is.EqualTo(expectedNormals));
                        Assert.That(tangents, Is.EqualTo(expectedTangents));
                    }
                }
            }
        }

        [TestCase(false, false)]
        [TestCase(false, true)]
        [TestCase(true, false)]
        [TestCase(true, true)]
        public void VisemeEnablementDoesNotActivateStaleNativeBehaviors(bool alreadyEnabled, bool custom) {
            using (var build = new Build()) {
                build.avatar.enableFaceTracking = true;
                build.avatar.lipSync = alreadyEnabled
                    ? VRC.SDKBase.VRC_AvatarDescriptor.LipSyncStyle.VisemeBlendShapeFaceTracking
                    : VRC.SDKBase.VRC_AvatarDescriptor.LipSyncStyle.VisemeParameterOnly;
                build.avatar.faceTrackingParameterBehaviors = new List<VRCAvatarDescriptor.FaceTrackingParameterBehavior> {
                    new VRCAvatarDescriptor.FaceTrackingParameterBehavior {
                        targetMode = VRCAvatarDescriptor.ParameterBehaviorTargetMode.ForwardToAnimator,
                        outputParameterName = "StaleViseme", inputTrackingKeyNames = new[] { "Viseme_aa", "Voice" },
                        combineMode = custom ? VRCAvatarDescriptor.ParameterBehaviorCombineMode.Custom : VRCAvatarDescriptor.ParameterBehaviorCombineMode.Max,
                        customExpression = "max(Viseme_aa, Voice)"
                    },
                    new VRCAvatarDescriptor.FaceTrackingParameterBehavior {
                        targetMode = VRCAvatarDescriptor.ParameterBehaviorTargetMode.ForwardToAnimator,
                        outputParameterName = "VoiceOnly", inputTrackingKeyNames = new[] { "Voice" }
                    }
                };
                build.Full(build.Skin("Imported").gameObject, visemes: true, behaviors: new[] {
                    new VRCAvatarDescriptor.FaceTrackingParameterBehavior {
                        targetMode = VRCAvatarDescriptor.ParameterBehaviorTargetMode.ForwardToAnimator,
                        outputParameterName = "ImportedViseme", inputTrackingKeyNames = new[] { "Viseme_aa" }
                    }
                });
                build.Run();
                var outputs = build.avatar.faceTrackingParameterBehaviors.Select(behavior => behavior.outputParameterName).ToArray();
                Assert.That(outputs.Contains("StaleViseme"), Is.EqualTo(alreadyEnabled));
                Assert.That(outputs, Does.Contain("VoiceOnly"));
                Assert.That(outputs, Does.Contain("VF1_ImportedViseme"));
            }
        }

        [Test]
        public void VisemeUpgradeClearsStaleBehaviorsWithoutImportedSets() {
            using (var build = new Build()) {
                build.avatar.enableFaceTracking = true;
                build.avatar.faceTrackingParameterBehaviors = new List<VRCAvatarDescriptor.FaceTrackingParameterBehavior> {
                    new VRCAvatarDescriptor.FaceTrackingParameterBehavior { inputTrackingKeyNames = new[] { "Viseme_aa" } },
                    new VRCAvatarDescriptor.FaceTrackingParameterBehavior { inputTrackingKeyNames = new[] { "Voice" } }
                };
                build.faceTracking.EnsureVisemesEnabled();
                build.Run();
                Assert.That(build.avatar.faceTrackingParameterBehaviors.Single().inputTrackingKeyNames, Is.EqualTo(new[] { "Voice" }));
            }
        }

        [TestCase(false)]
        [TestCase(true)]
        public void EmptyLegacyVisemesDoNotConsumeMeshOrSetCapacity(bool nullNames) {
            using (var build = new Build()) {
                build.avatar.enableFaceTracking = true;
                build.avatar.faceTrackingMeshes = Enumerable.Range(0, FaceTrackingUtils.MaxFaceTrackingMeshes)
                    .Select(i => build.Skin("Configured" + i)).ToList();
                build.avatar.lipSync = VRC.SDKBase.VRC_AvatarDescriptor.LipSyncStyle.VisemeBlendShape;
                build.avatar.VisemeSkinnedMesh = build.Skin("UnusedLegacy");
                build.avatar.VisemeBlendShapes = nullNames ? null : Enumerable.Repeat("", 15).ToArray();
                build.faceTracking.EnsureVisemesEnabled();
                build.Run();
                Assert.That(build.avatar.faceTrackingMeshes, Has.Count.EqualTo(FaceTrackingUtils.MaxFaceTrackingMeshes));
                Assert.That(build.avatar.faceTrackingMeshes, Has.No.Member(build.avatar.VisemeSkinnedMesh));
                Assert.That(build.avatar.faceTrackingBlendshapesSetList, Is.Empty);
            }
        }

        [TestCase("animator")]
        [TestCase("target")]
        [TestCase("weight")]
        public void ExpandedSetNamesDoNotCaptureDanglingReferences(string reference) {
            using (var build = new Build()) {
                var source = build.Skin("Source");
                var target = build.Skin("Target");
                target.sharedMesh.AddBlendShapeFrame("Second", 100, new[] { Vector3.up }, null, null);
                build.avatar.enableFaceTracking = true;
                build.avatar.useAllTrackingKeys = true;
                build.NativeMesh(source);
                var set = build.NativeSet("Face");
                SetMapping(set, (int)VRCAvatarDescriptor.FaceTrackingKey.JawOpen, "Smile");
                if (reference == "animator") build.ControlSet("Face_2");
                else build.avatar.faceTrackingParameterBehaviors = new List<VRCAvatarDescriptor.FaceTrackingParameterBehavior> {
                    new VRCAvatarDescriptor.FaceTrackingParameterBehavior {
                        targetMode = VRCAvatarDescriptor.ParameterBehaviorTargetMode.ApplyBlendSetWeight,
                        targetBlendSetName = reference == "target" ? "Face_2" : "Face",
                        weightBlendSetName = reference == "weight" ? "Face_2" : "",
                        inputTrackingKeyNames = new[] { "Voice" }
                    }
                };
                build.Link(source, target);
                build.Link(source, target, targetName: "Second");
                build.Run();
                Assert.That(build.avatar.faceTrackingBlendshapesSetList, Has.Count.EqualTo(2));
                Assert.That(build.avatar.faceTrackingBlendshapesSetList.Select(entry => entry.setName), Has.No.Member("Face_2"));
                if (reference != "animator") Assert.That(build.avatar.faceTrackingParameterBehaviors.All(behavior =>
                    reference == "target" ? behavior.targetBlendSetName == "Face_2" : behavior.weightBlendSetName == "Face_2"), Is.True);
            }
        }

        [Test]
        public void ImportedAndGeneratedSetsAvoidDanglingAnimatorReferences() {
            using (var build = new Build()) {
                build.ControlSet("VF1_Face");
                build.Full(build.Skin("Imported").gameObject);
                build.ControlSet("_vrcf_advanced_visemes");
                build.faceTracking.AddSet("_vrcf_advanced_visemes");
                build.Run();
                var names = build.avatar.faceTrackingBlendshapesSetList.Select(set => set.setName).ToArray();
                Assert.That(names, Has.No.Member("VF1_Face"));
                Assert.That(names, Has.No.Member("_vrcf_advanced_visemes"));
            }
        }

        [TestCase("set")]
        [TestCase("behavior")]
        public void FullControllerEditorOverridesSavedAuthoringAvatar(string field) {
            using (var build = new Build()) {
                var skin = build.Skin("Source");
                var owner = ScriptableObject.CreateInstance<AuthoringFields>();
                owner.meshes.Add(new ComponentOrPath<SkinnedMeshRenderer> { component = skin });
                try {
                    using (var serialized = new SerializedObject(owner)) {
                        var property = serialized.FindProperty(field);
                        var context = VRC.SDK3A.Editor.FaceTrackingEditorContext.ForProperty(property);
                        context.SourceAvatar = build.avatar;
                        PropertyDrawer drawer = field == "set"
                            ? (PropertyDrawer)new VRC.SDK3A.Editor.FaceTrackingBlendshapesSetDrawer()
                            : new VRC.SDK3A.Editor.FaceTrackingParameterBehaviorDrawer();
                        typeof(FullControllerBuilder).GetMethod("FaceTrackingEditor", BindingFlags.NonPublic | BindingFlags.Static)
                            .Invoke(null, new object[] { property, drawer, serialized.FindProperty("meshes") });
                        Assert.That(context.SourceAvatar, Is.Null);
                    }
                } finally {
                    Object.DestroyImmediate(owner);
                }
            }
        }

        [TestCase(false)]
        [TestCase(true)]
        public void RenamingSharedMutableMeshesDoesNotAffectOtherRenderers(bool importBoth) {
            using (var build = new Build()) {
                var first = build.Skin("First");
                var second = build.Skin("Second");
                second.sharedMesh = VrcfObjectFactory.Register(first.sharedMesh);
                build.Full(first.gameObject);
                if (importBoth) build.Full(second.gameObject);
                build.Run();
                Assert.That(first.sharedMesh, Is.Not.SameAs(second.sharedMesh));
                var names = build.avatar.faceTrackingBlendshapesSetList.Select(set =>
                    set.faceTrackingBlendshapeNames[(int)VRCAvatarDescriptor.FaceTrackingKey.JawOpen]).ToArray();
                Assert.That(first.HasBlendshape(names[0]), Is.True);
                if (importBoth) {
                    Assert.That(second.HasBlendshape(names[1]), Is.True);
                    Assert.That(second.HasBlendshape(names[0]), Is.False);
                } else Assert.That(second.HasBlendshape("Smile"), Is.True);
            }
        }

        [TestCase(false)]
        [TestCase(true)]
        public void FullControllerUsesOnlyExplicitFaceTrackingMeshes(bool empty) {
            using (var build = new Build()) {
                var owner = new GameObject("Controller");
                owner.transform.SetParent(build.root.transform);
                var unlisted = build.Skin("UnlistedChild");
                unlisted.transform.SetParent(owner.transform);
                var selected = build.Skin("ExplicitSibling");
                var behavior = new VRCAvatarDescriptor.FaceTrackingParameterBehavior {
                    targetMode = VRCAvatarDescriptor.ParameterBehaviorTargetMode.ApplyToBlendshape,
                    targetBlendshapeName = "Smile"
                };
                build.Full(owner, skins: empty ? new SkinnedMeshRenderer[0] : new[] { selected, selected },
                    behaviors: new[] { behavior });
                build.Run();
                Assert.That(build.avatar.faceTrackingMeshes,
                    Is.EquivalentTo(empty ? new SkinnedMeshRenderer[0] : new[] { selected }));
                Assert.That(unlisted.HasBlendshape("Smile"), Is.True);
                Assert.That(unlisted.sharedMesh.blendShapeCount, Is.EqualTo(1));
                if (empty) {
                    Assert.That(selected.HasBlendshape("Smile"), Is.True);
                    Assert.That(build.avatar.faceTrackingParameterBehaviors, Is.Empty);
                } else {
                    var name = build.avatar.faceTrackingBlendshapesSetList.Single()
                        .faceTrackingBlendshapeNames[(int)VRCAvatarDescriptor.FaceTrackingKey.JawOpen];
                    Assert.That(selected.HasBlendshape(name), Is.True);
                    Assert.That(unlisted.HasBlendshape(name), Is.False);
                    Assert.That(build.avatar.faceTrackingParameterBehaviors.Single().targetBlendshapeName, Is.EqualTo(name));
                }
            }
        }

        [Test]
        public void FullControllerResolvesFaceTrackingPathsFromAvatarNotPrefab() {
            using (var build = new Build()) {
                var prefab = build.Skin("Prefab");
                var body = build.Skin("Body");
                build.Full(prefab.gameObject, path: "Body");
                build.Run();
                Assert.That(build.avatar.faceTrackingMeshes, Is.EquivalentTo(new[] { body }));
                Assert.That(prefab.HasBlendshape("Smile"), Is.True);
            }
        }

        [TestCase("Missing")]
        [TestCase("")]
        public void FullControllerRejectsInvalidFaceTrackingMeshPaths(string path) {
            using (var build = new Build()) {
                Assert.Throws<VF.Exceptions.VRCFBuilderException>(() => build.Full(build.root, path: path));
            }
        }

        [Test]
        public void FullControllerRejectsMissingFaceTrackingMeshReferences() {
            using (var build = new Build()) {
                var error = Assert.Throws<VF.Exceptions.VRCFBuilderException>(() =>
                    build.Full(build.root, skins: new SkinnedMeshRenderer[] { null }));
                Assert.That(error.Message, Does.Contain("missing a SkinnedMeshRenderer reference"));
            }
        }

        [Test]
        public void ImportedBehaviorsRewriteSetAndParameterNamesAndFollowLinks() {
            using (var build = new Build()) {
                var source = build.Skin("Source");
                var target = build.Skin("Target");
                var unrelated = build.Skin("Unrelated");
                var shape = new VRCAvatarDescriptor.FaceTrackingParameterBehavior {
                    targetMode = VRCAvatarDescriptor.ParameterBehaviorTargetMode.ApplyToBlendshape,
                    targetBlendshapeName = "Smile", weightBlendSetName = "Face"
                };
                build.Full(source.gameObject, behaviors: new[] {
                    shape,
                    new VRCAvatarDescriptor.FaceTrackingParameterBehavior {
                        targetMode = VRCAvatarDescriptor.ParameterBehaviorTargetMode.ApplyBlendSetWeight,
                        targetBlendSetName = "Face"
                    },
                    new VRCAvatarDescriptor.FaceTrackingParameterBehavior {
                        targetMode = VRCAvatarDescriptor.ParameterBehaviorTargetMode.ForwardToAnimator,
                        outputParameterName = "Output"
                    }
                });
                build.Link(source, target);
                build.Run();
                var result = build.avatar.faceTrackingParameterBehaviors;
                var mappedShape = result.Single(b => b.targetMode == shape.targetMode);
                Assert.That(source.HasBlendshape(mappedShape.targetBlendshapeName), Is.True);
                Assert.That(target.HasBlendshape(mappedShape.targetBlendshapeName), Is.True);
                Assert.That(unrelated.HasBlendshape(mappedShape.targetBlendshapeName), Is.False);
                Assert.That(mappedShape.weightBlendSetName, Is.EqualTo("VF1_Face"));
                Assert.That(result.Single(b => b.targetMode == VRCAvatarDescriptor.ParameterBehaviorTargetMode.ApplyBlendSetWeight)
                    .targetBlendSetName, Is.EqualTo("VF1_Face"));
                Assert.That(result.Single(b => b.targetMode == VRCAvatarDescriptor.ParameterBehaviorTargetMode.ForwardToAnimator)
                    .outputParameterName, Is.Not.EqualTo("Output").And.EndsWith("Output"));
                Assert.That(shape.targetBlendshapeName, Is.EqualTo("Smile"));
                Assert.That(shape.weightBlendSetName, Is.EqualTo("Face"));
            }
        }

        [TestCase("")]
        [TestCase(null)]
        public void ImportedUnnamedSetDoesNotGateUnweightedBehaviors(string weightSetName) {
            using (var build = new Build()) {
                var source = build.Skin("Source");
                var behaviors = System.Enum.GetValues(typeof(VRCAvatarDescriptor.ParameterBehaviorTargetMode))
                    .Cast<VRCAvatarDescriptor.ParameterBehaviorTargetMode>()
                    .Select(mode => new VRCAvatarDescriptor.FaceTrackingParameterBehavior {
                        targetMode = mode,
                        targetBlendSetName = "",
                        targetBlendshapeName = "Smile",
                        outputParameterName = "Output",
                        inputTrackingKeyNames = new[] { "Voice" },
                        weightBlendSetName = weightSetName
                    }).ToArray();
                build.Full(source.gameObject, setName: "", setWeight: 0.25f, behaviors: behaviors);
                build.Run();
                var set = build.avatar.faceTrackingBlendshapesSetList.Single();
                Assert.That(set.setName, Is.Not.Empty);
                Assert.That(set.setWeight, Is.EqualTo(0.25f));
                var result = build.avatar.faceTrackingParameterBehaviors;
                Assert.That(result.Count, Is.EqualTo(behaviors.Length));
                Assert.That(result.All(behavior => string.IsNullOrEmpty(behavior.weightBlendSetName)), Is.True);
                Assert.That(result.Single(behavior => behavior.targetMode == VRCAvatarDescriptor.ParameterBehaviorTargetMode.ApplyBlendSetWeight)
                    .targetBlendSetName, Is.EqualTo(set.setName));
                Assert.That(behaviors.All(behavior => behavior.weightBlendSetName == weightSetName), Is.True);
            }
        }

        [Test]
        public void ImportedVisemesEnableTheirDescriptorModeAndPreserveLegacyVisemes() {
            using (var build = new Build()) {
                var source = build.Skin("Source");
                var legacy = build.Skin("Legacy");
                build.avatar.lipSync = VRC.SDKBase.VRC_AvatarDescriptor.LipSyncStyle.VisemeBlendShape;
                build.avatar.VisemeSkinnedMesh = legacy;
                build.avatar.VisemeBlendShapes = Enumerable.Repeat("Smile", (int)VRC.SDKBase.VRC_AvatarDescriptor.Viseme.Count).ToArray();
                build.Full(source.gameObject, visemes: true);
                build.Run();
                Assert.That(build.avatar.lipSync, Is.EqualTo(VRC.SDKBase.VRC_AvatarDescriptor.LipSyncStyle.VisemeBlendShapeFaceTracking));
                var legacyName = build.avatar.faceTrackingBlendshapesSetList.Single(set => set.setName == "_vrcf_upgraded_visemes")
                    .visemeBlendshapeNames[1];
                Assert.That(legacy.HasBlendshape(legacyName), Is.True);
                Assert.That(source.HasBlendshape(legacyName), Is.False);
                Assert.That(source.HasBlendshape(build.avatar.faceTrackingBlendshapesSetList.Single(set => set.setName.EndsWith("_Face"))
                    .visemeBlendshapeNames[1]), Is.True);
            }
        }

        [TestCase(false)]
        [TestCase(true)]
        public void JawFlapBlendshapeMigratesToVoiceBehavior(bool nativeTracking) {
            using (var build = new Build()) {
                var jaw = build.Skin("Jaw");
                var imported = build.Skin("Imported");
                build.avatar.enableFaceTracking = nativeTracking;
                build.avatar.lipSync = VRC.SDKBase.VRC_AvatarDescriptor.LipSyncStyle.JawFlapBlendShape;
                build.avatar.VisemeSkinnedMesh = jaw;
                build.avatar.MouthOpenBlendShapeName = "Smile";
                build.Full(imported.gameObject, visemes: true);
                build.faceTracking.EnsureVisemesEnabled();
                build.Run();
                var behavior = build.avatar.faceTrackingParameterBehaviors.Single();
                Assert.That(behavior.targetMode, Is.EqualTo(VRCAvatarDescriptor.ParameterBehaviorTargetMode.ApplyToBlendshape));
                Assert.That(behavior.inputTrackingKeyNames, Is.EqualTo(new[] { "Voice" }));
                Assert.That(jaw.HasBlendshape(behavior.targetBlendshapeName), Is.True);
                Assert.That(imported.HasBlendshape(behavior.targetBlendshapeName), Is.False);
                Assert.That(build.avatar.faceTrackingMeshes, Does.Contain(jaw));
            }
        }

        [Test]
        public void JawFlapUpgradeIsIdempotentAndOnlyPublishesWhenApplied() {
            using (var build = new Build()) {
                var jaw = build.Skin("Jaw");
                build.avatar.lipSync = VRC.SDKBase.VRC_AvatarDescriptor.LipSyncStyle.JawFlapBlendShape;
                build.avatar.VisemeSkinnedMesh = jaw;
                build.avatar.MouthOpenBlendShapeName = "Smile";
                build.faceTracking.EnsureVisemesEnabled();
                build.faceTracking.EnsureVisemesEnabled();
                var plan = build.faceTracking.CreatePlan();
                Assert.That(plan.meshes, Does.Contain(jaw));
                Assert.That(plan.sets, Is.Empty);
                Assert.That(plan.behaviors.Single().targetBlendshapeName, Is.EqualTo("Smile"));
                Assert.That(build.avatar.faceTrackingParameterBehaviors ?? new List<VRCAvatarDescriptor.FaceTrackingParameterBehavior>(), Is.Empty);
                build.faceTracking.ApplyPlan(plan);
                Assert.That(build.avatar.faceTrackingParameterBehaviors.Single().targetBlendshapeName, Is.EqualTo("Smile"));
                Assert.That(build.avatar.faceTrackingMeshes, Does.Contain(jaw));
            }
        }

        [Test]
        public void BoneJawFlapCannotBeSilentlyReplaced() {
            using (var build = new Build()) {
                build.avatar.lipSync = VRC.SDKBase.VRC_AvatarDescriptor.LipSyncStyle.JawFlapBone;
                Assert.Throws<VF.Exceptions.VRCFBuilderException>(() => build.faceTracking.EnsureVisemesEnabled());
                Assert.That(build.avatar.lipSync, Is.EqualTo(VRC.SDKBase.VRC_AvatarDescriptor.LipSyncStyle.JawFlapBone));
            }
        }

        [TestCase(false)]
        [TestCase(true)]
        public void BoneEyelidsOnlyPreventUpgradeWhenActive(bool enabled) {
            using (var build = new Build()) {
                build.avatar.enableEyeLook = enabled;
                build.avatar.customEyeLookSettings.eyelidType = VRCAvatarDescriptor.EyelidType.Bones;
                if (enabled) {
                    Assert.Throws<VF.Exceptions.VRCFBuilderException>(() => build.faceTracking.EnsureEyelidsEnabled());
                    Assert.That(build.avatar.customEyeLookSettings.eyelidType, Is.EqualTo(VRCAvatarDescriptor.EyelidType.Bones));
                } else {
                    Assert.DoesNotThrow(() => build.faceTracking.EnsureEyelidsEnabled());
                }
            }
        }

        [TestCase(false)]
        [TestCase(true)]
        public void ShapesAddedAfterCaptureAreResolvedAtFinalization(bool native) {
            using (var build = new Build()) {
                var skin = build.Skin("Body");
                var unrelated = build.Skin("Unrelated");
                unrelated.sharedMesh.AddBlendShapeFrame("Late", 100, new[] { Vector3.up }, null, null);
                var set = new VRCAvatarDescriptor.FaceTrackingBlendshapesSet { setName = "Late", setWeight = 1 };
                SetMapping(set, (int)VRCAvatarDescriptor.FaceTrackingKey.JawOpen, "Late");
                if (native) {
                    build.avatar.enableFaceTracking = true;
                    build.avatar.useAllTrackingKeys = true;
                    build.avatar.faceTrackingMeshes = new List<SkinnedMeshRenderer> { skin };
                    build.avatar.faceTrackingBlendshapesSetList = new List<VRCAvatarDescriptor.FaceTrackingBlendshapesSet> { set };
                    build.faceTracking.CaptureInitialState();
                } else {
                    build.faceTracking.RegisterImportedSet(set, new[] { skin });
                }
                skin.sharedMesh.AddBlendShapeFrame("Late", 100, new[] { Vector3.up }, null, null);
                var plan = build.faceTracking.CreatePlan();
                Assert.That(plan.warnings, Is.Empty);
                build.faceTracking.ApplyPlan(plan);
                var name = build.avatar.faceTrackingBlendshapesSetList.Single().faceTrackingBlendshapeNames[(int)VRCAvatarDescriptor.FaceTrackingKey.JawOpen];
                Assert.That(skin.HasBlendshape(name), Is.True);
                Assert.That(build.avatar.faceTrackingMeshes, Has.No.Member(unrelated));
            }
        }

        [TestCase(32, false)]
        [TestCase(33, false)]
        [TestCase(33, true)]
        public void ImportedBehaviorInputLimitIsEnforced(int count, bool custom) {
            using (var build = new Build()) {
                var behavior = new VRCAvatarDescriptor.FaceTrackingParameterBehavior {
                    targetMode = VRCAvatarDescriptor.ParameterBehaviorTargetMode.ForwardToAnimator,
                    outputParameterName = "Output",
                    inputTrackingKeyNames = Enumerable.Repeat("Voice", count).ToArray(),
                    customExpression = "Voice"
                };
                if (custom) behavior.combineMode = VRCAvatarDescriptor.ParameterBehaviorCombineMode.Custom;
                if (count > 32 && !custom) {
                    Assert.Throws<VF.Exceptions.VRCFBuilderException>(() => build.faceTracking.RegisterImportedBehavior(behavior, System.Array.Empty<SkinnedMeshRenderer>()));
                    Assert.That(build.avatar.enableFaceTracking, Is.False);
                } else {
                    Assert.DoesNotThrow(() => build.faceTracking.RegisterImportedBehavior(behavior, System.Array.Empty<SkinnedMeshRenderer>()));
                }
            }
        }

        [Test]
        public void IgnoredNativeInputsDoNotCauseActiveBehaviorToBeRemoved() {
            using (var build = new Build()) {
                build.avatar.enableFaceTracking = true;
                build.avatar.faceTrackingParameterBehaviors = new List<VRCAvatarDescriptor.FaceTrackingParameterBehavior> {
                    new VRCAvatarDescriptor.FaceTrackingParameterBehavior {
                        targetMode = VRCAvatarDescriptor.ParameterBehaviorTargetMode.ForwardToAnimator,
                        outputParameterName = "Native",
                        inputTrackingKeyNames = Enumerable.Repeat("Voice", 32).Concat(new[] { "JawOpen" }).ToArray()
                    }
                };
                build.Full(build.Skin("Imported").gameObject);
                build.Run();
                Assert.That(build.avatar.faceTrackingParameterBehaviors.Single().outputParameterName, Is.EqualTo("Native"));
            }
        }

        [TestCase(VRC.SDKBase.VRC_AvatarDescriptor.LipSyncStyle.JawFlapBlendShape)]
        [TestCase(VRC.SDKBase.VRC_AvatarDescriptor.LipSyncStyle.VisemeParameterOnly)]
        public void RenamingFaceTrackingShapesPreservesActiveJawFlapReferences(VRC.SDKBase.VRC_AvatarDescriptor.LipSyncStyle mode) {
            using (var build = new Build()) {
                var skin = build.Skin("Body");
                build.avatar.lipSync = mode;
                build.avatar.VisemeSkinnedMesh = skin;
                build.avatar.MouthOpenBlendShapeName = "Smile";
                build.Full(skin.gameObject);
                var plan = build.faceTracking.CreatePlan();
                Assert.That(build.avatar.MouthOpenBlendShapeName, Is.EqualTo("Smile"), "Planning must not rewrite the descriptor");
                build.faceTracking.ApplyPlan(plan);
                Assert.That(build.avatar.lipSync, Is.EqualTo(mode));
                var mapping = build.avatar.faceTrackingBlendshapesSetList.Single()
                    .faceTrackingBlendshapeNames[(int)VRCAvatarDescriptor.FaceTrackingKey.JawOpen];
                Assert.That(skin.HasBlendshape(mapping), Is.True);
                Assert.That(build.avatar.MouthOpenBlendShapeName,
                    Is.EqualTo(mode == VRC.SDKBase.VRC_AvatarDescriptor.LipSyncStyle.JawFlapBlendShape ? mapping : "Smile"));
            }
        }

        [TestCase(VRC.SDKBase.VRC_AvatarDescriptor.LipSyncStyle.JawFlapBlendShape)]
        [TestCase(VRC.SDKBase.VRC_AvatarDescriptor.LipSyncStyle.VisemeBlendShape)]
        public void SurplusVisemeSlotsDoNotChangeLipSyncMode(VRC.SDKBase.VRC_AvatarDescriptor.LipSyncStyle mode) {
            using (var build = new Build()) {
                var skin = build.Skin("Body");
                build.avatar.lipSync = mode;
                build.avatar.VisemeSkinnedMesh = skin;
                build.avatar.MouthOpenBlendShapeName = "Smile";
                build.avatar.VisemeBlendShapes = Enumerable.Repeat("Smile", (int)VRC.SDKBase.VRC_AvatarDescriptor.Viseme.Count).ToArray();
                var set = new VRCAvatarDescriptor.FaceTrackingBlendshapesSet {
                    setName = "Surplus",
                    visemeBlendshapeNames = Enumerable.Repeat("", (int)VRC.SDKBase.VRC_AvatarDescriptor.Viseme.Count).Concat(new[] { "Smile" }).ToArray()
                };
                build.faceTracking.RegisterImportedSet(set, new[] { skin });
                var plan = build.faceTracking.CreatePlan();
                Assert.That(plan.warnings, Is.Empty);
                build.faceTracking.ApplyPlan(plan);
                Assert.That(build.avatar.lipSync, Is.EqualTo(mode));
                Assert.That(build.avatar.faceTrackingBlendshapesSetList.Single().visemeBlendshapeNames, Is.All.Empty);
                Assert.That(skin.HasBlendshape("Smile"), Is.True);
            }
        }

        [TestCase(false, false)]
        [TestCase(true, false)]
        [TestCase(false, true)]
        [TestCase(true, true)]
        public void NativeMeshPlaceholdersDoNotConsumeCapacity(bool duplicates, bool upgradeVisemes) {
            using (var build = new Build()) {
                var native = build.Skin("Native");
                var added = build.Skin("Added");
                build.avatar.enableFaceTracking = true;
                var input = Enumerable.Repeat(duplicates ? native : null, FaceTrackingUtils.MaxFaceTrackingMeshes).ToList();
                input[0] = native;
                build.avatar.faceTrackingMeshes = input;
                if (upgradeVisemes) {
                    build.avatar.lipSync = VRC.SDKBase.VRC_AvatarDescriptor.LipSyncStyle.VisemeBlendShape;
                    build.avatar.VisemeSkinnedMesh = added;
                    build.avatar.VisemeBlendShapes = Enumerable.Repeat("Smile", (int)VRC.SDKBase.VRC_AvatarDescriptor.Viseme.Count).ToArray();
                    build.faceTracking.EnsureVisemesEnabled();
                }
                build.Full(added.gameObject);
                Assert.That(input, Has.Count.EqualTo(FaceTrackingUtils.MaxFaceTrackingMeshes), "Do not mutate captured descriptor inputs");
                build.Run();
                Assert.That(build.avatar.faceTrackingMeshes, Is.EquivalentTo(new[] { native, added }));
            }
        }

        [Test]
        public void MeshesDeletedAfterCaptureDoNotConsumeCapacity() {
            using (var build = new Build()) {
                var added = build.Skin("Added");
                build.avatar.enableFaceTracking = true;
                var native = Enumerable.Range(0, FaceTrackingUtils.MaxFaceTrackingMeshes).Select(i => build.Skin("Native" + i)).ToList();
                build.avatar.faceTrackingMeshes = native;
                build.faceTracking.CaptureInitialState();
                foreach (var skin in native) Object.DestroyImmediate(skin.gameObject);
                build.Full(added.gameObject);
                build.Run();
                Assert.That(build.avatar.faceTrackingMeshes, Is.EquivalentTo(new[] { added }));
            }
        }

        [Test]
        public void EnsureMeshCompactsNullAndDuplicateEntries() {
            using (var build = new Build()) {
                var native = build.Skin("Native");
                var added = build.Skin("Added");
                build.avatar.faceTrackingMeshes = Enumerable.Repeat(native, FaceTrackingUtils.MaxFaceTrackingMeshes).ToList();
                build.avatar.faceTrackingMeshes[1] = null;
                build.NativeMesh(added);
                Assert.That(build.avatar.faceTrackingMeshes, Is.EqualTo(new[] { native, added }));
            }
        }

        [TestCase(false)]
        [TestCase(true)]
        public void UnresolvedNonemptyMappingsProduceWarningsWithoutFailingTheBuild(bool native) {
            using (var build = new Build()) {
                var skin = build.Skin("Body");
                var set = new VRCAvatarDescriptor.FaceTrackingBlendshapesSet { setName = "MissingSet" };
                SetMapping(set, (int)VRCAvatarDescriptor.FaceTrackingKey.JawOpen, "MissingTracking");
                SetMapping(set, 1, "MissingViseme", visemes: true);
                var behavior = new VRCAvatarDescriptor.FaceTrackingParameterBehavior {
                    targetMode = VRCAvatarDescriptor.ParameterBehaviorTargetMode.ApplyToBlendshape,
                    targetBlendshapeName = "MissingBehavior", inputTrackingKeyNames = new[] { "Voice" }
                };
                if (native) {
                    build.avatar.enableFaceTracking = true;
                    build.avatar.useAllTrackingKeys = true;
                    build.avatar.lipSync = VRC.SDKBase.VRC_AvatarDescriptor.LipSyncStyle.VisemeBlendShapeFaceTracking;
                    build.avatar.faceTrackingMeshes = new List<SkinnedMeshRenderer> { skin };
                    build.avatar.faceTrackingBlendshapesSetList = new List<VRCAvatarDescriptor.FaceTrackingBlendshapesSet> { set };
                    build.avatar.faceTrackingParameterBehaviors = new List<VRCAvatarDescriptor.FaceTrackingParameterBehavior> { behavior };
                } else {
                    build.faceTracking.RegisterImportedSet(set, new[] { skin });
                    build.faceTracking.RegisterImportedBehavior(behavior, new[] { skin });
                }
                var plan = build.faceTracking.CreatePlan();
                Assert.That(plan.warnings, Has.Count.EqualTo(3));
                Assert.That(plan.warnings.Any(warning => warning.Contains("MissingSet") && warning.Contains("JawOpen") && warning.Contains("MissingTracking")), Is.True);
                Assert.That(plan.warnings.Any(warning => warning.Contains("MissingViseme")), Is.True);
                Assert.That(plan.warnings.Any(warning => warning.Contains("MissingBehavior")), Is.True);
                LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("VRCFury: Some Face Tracking blendshape mappings could not be resolved"));
                Assert.DoesNotThrow(() => build.faceTracking.ApplyPlan(plan));
                Assert.That(build.avatar.faceTrackingBlendshapesSetList.Single().faceTrackingBlendshapeNames, Is.All.Empty);
                Assert.That(build.avatar.faceTrackingBlendshapesSetList.Single().visemeBlendshapeNames, Is.All.Empty);
                Assert.That(build.avatar.faceTrackingParameterBehaviors, Is.Empty);
            }
        }

        [Test]
        public void EmptyAndResolvedMappingsDoNotProduceWarnings() {
            using (var build = new Build()) {
                var skin = build.Skin("Body");
                build.Full(skin.gameObject);
                Assert.That(build.faceTracking.CreatePlan().warnings, Is.Empty);
            }
        }

        [Test]
        public void LegacyVisemeLookupAndUpgradeUseTheSameBoundedMappings() {
            using (var build = new Build()) {
                var skin = build.Skin("Legacy");
                var count = (int)VRC.SDKBase.VRC_AvatarDescriptor.Viseme.Count;
                build.avatar.lipSync = VRC.SDKBase.VRC_AvatarDescriptor.LipSyncStyle.VisemeBlendShape;
                build.avatar.VisemeSkinnedMesh = skin;
                build.avatar.VisemeBlendShapes = Enumerable.Repeat("Smile", count).Concat(new[] { "Ignored" }).ToArray();
                build.avatar.VisemeBlendShapes[0] = null;
                var (mesh, names) = FaceTrackingUtils.GetLegacyVisemes(build.avatar);
                Assert.That(mesh, Is.SameAs(skin));
                Assert.That(names, Has.Length.EqualTo(count));
                Assert.That(names[0], Is.Empty);
                Assert.That(names, Does.Not.Contain("Ignored"));
                build.faceTracking.EnsureVisemesEnabled();
                build.Run();
                var upgraded = build.avatar.faceTrackingBlendshapesSetList.Single();
                Assert.That(build.avatar.faceTrackingMeshes, Does.Contain(skin));
                Assert.That(build.avatar.faceTrackingParameterBehaviors, Is.Empty);
                Assert.That(upgraded.visemeBlendshapeNames, Is.EqualTo(names));
                Assert.That(FaceTrackingUtils.GetLegacyVisemes(build.avatar).mesh, Is.Null);
                Assert.That(FaceTrackingUtils.GetLegacyVisemes(build.avatar).names, Is.Empty);
            }
        }

        [TestCase(true, true)]
        [TestCase(true, false)]
        [TestCase(false, true)]
        public void LegacyEyelidLookupAndUpgradeUseTheSameActiveMappings(bool enabled, bool boneGaze) {
            using (var build = new Build()) {
                var skin = build.Skin("Legacy");
                skin.sharedMesh.AddBlendShapeFrame("Up", 100, new[] { Vector3.up }, null, null);
                skin.sharedMesh.AddBlendShapeFrame("Down", 100, new[] { Vector3.down }, null, null);
                build.avatar.enableEyeLook = enabled;
                build.avatar.customEyeLookSettings.eyelidType = VRCAvatarDescriptor.EyelidType.Blendshapes;
                build.avatar.customEyeLookSettings.eyeGazeMode = boneGaze
                    ? VRCAvatarDescriptor.CustomEyeLookSettings.EyeGazeMode.BoneRotation
                    : VRCAvatarDescriptor.CustomEyeLookSettings.EyeGazeMode.Both;
                build.avatar.customEyeLookSettings.eyelidsSkinnedMesh = skin;
                build.avatar.customEyeLookSettings.eyelidsBlendshapes = new[] { 0, 1, 2 };
                var (mesh, names) = FaceTrackingUtils.GetLegacyEyelids(build.avatar);
                Assert.That(mesh, enabled ? Is.SameAs(skin) : Is.Null);
                Assert.That(names, Is.EqualTo(new[] { enabled ? "Smile" : "", enabled && boneGaze ? "Up" : "", enabled && boneGaze ? "Down" : "" }));
                build.faceTracking.EnsureEyelidsEnabled();
                build.Run();
                var upgraded = build.avatar.faceTrackingBlendshapesSetList.SingleOrDefault();
                if (enabled) {
                    Assert.That(build.avatar.faceTrackingMeshes, Does.Contain(skin));
                    Assert.That(build.avatar.faceTrackingParameterBehaviors, Is.Empty);
                    Assert.That(upgraded.faceTrackingBlendshapeNames[(int)VRCAvatarDescriptor.FaceTrackingKey.EyeClosedLeft], Is.EqualTo(names[0]));
                    Assert.That(upgraded.faceTrackingBlendshapeNames[(int)VRCAvatarDescriptor.FaceTrackingKey.EyeLookUpLeft], Is.EqualTo(names[1]));
                    Assert.That(upgraded.faceTrackingBlendshapeNames[(int)VRCAvatarDescriptor.FaceTrackingKey.EyeLookDownLeft], Is.EqualTo(names[2]));
                } else Assert.That(upgraded, Is.Null);
                Assert.That(FaceTrackingUtils.GetLegacyEyelids(build.avatar).mesh, Is.Null);
                Assert.That(FaceTrackingUtils.GetLegacyEyelids(build.avatar).names, Is.All.Empty);
            }
        }

        [TestCase(VRCAvatarDescriptor.FaceTrackingKey.EyeClosedLeft)]
        [TestCase(VRCAvatarDescriptor.FaceTrackingKey.EyeLookUpLeft)]
        public void ImportedEyeMappingsEnableTheirDescriptorModes(VRCAvatarDescriptor.FaceTrackingKey key) {
            using (var build = new Build()) {
                var source = build.Skin("Source");
                build.Full(source.gameObject, key);
                build.Run();
                Assert.That(build.avatar.enableEyeLook, Is.True);
                if (key == VRCAvatarDescriptor.FaceTrackingKey.EyeClosedLeft)
                    Assert.That(build.avatar.customEyeLookSettings.eyelidType, Is.EqualTo(VRCAvatarDescriptor.EyelidType.FaceTracking));
                else
                    Assert.That(build.avatar.customEyeLookSettings.eyeGazeMode, Is.EqualTo(VRCAvatarDescriptor.CustomEyeLookSettings.EyeGazeMode.Both));
            }
        }

        [TestCase(false, false)]
        [TestCase(false, true)]
        [TestCase(true, false)]
        [TestCase(true, true)]
        public void ImportedVisemeInputsEnableTheirDescriptorMode(bool custom, bool legacy) {
            using (var build = new Build()) {
                var source = build.Skin("Source");
                var original = build.Skin("Legacy");
                if (legacy) {
                    build.avatar.lipSync = VRC.SDKBase.VRC_AvatarDescriptor.LipSyncStyle.VisemeBlendShape;
                    build.avatar.VisemeSkinnedMesh = original;
                    build.avatar.VisemeBlendShapes = Enumerable.Repeat("Smile", (int)VRC.SDKBase.VRC_AvatarDescriptor.Viseme.Count).ToArray();
                }
                var behavior = new VRCAvatarDescriptor.FaceTrackingParameterBehavior {
                    targetMode = VRCAvatarDescriptor.ParameterBehaviorTargetMode.ForwardToAnimator,
                    outputParameterName = "Output",
                    inputTrackingKeyNames = new[] { "Viseme_aa" },
                    // Serialized indices are not authoritative and may be stale before SDK preprocessing.
                    inputTrackingKeys = new[] { 0 },
                    customExpression = custom ? "max(Viseme_aa, Voice)" : "Voice"
                };
                if (custom) behavior.combineMode = VRCAvatarDescriptor.ParameterBehaviorCombineMode.Custom;
                build.Full(source.gameObject, behaviors: new[] { behavior });
                build.Run();
                Assert.That(build.avatar.lipSync, Is.EqualTo(VRC.SDKBase.VRC_AvatarDescriptor.LipSyncStyle.VisemeBlendShapeFaceTracking));
                Assert.That(build.avatar.faceTrackingParameterBehaviors, Has.Count.EqualTo(1));
                var upgraded = build.avatar.faceTrackingBlendshapesSetList.Where(set => set.setName == "_vrcf_upgraded_visemes").ToArray();
                Assert.That(upgraded, Has.Length.EqualTo(legacy ? 1 : 0));
                if (legacy) {
                    Assert.That(original.HasBlendshape(upgraded[0].visemeBlendshapeNames[1]), Is.True);
                    Assert.That(source.HasBlendshape(upgraded[0].visemeBlendshapeNames[1]), Is.False);
                }
            }
        }

        [TestCase(false)]
        [TestCase(true)]
        public void ImportedNonVisemeInputsDoNotChangeLipSync(bool custom) {
            using (var build = new Build()) {
                var source = build.Skin("Source");
                var before = build.avatar.lipSync;
                var behavior = new VRCAvatarDescriptor.FaceTrackingParameterBehavior {
                    targetMode = VRCAvatarDescriptor.ParameterBehaviorTargetMode.ForwardToAnimator,
                    outputParameterName = "Output",
                    inputTrackingKeyNames = new[] { custom ? "Viseme_aa" : "JawOpen" },
                    customExpression = "NotViseme_aa + Viseme_aa_suffix"
                };
                if (custom) behavior.combineMode = VRCAvatarDescriptor.ParameterBehaviorCombineMode.Custom;
                build.Full(source.gameObject, behaviors: new[] { behavior });
                build.Run();
                Assert.That(build.avatar.lipSync, Is.EqualTo(before));
            }
        }

        [Test]
        public void NativeDuplicateSetNamesUseOnlyTheLastSetsMappingsAndWeight() {
            using (var build = new Build()) {
                var skin = build.Skin("Original");
                build.avatar.enableFaceTracking = true;
                build.NativeMesh(skin);
                var shadowed = build.NativeSet("Shared");
                SetMapping(shadowed, (int)VRCAvatarDescriptor.FaceTrackingKey.JawOpen, "Smile");
                shadowed.setWeight = 1;
                var winner = build.NativeSet("Shared");
                winner.setName = "Shared";
                winner.setWeight = 0.25f;
                SetMapping(winner, (int)VRCAvatarDescriptor.FaceTrackingKey.EyeClosedLeft, "Smile");
                var control = build.ControlSet("Shared");
                build.Run();
                var result = build.avatar.faceTrackingBlendshapesSetList.Single();
                Assert.That(result.setName, Is.EqualTo("Shared"));
                Assert.That(result.setWeight, Is.EqualTo(0.25f));
                Assert.That(result.faceTrackingBlendshapeNames[(int)VRCAvatarDescriptor.FaceTrackingKey.JawOpen], Is.Empty);
                Assert.That(result.faceTrackingBlendshapeNames[(int)VRCAvatarDescriptor.FaceTrackingKey.EyeClosedLeft], Is.EqualTo("Smile"));
                Assert.That(control.Read<VRCAnimatorFaceTrackingBlendshapesSet>().blendSets.Single().blendshapesSetName, Is.EqualTo("Shared"));
            }
        }

        [Test]
        public void OverlappingFullControllersPreserveTheirScopesWhenAParentJoinsThem() {
            using (var build = new Build()) {
                var left = build.Skin("Left");
                var right = build.Skin("Right");
                build.avatar.enableFaceTracking = true;
                build.Full(left.gameObject);
                build.Full(right.gameObject);
                build.Full(build.root, setWeight: 0.35f, skins: new[] { left, right }, behaviors: new[] {
                    new VRCAvatarDescriptor.FaceTrackingParameterBehavior {
                        targetMode = VRCAvatarDescriptor.ParameterBehaviorTargetMode.ApplyBlendSetWeight,
                        targetBlendSetName = "Face"
                    }
                });
                var control = build.ControlSet("VF3_Face");
                build.Run();
                var sets = build.avatar.faceTrackingBlendshapesSetList;
                var leftName = sets.Single(set => set.setName == "VF1_Face").faceTrackingBlendshapeNames[(int)VRCAvatarDescriptor.FaceTrackingKey.JawOpen];
                var rightName = sets.Single(set => set.setName == "VF2_Face").faceTrackingBlendshapeNames[(int)VRCAvatarDescriptor.FaceTrackingKey.JawOpen];
                Assert.That(left.HasBlendshape(leftName), Is.True);
                Assert.That(right.HasBlendshape(leftName), Is.False);
                Assert.That(right.HasBlendshape(rightName), Is.True);
                Assert.That(left.HasBlendshape(rightName), Is.False);
                Assert.That(sets.Where(set => set.setName.StartsWith("VF3_Face")).Select(set =>
                    set.faceTrackingBlendshapeNames[(int)VRCAvatarDescriptor.FaceTrackingKey.JawOpen]),
                    Is.EquivalentTo(new[] { leftName, rightName }));
                Assert.That(sets.Select(set => set.setName).Distinct().Count(), Is.EqualTo(4));
                var expanded = control.Read<VRCAnimatorFaceTrackingBlendshapesSet>().blendSets;
                Assert.That(expanded.Select(entry => entry.blendshapesSetName),
                    Is.EquivalentTo(sets.Where(set => set.setName.StartsWith("VF3_Face")).Select(set => set.setName)));
                Assert.That(expanded.All(entry => entry.sourceParameter == "VF_Weight" && entry.weight == 0.4f && entry.lerpSpeed == 0.25f), Is.True);
                Assert.That(sets.Where(set => set.setName.StartsWith("VF3_Face")).All(set => set.setWeight == 0.35f), Is.True);
                Assert.That(build.avatar.faceTrackingParameterBehaviors.Select(behavior => behavior.targetBlendSetName),
                    Is.EquivalentTo(expanded.Select(entry => entry.blendshapesSetName)));
            }
        }

        [Test]
        public void NativeFaceTrackingDoesNotLeakOntoAnImportedMeshWithTheSameShapeName() {
            using (var build = new Build()) {
                var original = build.Skin("Original");
                var imported = build.Skin("Imported");
                build.avatar.enableFaceTracking = true;
                build.avatar.useAllTrackingKeys = true;
                build.NativeMesh(original);
                var native = build.NativeSet("Native");
                SetMapping(native, (int)VRCAvatarDescriptor.FaceTrackingKey.JawOpen, "Smile");
                build.Full(imported.gameObject);
                build.Run();
                var sets = build.avatar.faceTrackingBlendshapesSetList;
                var nativeName = sets.Single(set => set.setName == "Native").faceTrackingBlendshapeNames[(int)VRCAvatarDescriptor.FaceTrackingKey.JawOpen];
                Assert.That(nativeName, Is.EqualTo("Smile"));
                var importedName = sets.Single(set => set.setName == "VF1_Face").faceTrackingBlendshapeNames[(int)VRCAvatarDescriptor.FaceTrackingKey.JawOpen];
                Assert.That(original.HasBlendshape(nativeName), Is.True);
                Assert.That(imported.HasBlendshape(nativeName), Is.False);
                Assert.That(imported.HasBlendshape(importedName), Is.True);
                Assert.That(original.HasBlendshape(importedName), Is.False);
            }
        }

        [TestCase(VRCAvatarDescriptor.FaceTrackingKey.EyeClosedLeft, false)]
        [TestCase(VRCAvatarDescriptor.FaceTrackingKey.EyeLookUpLeft, false)]
        [TestCase(VRCAvatarDescriptor.FaceTrackingKey.JawOpen, true)]
        public void BasicImportsDoNotEnableInactiveNativeHardwareMappings(VRCAvatarDescriptor.FaceTrackingKey key, bool visemes) {
            using (var build = new Build()) {
                var native = build.Skin("Native");
                var imported = build.Skin("Imported");
                build.avatar.enableFaceTracking = true;
                build.avatar.useAllTrackingKeys = false;
                build.NativeMesh(native);
                var set = build.NativeSet("Native");
                SetMapping(set, (int)VRCAvatarDescriptor.FaceTrackingKey.JawOpen, "Smile");
                build.Full(imported.gameObject, key, visemes);
                build.Run();
                Assert.That(build.avatar.useAllTrackingKeys, Is.False);
                Assert.That(build.avatar.faceTrackingBlendshapesSetList.Single(entry => entry.setName == "Native")
                    .faceTrackingBlendshapeNames[(int)VRCAvatarDescriptor.FaceTrackingKey.JawOpen], Is.EqualTo("Smile"));
            }
        }

        [TestCase(false, 0)]
        [TestCase(false, 1)]
        [TestCase(false, 2)]
        [TestCase(true, 0)]
        [TestCase(true, 1)]
        [TestCase(true, 2)]
        public void HardwareImportsDoNotActivateStaleNativeMappingsOrBehaviors(bool alreadyEnabled, int importMode) {
            using (var build = new Build()) {
                var native = build.Skin("Native");
                var imported = build.Skin("Imported");
                build.avatar.enableFaceTracking = true;
                build.avatar.useAllTrackingKeys = alreadyEnabled;
                build.avatar.enableEyeLook = true;
                build.avatar.customEyeLookSettings.eyelidType = VRCAvatarDescriptor.EyelidType.FaceTracking;
                build.NativeMesh(native);
                var set = build.NativeSet("Native");
                SetMapping(set, (int)VRCAvatarDescriptor.FaceTrackingKey.JawOpen, "Smile");
                SetMapping(set, (int)VRCAvatarDescriptor.FaceTrackingKey.EyeClosedLeft, "Smile");
                build.avatar.faceTrackingParameterBehaviors = new List<VRCAvatarDescriptor.FaceTrackingParameterBehavior> {
                    new VRCAvatarDescriptor.FaceTrackingParameterBehavior {
                        targetMode = VRCAvatarDescriptor.ParameterBehaviorTargetMode.ForwardToAnimator,
                        outputParameterName = "NativeHardware",
                        combineMode = VRCAvatarDescriptor.ParameterBehaviorCombineMode.Custom,
                        customExpression = "JawOpen"
                    },
                    new VRCAvatarDescriptor.FaceTrackingParameterBehavior {
                        targetMode = VRCAvatarDescriptor.ParameterBehaviorTargetMode.ForwardToAnimator,
                        outputParameterName = "NativeVoice",
                        inputTrackingKeyNames = new[] { "Voice" }
                    }
                };
                if (importMode != 0) {
                    build.Full(imported.gameObject, VRCAvatarDescriptor.FaceTrackingKey.EyeClosedLeft, behaviors: new[] {
                        new VRCAvatarDescriptor.FaceTrackingParameterBehavior {
                            targetMode = VRCAvatarDescriptor.ParameterBehaviorTargetMode.ForwardToAnimator,
                            outputParameterName = "ImportedHardware", inputTrackingKeyNames = new[] { "JawOpen" },
                            combineMode = importMode == 2 ? VRCAvatarDescriptor.ParameterBehaviorCombineMode.Custom : VRCAvatarDescriptor.ParameterBehaviorCombineMode.Max,
                            customExpression = "max(JawOpen, Voice)"
                        }
                    });
                } else build.Full(imported.gameObject);
                build.Run();
                Assert.That(build.avatar.useAllTrackingKeys, Is.True);
                var result = build.avatar.faceTrackingBlendshapesSetList.Single(entry => entry.setName == "Native");
                Assert.That(result.faceTrackingBlendshapeNames[(int)VRCAvatarDescriptor.FaceTrackingKey.JawOpen],
                    alreadyEnabled ? Is.Not.Empty : Is.Empty);
                Assert.That(result.faceTrackingBlendshapeNames[(int)VRCAvatarDescriptor.FaceTrackingKey.EyeClosedLeft], Is.Not.Empty);
                Assert.That(build.avatar.faceTrackingParameterBehaviors.Any(entry => entry.outputParameterName == "NativeHardware"), Is.EqualTo(alreadyEnabled));
                Assert.That(build.avatar.faceTrackingParameterBehaviors.Any(entry => entry.outputParameterName == "NativeVoice"), Is.True);
            }
        }

        [TestCase(VRCAvatarDescriptor.FaceTrackingKey.EyeClosedLeft)]
        [TestCase(VRCAvatarDescriptor.FaceTrackingKey.EyeLookUpLeft)]
        public void ExplicitUpgradeRegistrationPreservesLegacyEyeScopes(VRCAvatarDescriptor.FaceTrackingKey importedKey) {
            using (var build = new Build()) {
                var legacy = build.Skin("Legacy");
                legacy.sharedMesh.AddBlendShapeFrame("Up", 100, new[] { Vector3.right }, null, null);
                legacy.sharedMesh.AddBlendShapeFrame("Down", 100, new[] { Vector3.down }, null, null);
                var imported = build.Skin("Imported");
                build.avatar.enableEyeLook = true;
                build.avatar.customEyeLookSettings.eyelidType = VRCAvatarDescriptor.EyelidType.Blendshapes;
                build.avatar.customEyeLookSettings.eyeGazeMode = VRCAvatarDescriptor.CustomEyeLookSettings.EyeGazeMode.BoneRotation;
                build.avatar.customEyeLookSettings.eyelidsSkinnedMesh = legacy;
                build.avatar.customEyeLookSettings.eyelidsBlendshapes = new[] { 0, 1, 2 };
                build.Full(imported.gameObject, importedKey);
                build.faceTracking.EnsureEyelidsEnabled();
                build.Run();
                var upgraded = build.avatar.faceTrackingBlendshapesSetList.Single(set => set.setName == "_vrcf_upgraded_eyelids");
                var blink = upgraded.faceTrackingBlendshapeNames[(int)VRCAvatarDescriptor.FaceTrackingKey.EyeClosedLeft];
                Assert.That(legacy.HasBlendshape(blink), Is.True);
                Assert.That(imported.HasBlendshape(blink), Is.False);
                Assert.That(upgraded.faceTrackingBlendshapeNames[(int)VRCAvatarDescriptor.FaceTrackingKey.EyeLookUpLeft], Is.EqualTo("Up"));
                Assert.That(upgraded.faceTrackingBlendshapeNames[(int)VRCAvatarDescriptor.FaceTrackingKey.EyeLookDownLeft], Is.EqualTo("Down"));
                Assert.That(build.avatar.faceTrackingBlendshapesSetList, Has.Count.EqualTo(2));
            }
        }

        [Test, Combinatorial]
        public void ImportsOnlyPreserveAlreadyActiveVisemeDampening(
            [Values] bool faceTrackingEnabled,
            [Values] bool hardwareEnabled,
            [Values] bool visemesEnabled,
            [Values] bool hardwareFirst,
            [Values(VRCAvatarDescriptor.VisemeDampenMode.DampenTracking, VRCAvatarDescriptor.VisemeDampenMode.DampenVisemes)]
            VRCAvatarDescriptor.VisemeDampenMode mode
        ) {
            using (var build = new Build()) {
                var source = build.Skin("Source");
                build.avatar.enableFaceTracking = faceTrackingEnabled;
                build.avatar.useAllTrackingKeys = hardwareEnabled;
                build.avatar.lipSync = visemesEnabled
                    ? VRC.SDKBase.VRC_AvatarDescriptor.LipSyncStyle.VisemeBlendShapeFaceTracking
                    : VRC.SDKBase.VRC_AvatarDescriptor.LipSyncStyle.VisemeParameterOnly;
                build.avatar.visemeDampenMode = mode;
                build.avatar.visemeDampenAmount = 0.37f;
                build.Full(source.gameObject, visemes: !hardwareFirst);
                build.Full(source.gameObject, visemes: hardwareFirst);
                build.Run();
                Assert.That(build.avatar.enableFaceTracking, Is.True);
                Assert.That(build.avatar.useAllTrackingKeys, Is.True);
                Assert.That(build.avatar.lipSync, Is.EqualTo(VRC.SDKBase.VRC_AvatarDescriptor.LipSyncStyle.VisemeBlendShapeFaceTracking));
                Assert.That(build.avatar.visemeDampenMode,
                    Is.EqualTo(faceTrackingEnabled && hardwareEnabled && visemesEnabled ? mode : VRCAvatarDescriptor.VisemeDampenMode.None));
                Assert.That(build.avatar.visemeDampenAmount, Is.EqualTo(0.37f));
            }
        }

        [TestCase(false)]
        [TestCase(true)]
        public void BasicBehaviorInputsDoNotEnableHardwareTracking(bool custom) {
            using (var build = new Build()) {
                var source = build.Skin("Source");
                var behavior = new VRCAvatarDescriptor.FaceTrackingParameterBehavior {
                    targetMode = VRCAvatarDescriptor.ParameterBehaviorTargetMode.ForwardToAnimator,
                    outputParameterName = "Voice", inputTrackingKeyNames = new[] { "Voice", "Viseme_aa" },
                    customExpression = "max(Voice, Viseme_aa)"
                };
                if (custom) behavior.combineMode = VRCAvatarDescriptor.ParameterBehaviorCombineMode.Custom;
                build.Full(source.gameObject, VRCAvatarDescriptor.FaceTrackingKey.EyeClosedLeft, behaviors: new[] { behavior });
                build.Run();
                Assert.That(build.avatar.useAllTrackingKeys, Is.False);
                Assert.That(build.avatar.lipSync, Is.EqualTo(VRC.SDKBase.VRC_AvatarDescriptor.LipSyncStyle.VisemeBlendShapeFaceTracking));
            }
        }

        [TestCase(100)]
        [TestCase(140)]
        public void ImportedSetNamesAreBoundedAndTheirReferencesFollowRenames(int length) {
            using (var build = new Build()) {
                var source = build.Skin("Source");
                var name = new string('a', length);
                build.Full(source.gameObject, setName: name, behaviors: new[] {
                    new VRCAvatarDescriptor.FaceTrackingParameterBehavior {
                        targetMode = VRCAvatarDescriptor.ParameterBehaviorTargetMode.ApplyBlendSetWeight,
                        targetBlendSetName = name, weightBlendSetName = name
                    }
                });
                var logicalName = build.faceTracking.CreatePlan().sets.Single().setName;
                var control = build.ControlSet(logicalName);
                var second = build.faceTracking.RegisterImportedSet(new VRCAvatarDescriptor.FaceTrackingBlendshapesSet {
                    setName = logicalName, setWeight = 1
                }, new[] { source });
                Assert.That(second.name, Is.Not.EqualTo(logicalName));
                build.Run();
                var names = build.avatar.faceTrackingBlendshapesSetList.Select(set => set.setName).ToArray();
                Assert.That(names.All(value => value.Length <= 100), Is.True);
                Assert.That(names.Distinct().Count(), Is.EqualTo(names.Length));
                Assert.That(build.avatar.faceTrackingParameterBehaviors.Single().targetBlendSetName, Is.EqualTo(logicalName));
                Assert.That(build.avatar.faceTrackingParameterBehaviors.Single().weightBlendSetName, Is.EqualTo(logicalName));
                Assert.That(control.Read<VRCAnimatorFaceTrackingBlendshapesSet>().blendSets.Single().blendshapesSetName, Is.EqualTo(logicalName));
            }
        }

        [Test]
        public void NativeLongSetNamesStayBoundedWhenExpanded() {
            using (var build = new Build()) {
                var source = build.Skin("Source");
                var target = build.Skin("Target");
                target.sharedMesh.AddBlendShapeFrame("Second", 100, new[] { Vector3.right }, null, null);
                build.avatar.enableFaceTracking = true;
                build.avatar.useAllTrackingKeys = true;
                build.NativeMesh(source);
                var set = build.NativeSet(new string('a', 100));
                SetMapping(set, (int)VRCAvatarDescriptor.FaceTrackingKey.JawOpen, "Smile");
                var control = build.ControlSet(set.setName);
                build.Link(source, target);
                build.Link(source, target, targetName: "Second");
                build.Run();
                var names = build.avatar.faceTrackingBlendshapesSetList.Select(entry => entry.setName).ToArray();
                Assert.That(names, Has.Length.EqualTo(2));
                Assert.That(names.All(name => name.Length <= 100), Is.True);
                Assert.That(names.Distinct().Count(), Is.EqualTo(2));
                Assert.That(control.Read<VRCAnimatorFaceTrackingBlendshapesSet>().blendSets.Select(entry => entry.blendshapesSetName), Is.EquivalentTo(names));
            }
        }

        [Test]
        public void LinkingOneKeyToTwoShapesOnOneMeshExpandsTheSetAndItsControls() {
            using (var build = new Build()) {
                var source = build.Skin("Source");
                var target = build.Skin("Target");
                target.sharedMesh.AddBlendShapeFrame("Second", 100, new[] { Vector3.right }, null, null);
                build.Full(source.gameObject);
                var control = build.ControlSet("VF1_Face");
                build.Link(source, target);
                build.Link(source, target, targetName: "Second");
                build.Run();
                var sets = build.avatar.faceTrackingBlendshapesSetList;
                Assert.That(sets, Has.Count.EqualTo(2));
                var names = sets.Select(set => set.faceTrackingBlendshapeNames[(int)VRCAvatarDescriptor.FaceTrackingKey.JawOpen]).ToArray();
                Assert.That(names.All(name => target.HasBlendshape(name)), Is.True);
                Assert.That(names.Count(name => source.HasBlendshape(name)), Is.EqualTo(1));
                Assert.That(control.Read<VRCAnimatorFaceTrackingBlendshapesSet>().blendSets.Select(entry => entry.blendshapesSetName),
                    Is.EquivalentTo(sets.Select(set => set.setName)));
            }
        }

        [TestCase(false)]
        [TestCase(true)]
        public void FaceTrackingPropagatesThroughChainedLinksInEitherOrder(bool reversed) {
            using (var build = new Build()) {
                var source = build.Skin("Source");
                var middle = build.Skin("Middle");
                var target = build.Skin("Target");
                build.Full(source.gameObject);
                if (reversed) build.Link(middle, target);
                build.Link(source, middle);
                if (!reversed) build.Link(middle, target);
                build.Run();
                var name = build.avatar.faceTrackingBlendshapesSetList.Single().faceTrackingBlendshapeNames[(int)VRCAvatarDescriptor.FaceTrackingKey.JawOpen];
                foreach (var skin in new[] { source, middle, target }) {
                    Assert.That(skin.HasBlendshape(name), Is.True);
                    Assert.That(build.avatar.faceTrackingMeshes, Does.Contain(skin));
                }
            }
        }

        [TestCase(false, false, false, false)]
        [TestCase(true, false, false, false)]
        [TestCase(false, true, false, false)]
        [TestCase(true, false, true, false)]
        [TestCase(false, false, false, true)]
        public void ImportedFaceTrackingPropagatesThroughBlendshapeLink(
            bool targetInsideAsset, bool moveSource, bool independentTarget, bool advancedVisemes
        ) {
            var root = new GameObject("Avatar");
            var meshes = new List<Mesh>();
            try {
                var avatar = root.AddComponent<VRCAvatarDescriptor>();
                avatar.baseAnimationLayers = System.Array.Empty<VRCAvatarDescriptor.CustomAnimLayer>();
                avatar.specialAnimationLayers = System.Array.Empty<VRCAvatarDescriptor.CustomAnimLayer>();
                var asset = new GameObject("Added Asset");
                asset.transform.SetParent(root.transform);
                SkinnedMeshRenderer MakeSkin(string name, string shape, Transform parent) {
                    var obj = new GameObject(name);
                    obj.transform.SetParent(parent);
                    var skin = obj.AddComponent<SkinnedMeshRenderer>();
                    var mesh = new Mesh { vertices = new[] { Vector3.zero } };
                    mesh.AddBlendShapeFrame(shape, 100, new[] { Vector3.up }, null, null);
                    meshes.Add(mesh);
                    skin.sharedMesh = mesh;
                    return skin;
                }
                var source = MakeSkin("Source", "Smile", asset.transform);
                var target = MakeSkin("Target", "Smile", targetInsideAsset ? asset.transform : root.transform);
                var unrelated = MakeSkin("Unrelated", "Smile", root.transform);
                var set = new VRCAvatarDescriptor.FaceTrackingBlendshapesSet { setName = "Face", setWeight = 1 };
                SetMapping(set, 0, "Smile");
                if (independentTarget) {
                    target.sharedMesh.AddBlendShapeFrame("Independent", 100, new[] { Vector3.right }, null, null);
                    SetMapping(set, 1, "Independent");
                }
                var full = new FullControllerBuilder {
                    featureBaseObject = asset,
                    model = new FullController {
                        faceTrackingMeshes = (targetInsideAsset ? new[] { source, target } : new[] { source })
                            .Select(skin => new ComponentOrPath<SkinnedMeshRenderer> { component = skin }).ToList(),
                        faceTrackingBlendshapeSets = { set }
                    }
                };
                var link = new BlendShapeLinkBuilder {
                    model = new BlendShapeLink {
                        baseObj = "Source",
                        linkSkins = { new BlendShapeLink.LinkSkin { renderer = target } }
                    }
                };
                var globals = new GlobalsService {
                    avatarObject = root,
                    allBuildersInRun = new List<FeatureBuilder> { full, link },
                    allFeaturesInRun = new List<FeatureModel> { full.model, link.model }
                };
                var controllers = new ControllersService();
                void Inject(object obj, string field, object value) {
                    obj.GetType().VFField(field).SetValue(obj, value);
                }
                Inject(controllers, "avatar", avatar);
                var faceTracking = new FaceTrackingService();
                Inject(faceTracking, "avatar", avatar);
                Inject(faceTracking, "controllers", controllers);
                foreach (var builder in new object[] { full, link }) {
                    Inject(builder, "globals", globals);
                    Inject(builder, "avatar", avatar);
                    Inject(builder, "controllers", controllers);
                    Inject(builder, "faceTracking", faceTracking);
                }
                Inject(link, "avatarObject", (VFGameObject)root);
                Inject(full, "animatorLayerControlManager", new AnimatorLayerControlOffsetService());
                full.Apply();
                if (moveSource) source.transform.SetParent(root.transform);
                if (advancedVisemes) {
                    var model = new Visemes();
                    foreach (var field in typeof(Visemes).GetFields().Where(field => field.FieldType == typeof(State)))
                        field.SetValue(model, new State());
                    model.state_PP.actions.Add(new BlendShapeAction {
                        renderer = source, allRenderers = false, blendShape = "Smile"
                    });
                    var builder = new VisemesBuilder { model = model };
                    Inject(builder, "globals", globals);
                    Inject(builder, "avatar", avatar);
                    Inject(builder, "avatarObject", (VFGameObject)root);
                    Inject(builder, "faceTracking", faceTracking);
                    builder.Apply();
                }

                link.Apply();
                faceTracking.Apply();

                var importedSet = avatar.faceTrackingBlendshapesSetList.Single(entry => entry.setName.EndsWith("_Face"));
                var finalName = importedSet.faceTrackingBlendshapeNames[0];
                Assert.That(finalName, Is.Not.EqualTo("Smile"));
                Assert.That(source.HasBlendshape(finalName), Is.True);
                Assert.That(target.HasBlendshape(finalName), Is.True);
                Assert.That(unrelated.HasBlendshape("Smile"), Is.True);
                Assert.That(unrelated.HasBlendshape(finalName), Is.False);
                Assert.That(avatar.faceTrackingMeshes, Does.Contain(source));
                Assert.That(avatar.faceTrackingMeshes, Does.Contain(target));
                Assert.That(avatar.faceTrackingBlendshapesSetList, Has.Count.EqualTo(advancedVisemes ? 2 : 1));
                if (independentTarget)
                    Assert.That(target.HasBlendshape(importedSet.faceTrackingBlendshapeNames[1]), Is.True);
                if (advancedVisemes) {
                    var visemeSet = avatar.faceTrackingBlendshapesSetList.Single(entry => entry != importedSet);
                    Assert.That(visemeSet.visemeBlendshapeNames[1], Is.EqualTo(finalName));
                }
                meshes.Add(source.sharedMesh);
                meshes.Add(target.sharedMesh);
            } finally {
                Object.DestroyImmediate(root);
                foreach (var mesh in meshes.Distinct()) Object.DestroyImmediate(mesh);
            }
        }

        [Test]
        public void FaceTrackingWeightParameterIsRewrittenAsAnInput() {
            var raw = ScriptableObject.CreateInstance<VRCAnimatorFaceTrackingBlendshapesSet>();
            try {
                raw.blendSets = new List<FtBehaviourControl> {
                    new FtBehaviourControl {
                        type = VRC.SDKBase.VRC_AnimatorFaceTrackingBlendshapesSet.SourceType.Parameter,
                        sourceParameter = "Weight", blendshapesSetName = "Face"
                    }
                };
                var behavior = new VF.Utils.Controller.VFBehaviour(raw);
                behavior.RewriteParameters(name => "VF_" + name, includeWrites: false);
                var rewritten = behavior.Read<VRCAnimatorFaceTrackingBlendshapesSet>().blendSets.Single();
                Assert.That(rewritten.sourceParameter, Is.EqualTo("VF_Weight"));
                Assert.That(rewritten.blendshapesSetName, Is.EqualTo("Face"));
                Object.DestroyImmediate(behavior.Read<VRCAnimatorFaceTrackingBlendshapesSet>());
            } finally {
                Object.DestroyImmediate(raw);
            }
        }

        [Test]
        public void RegistrationAndRepeatedPlanningDoNotMutateOutputAssets() {
            using (var build = new Build()) {
                var native = build.Skin("Native");
                var source = build.Skin("Source");
                var target = build.Skin("Target");
                target.sharedMesh.AddBlendShapeFrame("Second", 100, new[] { Vector3.right }, null, null);
                build.avatar.enableFaceTracking = true;
                build.avatar.useAllTrackingKeys = false;
                build.NativeMesh(native);
                var originalSet = build.NativeSet("Native");
                SetMapping(originalSet, (int)VRCAvatarDescriptor.FaceTrackingKey.JawOpen, "Smile");
                var originalSets = build.avatar.faceTrackingBlendshapesSetList;
                var originalMeshes = build.avatar.faceTrackingMeshes;
                var originalBehaviors = build.avatar.faceTrackingParameterBehaviors;
                var originalJson = JsonUtility.ToJson(originalSet);
                var sourceMesh = source.sharedMesh;
                var targetMesh = target.sharedMesh;
                build.Full(source.gameObject);
                var control = build.ControlSet("VF1_Face").Read<VRCAnimatorFaceTrackingBlendshapesSet>();
                var originalControls = JsonUtility.ToJson(control);
                build.faceTracking.Link(source, "Smile", target, "Smile");
                build.faceTracking.Link(source, "Smile", target, "Second");
                var plan = build.faceTracking.CreatePlan();
                var repeated = build.faceTracking.CreatePlan();
                Assert.That(plan.sets.Select(JsonUtility.ToJson), Is.EqualTo(repeated.sets.Select(JsonUtility.ToJson)));
                Assert.That(plan.controls[control], Has.Count.EqualTo(2));
                Assert.That(plan.meshes, Is.EquivalentTo(new[] { native, source, target }));
                Assert.That(build.avatar.faceTrackingBlendshapesSetList, Is.SameAs(originalSets));
                Assert.That(originalSets, Has.Count.EqualTo(1));
                Assert.That(JsonUtility.ToJson(originalSet), Is.EqualTo(originalJson), "Clearing staged native data must not edit descriptor inputs");
                Assert.That(build.avatar.faceTrackingMeshes, Is.SameAs(originalMeshes));
                Assert.That(originalMeshes, Is.EquivalentTo(new[] { native }));
                Assert.That(build.avatar.faceTrackingParameterBehaviors, Is.SameAs(originalBehaviors));
                Assert.That(JsonUtility.ToJson(control), Is.EqualTo(originalControls));
                Assert.That(source.sharedMesh, Is.SameAs(sourceMesh));
                Assert.That(target.sharedMesh, Is.SameAs(targetMesh));
                Assert.That(source.GetBlendshapeNames(), Is.EquivalentTo(new[] { "Smile" }));
                Assert.That(target.GetBlendshapeNames(), Is.EquivalentTo(new[] { "Smile", "Second" }));
                build.faceTracking.ApplyPlan(plan);
                Assert.That(build.avatar.faceTrackingBlendshapesSetList, Has.Count.EqualTo(3));
                Assert.That(build.avatar.faceTrackingBlendshapesSetList.Single(set => set.setName == "Native")
                    .faceTrackingBlendshapeNames[(int)VRCAvatarDescriptor.FaceTrackingKey.JawOpen], Is.Empty);
                Assert.That(control.blendSets, Has.Count.EqualTo(2));
                Assert.That(plan.renames[source].Values.All(source.HasBlendshape), Is.True);
                Assert.That(plan.renames[target].Values.All(target.HasBlendshape), Is.True);
            }
        }

        [TestCase("sets")]
        [TestCase("behaviors")]
        [TestCase("controls")]
        [TestCase("meshes")]
        public void RejectedPlansDoNotPublishOrMutateOutputAssets(string limit) {
            using (var build = new Build()) {
                var source = build.Skin("Source");
                var target = build.Skin("Target");
                target.sharedMesh.AddBlendShapeFrame("Second", 100, new[] { Vector3.right }, null, null);
                if (limit == "meshes") {
                    build.avatar.enableFaceTracking = true;
                    build.NativeMesh(source);
                    for (var i = 1; i < FaceTrackingUtils.MaxFaceTrackingMeshes; i++)
                        build.NativeMesh(build.Skin("Extra" + i));
                }
                var originalSets = build.avatar.faceTrackingBlendshapesSetList;
                var originalBehaviors = build.avatar.faceTrackingParameterBehaviors;
                var originalMeshes = build.avatar.faceTrackingMeshes;
                var sourceMesh = source.sharedMesh;
                var targetMesh = target.sharedMesh;
                build.Full(source.gameObject);
                var control = build.ControlSet("VF1_Face").Read<VRCAnimatorFaceTrackingBlendshapesSet>();
                build.faceTracking.Link(source, "Smile", target, "Smile");
                build.faceTracking.Link(source, "Smile", target, "Second");
                if (limit == "sets") {
                    for (var i = 0; i < 128; i++) build.faceTracking.AddSet("Extra" + i);
                } else if (limit == "behaviors") {
                    for (var i = 0; i < 129; i++) build.faceTracking.RegisterImportedBehavior(
                        new VRCAvatarDescriptor.FaceTrackingParameterBehavior {
                            targetMode = VRCAvatarDescriptor.ParameterBehaviorTargetMode.ForwardToAnimator,
                            inputTrackingKeyNames = new[] { "Voice" }, outputParameterName = "Output" + i
                        }, new[] { source });
                } else if (limit == "controls") {
                    control.blendSets = Enumerable.Repeat(control.blendSets.Single(), 64).ToList();
                }
                var originalControls = JsonUtility.ToJson(control);
                Assert.Throws<VF.Exceptions.VRCFBuilderException>(() => build.faceTracking.CreatePlan());
                Assert.That(build.avatar.faceTrackingBlendshapesSetList, Is.SameAs(originalSets));
                Assert.That(build.avatar.faceTrackingParameterBehaviors, Is.SameAs(originalBehaviors));
                Assert.That(build.avatar.faceTrackingMeshes, Is.SameAs(originalMeshes));
                Assert.That(JsonUtility.ToJson(control), Is.EqualTo(originalControls));
                Assert.That(source.sharedMesh, Is.SameAs(sourceMesh));
                Assert.That(target.sharedMesh, Is.SameAs(targetMesh));
                Assert.That(source.GetBlendshapeNames(), Is.EquivalentTo(new[] { "Smile" }));
                Assert.That(target.GetBlendshapeNames(), Is.EquivalentTo(new[] { "Smile", "Second" }));
            }
        }

        [Test]
        public void SharedSetReferenceRewritePreservesInputsAndControlSettings() {
            var original = new FtBehaviourControl {
                blendshapesSetName = "Source", weight = 0.3f, lerpSpeed = 0.7f, sourceParameter = "Weight",
                type = VRC.SDKBase.VRC_AnimatorFaceTrackingBlendshapesSet.SourceType.Parameter
            };
            var unchanged = new FtBehaviourControl { blendshapesSetName = "Unchanged" };
            var controls = new List<FtBehaviourControl> { original, null, unchanged };
            var output = FaceTrackingUtils.RewriteSetReferences(controls, name => name == "Source" ? new[] { "Left", "Right" } : null);
            Assert.That(output.Select(control => control?.blendshapesSetName), Is.EqualTo(new[] { "Left", "Right", null, "Unchanged" }));
            Assert.That(original.blendshapesSetName, Is.EqualTo("Source"));
            foreach (var copy in output.Take(2)) {
                Assert.That(copy, Is.Not.SameAs(original));
                Assert.That(copy.weight, Is.EqualTo(original.weight));
                Assert.That(copy.lerpSpeed, Is.EqualTo(original.lerpSpeed));
                Assert.That(copy.sourceParameter, Is.EqualTo(original.sourceParameter));
                Assert.That(copy.type, Is.EqualTo(original.type));
            }
            Assert.That(output[3], Is.Not.SameAs(unchanged));
        }
    }
}
#endif
