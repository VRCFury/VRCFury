#if VRCSDK_HAS_FACE_TRACKING
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using VF.Feature;
using VF.Builder;
using VF.Feature.Base;
using VF.Inspector;
using VF.Model;
using VF.Model.Feature;
using VF.Model.StateAction;
using VF.Service;
using VF.Utils;
using VF.Utils.Controller;
using VRC.SDK3.Avatars.Components;
using FtBehaviourControl = VRC.SDKBase.VRC_AnimatorFaceTrackingBlendshapesSet.BlendSet;
using Toggle = VF.Model.Feature.Toggle;

namespace VF.Tests {
    internal abstract class FaceTrackingTestBase {
        protected sealed class AuthoringFields : ScriptableObject {
            public List<ObjectReference<SkinnedMeshRenderer>> meshes = new List<ObjectReference<SkinnedMeshRenderer>>();
            public VRCAvatarDescriptor.FaceTrackingBlendshapesSet set = new VRCAvatarDescriptor.FaceTrackingBlendshapesSet();
            public VRCAvatarDescriptor.FaceTrackingParameterBehavior behavior = new VRCAvatarDescriptor.FaceTrackingParameterBehavior();
            public FullController controller = new();
        }

        protected static void SetMapping(VRCAvatarDescriptor.FaceTrackingBlendshapesSet set, int index, string name, bool visemes = false, float multiplier = 1) {
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

        protected sealed class Build : System.IDisposable {
            public readonly GameObject root = new GameObject("Avatar");
            public readonly VRCAvatarDescriptor avatar;
            public readonly GlobalsService globals;
            public readonly FaceTrackingService faceTracking = new FaceTrackingService();
            private readonly ControllersService controllers = new ControllersService();
            private readonly UniqueParamService uniqueParams = new UniqueParamService();
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
                var parameters = new ExpressionParamsService();
                Inject(parameters, "avatar", avatar);
                Inject(parameters, "globals", globals);
                Inject(controllers, "expressionParams", parameters);
                Inject(controllers, "uniqueParams", new System.Lazy<UniqueParamService>(() => uniqueParams));
                Inject(uniqueParams, "controllers", controllers);
                Inject(uniqueParams, "expressionParams", parameters);
                Inject(uniqueParams, "avatarObject", (VFGameObject)root);
                Inject(uniqueParams, "pipelineId", new PipelineIdService(root));
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
                bool includeSet = true,
                VRCAvatarDescriptor.VisemeDampenMode dampenMode = VRCAvatarDescriptor.VisemeDampenMode.None,
                float dampenAmount = 1,
                params VRCAvatarDescriptor.FaceTrackingParameterBehavior[] behaviors) {
                var set = new VRCAvatarDescriptor.FaceTrackingBlendshapesSet { setName = setName, setWeight = setWeight };
                if (visemes) SetMapping(set, 1, blendshape, visemes: true);
                else SetMapping(set, (int)key, blendshape);
                var builder = new FullControllerBuilder {
                    featureBaseObject = owner,
                    model = new FullController {
                        faceTrackingMeshes = path != null
                            ? new List<ObjectReference<SkinnedMeshRenderer>> { new ObjectReference<SkinnedMeshRenderer> { useBone = false, offset = path } }
                            : (skins ?? new[] { owner.GetComponent<SkinnedMeshRenderer>() })
                                .Select(skin => (ObjectReference<SkinnedMeshRenderer>)skin).ToList(),
                        faceTrackingBlendshapeSets = includeSet ? new() { set } : new(),
                        faceTrackingParameterBehaviors = behaviors.ToList(),
                        visemeDampenMode = dampenMode,
                        visemeDampenAmount = dampenAmount
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
                var controller = new VFAvatarController(
                    VF.Utils.Controller.VFController.Create(),
                    VRCAvatarDescriptor.AnimLayerType.FX,
                    () => 0,
                    uniqueParams.GetInternalName,
                    layerSource
                );
                var cache = (Dictionary<VRCAvatarDescriptor.AnimLayerType, VFAvatarController>)typeof(ControllersService)
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
                if (builder is FullControllerBuilder) Inject(builder, "uniqueParams", uniqueParams);
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

            public RemoveLegacyFaceTrackingService RemoveBuiltIns() {
                var service = new RemoveLegacyFaceTrackingService();
                Inject(service, "globals", globals);
                Inject(service, "avatar", avatar);
                service.Apply();
                return service;
            }

            public void Dispose() {
                meshes.AddRange(root.GetComponentsInChildren<SkinnedMeshRenderer>(true).Select(skin => skin.sharedMesh));
                Object.DestroyImmediate(root);
                foreach (var mesh in meshes.Distinct()) Object.DestroyImmediate(mesh);
            }
        }

        protected static Visemes NewVisemes() {
            var model = new Visemes();
            foreach (var field in typeof(Visemes).GetFields().Where(field => field.FieldType == typeof(State)))
                field.SetValue(model, new State());
            return model;
        }
    }
}
#endif
