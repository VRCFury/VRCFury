using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using JetBrains.Annotations;
using UnityEditor;
using UnityEngine;
using VF.Actions;
using VF.Builder;
using VF.Feature;
using VF.Feature.Base;
using VF.Injector;
using VF.Model;
using VF.Model.StateAction;
using VF.Utils;
using VF.Utils.Controller;
using VRC.SDK3.Avatars.Components;
using static VRC.SDK3.Avatars.ScriptableObjects.VRCExpressionsMenu.Control;
using Action = VF.Model.StateAction.Action;

namespace VF.Service {
    /** Turns VRCFury actions into clips */
    [VFService]
    [VFPrototypeScope]
    internal class ActionClipService {
        [VFAutowired] private readonly VFGameObject avatarObject;
        [VFAutowired] private readonly Func<VFGameObject> componentObject;
        [VFAutowired] [CanBeNull] private readonly AvatarManager manager;
        [VFAutowired] [CanBeNull] private readonly FullBodyEmoteService fullBodyEmoteService;
        [VFAutowired] [CanBeNull] private readonly TrackingConflictResolverBuilder trackingConflictResolverBuilder;
        [VFAutowired] [CanBeNull] private readonly PhysboneResetService physboneResetService;
        [VFAutowired] [CanBeNull] private readonly DriveOtherTypesFromFloatService driveOtherTypesFromFloatService;
        [VFAutowired] [CanBeNull] private readonly ClipFactoryService clipFactory;
        [VFAutowired] [CanBeNull] private readonly ClipBuilderService clipBuilder;
        [VFAutowired] [CanBeNull] private readonly GlobalsService globals;
        private readonly IDictionary<Type,ActionBuilder> modelTypeToBuilder;

        private readonly List<(VFAFloat,string,float)> drivenParams = new List<(VFAFloat,string,float)>();
        private readonly List<(VFAFloat,string,float)> drivenSyncParams = new List<(VFAFloat,string,float)>();
        private readonly List<(VFAFloat,string,float)> drivenToggles = new List<(VFAFloat,string,float)>();
        private readonly List<(VFAFloat,string,float,FeatureBuilder)> drivenTags = new List<(VFAFloat,string,float,FeatureBuilder)>();

        private static VFAFloat triggerParam = null; // may be used across multiple actions

        public ActionClipService(List<ActionBuilder> actionBuilders) {
            modelTypeToBuilder = actionBuilders.ToImmutableDictionary(
                builder => ReflectionUtils.GetGenericArgument(builder.GetType(), typeof(IVRCFuryBuilder<>)),
                builder => builder
            );
        }

        public enum MotionTimeMode {
            Auto,
            Never,
            Always
        }

        public AnimationClip LoadState(string name, State state, VFGameObject animObjectOverride = null, MotionTimeMode motionTime = MotionTimeMode.Never, ToggleBuilder toggleFeature = null) {
            return LoadStateAdv(name, state, animObjectOverride, motionTime, toggleFeature).onClip;
        }
        
        public class BuiltAction {
            // Don't use fx.GetEmptyClip(), since this clip may be mutated later
            public AnimationClip onClip = VrcfObjectFactory.Create<AnimationClip>();
            public AnimationClip implicitRestingClip = VrcfObjectFactory.Create<AnimationClip>();
            public bool useMotionTime = false;
        }
        
        public BuiltAction LoadStateAdv(string name, State state, VFGameObject animObjectOverride = null, MotionTimeMode motionTime = MotionTimeMode.Never, ToggleBuilder toggleFeature = null) {
            triggerParam = null; // always reset when making an animation
            var animObject = animObjectOverride ?? componentObject();

            if (state == null) {
                return new BuiltAction();
            }

            if (state.actions.Any(action => action == null)) {
                throw new Exception("Action list contains a corrupt action");
            }

            var actions = state.actions.Where(action => {
                if (action.desktopActive || action.androidActive) {
                    var isDesktop = BuildTargetUtils.IsDesktop();
                    if (!isDesktop && !action.androidActive) return false;
                    if (isDesktop && !action.desktopActive) return false;
                }
                return true;
            }).ToArray();
            if (actions.Length == 0) {
                return new BuiltAction();
            }

            var offClip = VrcfObjectFactory.Create<AnimationClip>();

            var outputClips = actions
                .Select(a => LoadAction(name, a, offClip, animObject, toggleFeature))
                .ToList();

            bool useMotionTime;
            if (motionTime == MotionTimeMode.Auto) {
                useMotionTime = outputClips.Any(clip => !clip.IsStatic());
            } else if (motionTime == MotionTimeMode.Always) {
                useMotionTime = true;
            } else {
                useMotionTime = false;
            }

            var useLoop = false;
            if (useMotionTime) {
                var finalLength = outputClips.Where(clip => !clip.IsStatic()).Select(clip => clip.GetLengthInSeconds()).DefaultIfEmpty(0).Max();
                if (finalLength == 0) finalLength = 1;
                outputClips = outputClips.Select(clip => {
                    var clipLength = clip.GetLengthInSeconds();
                    if (clip.IsStatic() && clipBuilder != null) {
                        var motionClip = clipBuilder.MergeSingleFrameClips(
                            (0, VrcfObjectFactory.Create<AnimationClip>()),
                            (finalLength, clip)
                        );
                        motionClip.UseLinearTangents();
                        motionClip.name = clip.name;
                        return motionClip;
                    } else if (clipLength == finalLength) {
                        return clip;
                    } else {
                        clip.Rewrite(AnimationRewriter.RewriteCurve((binding, curve) => {
                            return (binding, curve.ScaleTime(finalLength / clipLength), true);
                        }));
                        return clip;
                    }
                }).ToList();
            } else {
                useLoop = outputClips.Any(clip => clip.IsLooping());
            }

            AnimationClip finalClip;
            if (outputClips.Count == 1) {
                finalClip = outputClips.First();
            } else {
                finalClip = VrcfObjectFactory.Create<AnimationClip>();
                foreach (var clip in outputClips) {
                    finalClip.CopyFrom(clip);
                }
            }
            finalClip.SetLooping(useLoop);

            if (clipFactory != null) {
                finalClip.name = $"{clipFactory.GetPrefix()}/{name}";
            }

            return new BuiltAction {
                onClip = finalClip,
                implicitRestingClip = offClip,
                useMotionTime = useMotionTime
            };
        }
        
        private void AddFullBodyClip(AnimationClip clip) {
            if (fullBodyEmoteService == null) return;
            var types = clip.GetMuscleBindingTypes();
            if (types.Contains(EditorCurveBindingExtensions.MuscleBindingType.NonMuscle)) {
                types = types.Remove(EditorCurveBindingExtensions.MuscleBindingType.NonMuscle);
            }
            if (types.Contains(EditorCurveBindingExtensions.MuscleBindingType.Body)) {
                types = ImmutableHashSet.Create(EditorCurveBindingExtensions.MuscleBindingType.Body);
            }
            var copy = clip.Clone();
            foreach (var muscleType in types) {
                var trigger = fullBodyEmoteService.AddClip(copy, muscleType);
                clip.SetAap(trigger, 1);
            }
            clip.Rewrite(AnimationRewriter.RewriteBinding(b => {
                if (b.GetPropType() == EditorCurveBindingType.Muscle) return null;
                return b;
            }));
        }

        private AnimationClip LoadAction(string name, Action action, AnimationClip offClip, VFGameObject animObject, ToggleBuilder toggleFeature = null) {
            if (action == null) {
                throw new Exception("Action is corrupt");
            }

            var onClip = VrcfObjectFactory.Create<AnimationClip>();

            if (action.desktopActive || action.androidActive) {
                var isDesktop = BuildTargetUtils.IsDesktop();
                if (!isDesktop && !action.androidActive) return onClip;
                if (isDesktop && !action.desktopActive) return onClip;
            }

            if (modelTypeToBuilder.TryGetValue(action.GetType(), out var builder)) {
                var methodInjector = new VRCFuryInjector();
                methodInjector.Set(action);
                methodInjector.Set("animObject", animObject);
                methodInjector.Set("offClip", offClip);
                var buildMethod = builder.GetType().GetMethod("Build");
                return (AnimationClip)methodInjector.FillMethod(buildMethod, builder);
            }

            switch (action) {
                case ShaderInventoryAction shaderInventoryAction: {
                    var renderer = shaderInventoryAction.renderer;
                    if (renderer == null) break;
                    var propertyName = $"material._InventoryItem{shaderInventoryAction.slot:D2}Animated";
                    offClip.SetCurve(renderer, propertyName, 0);
                    onClip.SetCurve(renderer, propertyName, 1);
                    break;
                }
                case PoiyomiUVTileAction poiyomiUVTileAction: {
                    var renderer = poiyomiUVTileAction.renderer;
                    if (poiyomiUVTileAction.row > 3 || poiyomiUVTileAction.row < 0 || poiyomiUVTileAction.column > 3 || poiyomiUVTileAction.column < 0) {
                        throw new ArgumentException("Poiyomi UV Tiles are ranges between 0-3, check if slots are within these ranges.");
                    }
                    if (renderer != null) {
                        var propertyName = poiyomiUVTileAction.dissolve ? "_UVTileDissolveAlpha_Row" : "_UDIMDiscardRow";
                        propertyName += $"{poiyomiUVTileAction.row}_{(poiyomiUVTileAction.column)}";
                        if (poiyomiUVTileAction.renamedMaterial != "")
                            propertyName += $"_{poiyomiUVTileAction.renamedMaterial}";
                        offClip.SetCurve(renderer, $"material.{propertyName}", 1);
                        onClip.SetCurve(renderer, $"material.{propertyName}", 0);
                    }
                    break;
                }
                case AnimationClipAction clipAction:
                    var clipActionClip = clipAction.clip.Get();
                    if (clipActionClip == null) break;

                    var copy = clipActionClip.Clone();
                    AddFullBodyClip(copy);
                    var rewriter = AnimationRewriter.Combine(
                        ClipRewriter.CreateNearestMatchPathRewriter(
                            animObject: animObject,
                            rootObject: avatarObject
                        ),
                        ClipRewriter.AdjustRootScale(avatarObject),
                        ClipRewriter.AnimatorBindingsAlwaysTargetRoot()
                    );
                    copy.Rewrite(rewriter);
                    onClip = copy;
                    break;
                case BlendShapeAction blendShape:
                    var foundOne = false;
                    foreach (var skin in avatarObject.GetComponentsInSelfAndChildren<SkinnedMeshRenderer>()) {
                        if (!blendShape.allRenderers && blendShape.renderer != skin) continue;
                        if (!skin.HasBlendshape(blendShape.blendShape)) continue;
                        foundOne = true;
                        onClip.SetCurve(skin, "blendShape." + blendShape.blendShape, blendShape.blendShapeValue);
                    }
                    if (!foundOne) {
                        //Debug.LogWarning("BlendShape not found: " + blendShape.blendShape);
                    }
                    break;
                case ScaleAction scaleAction:
                    if (scaleAction.obj == null) {
                        //Debug.LogWarning("Missing object in action: " + name);
                    } else {
                        var localScale = scaleAction.obj.transform.localScale;
                        var newScale = localScale * scaleAction.scale;
                        offClip.SetScale(scaleAction.obj, localScale);
                        onClip.SetScale(scaleAction.obj, newScale);
                    }
                    break;
                case MaterialAction matAction: {
                    var renderer = matAction.renderer;
                    if (renderer == null) break;
                    var mat = matAction.mat?.Get();
                    if (mat == null) break;

                    var propertyName = "m_Materials.Array.data[" + matAction.materialIndex + "]";
                    onClip.SetCurve(renderer, propertyName, mat);
                    break;
                }
                case SpsOnAction spsAction: {
                    if (spsAction.target == null) {
                        //Debug.LogWarning("Missing target in action: " + name);
                        break;
                    }
                    offClip.SetCurve(spsAction.target, "spsAnimatedEnabled", 0);
                    onClip.SetCurve(spsAction.target, "spsAnimatedEnabled", 1);
                    break;
                }
                case FxFloatAction fxFloatAction: {
                    if (string.IsNullOrWhiteSpace(fxFloatAction.name)) {
                        break;
                    }

                    if (FullControllerBuilder.VRChatGlobalParams.Contains(fxFloatAction.name)) {
                        throw new Exception("Set an FX Float cannot set built-in vrchat parameters");
                    }

                    if (manager != null && driveOtherTypesFromFloatService != null) {
                        var myFloat = manager.GetFx().NewFloat("vrcfParamDriver");
                        onClip.SetAap(myFloat, fxFloatAction.value);
                        driveOtherTypesFromFloatService.DriveAutoLater(myFloat, fxFloatAction.name, fxFloatAction.value);
                    }
                    break;
                }
                case BlockBlinkingAction blockBlinkingAction: {
                    if (trackingConflictResolverBuilder == null) break;
                    var blockTracking = trackingConflictResolverBuilder.AddInhibitor(name, TrackingConflictResolverBuilder.TrackingEyes);
                    onClip.SetAap(blockTracking, 1);
                    break;
                }
                case BlockVisemesAction blockVisemesAction: {
                    if (trackingConflictResolverBuilder == null) break;
                    var blockTracking = trackingConflictResolverBuilder.AddInhibitor(name, TrackingConflictResolverBuilder.TrackingMouth);
                    onClip.SetAap(blockTracking, 1);
                    break;
                }
                case ResetPhysboneAction resetPhysbone: {
                    if (resetPhysbone.physBone != null && physboneResetService != null) {
                        var param = physboneResetService.CreatePhysBoneResetter(new [] { resetPhysbone.physBone.owner() }, name);
                        onClip.SetAap(param, 1);
                    }
                    break;
                }
                case FlipBookBuilderAction sliderBuilderAction: {
                    var states = sliderBuilderAction.pages.Select(page => page.state).ToList();
                    if (states.Count == 0) break;
                    // Duplicate the last state so the last state still gets an entire frame
                    states.Add(states.Last());
                    var sources = states
                        .Select((substate,i) => {
                            var loaded = LoadStateAdv("tmp", substate, animObject);
                            return ((float)i, loaded.onClip);
                        })
                        .ToArray();

                    if (clipBuilder != null) {
                        var built = clipBuilder.MergeSingleFrameClips(sources);
                        built.UseConstantTangents();
                        onClip.CopyFrom(built);
                    } else {
                        // This is wrong, but it's fine because this branch is for debug info only
                        foreach (var source in sources) {
                            onClip.CopyFrom(source.onClip);
                        }
                    }
                    
                    break;
                }
                case SmoothLoopAction smoothLoopAction: {
                    var clip1 = LoadStateAdv("tmp", smoothLoopAction.state1, animObject);
                    var clip2 = LoadStateAdv("tmp", smoothLoopAction.state2, animObject);

                    if (clipBuilder != null) {
                        var built = clipBuilder.MergeSingleFrameClips(
                            (0, clip1.onClip),
                            (smoothLoopAction.loopTime / 2, clip2.onClip),
                            (smoothLoopAction.loopTime, clip1.onClip)
                        );
                        onClip.CopyFrom(built);
                    } else {
                        // This is wrong, but it's fine because this branch is for debug info only
                        onClip.CopyFrom(clip1.onClip);
                        onClip.CopyFrom(clip2.onClip);
                    }

                    onClip.SetLooping(true);
                    
                    break;
                }
                case SyncParamAction syncParamAction: {
                    if (triggerParam == null) {
                        triggerParam = manager.GetFx().NewFloat(name + " (Param Trigger)");
                        onClip.SetCurve(EditorCurveBinding.FloatCurve("", typeof(Animator), triggerParam.Name()), 1);
                    }
                    drivenSyncParams.Add((triggerParam, syncParamAction.param, syncParamAction.value));
                    break;
                }
                case ToggleStateAction toggleStateAction: {
                    if (triggerParam == null) {
                    triggerParam = manager.GetFx().NewFloat(name + " (Param Trigger)");
                    onClip.SetCurve(EditorCurveBinding.FloatCurve("", typeof(Animator), triggerParam.Name()), 1);
                    }
                    drivenToggles.Add((triggerParam, toggleStateAction.toggle, toggleStateAction.value));
                    break;
                }
                case TagStateAction tagStateAction: {
                    if (triggerParam == null) {
                        triggerParam = manager.GetFx().NewFloat(name + " (Param Trigger)");
                        onClip.SetCurve(EditorCurveBinding.FloatCurve("", typeof(Animator), triggerParam.Name()), 1);
                    }
                    drivenTags.Add((triggerParam, tagStateAction.tag, tagStateAction.value, toggleFeature));
                    break;
                }
                default: {
                    throw new Exception($"Unknown action type {action.GetType().Name}");
                }
            }

            return onClip;
        }

        [FeatureBuilderAction(FeatureOrder.DriveNonFloatTypes)]
        public void DriveNonFloatTypes() {
            var nonFloatParams = new HashSet<string>();
            foreach (var c in manager.GetAllUsedControllers()) {
                nonFloatParams.UnionWith(c.GetRaw().parameters
                    .Where(p => p.type != AnimatorControllerParameterType.Float || c.GetType() != VRCAvatarDescriptor.AnimLayerType.FX)
                    .Select(p => p.name));
            }

            List<(VFAFloat, string, float)> triggers = new();
            foreach (var trigger in drivenTags) {
                var (param, tag, target, feature) = trigger;
                foreach (var other in globals.allBuildersInRun
                     .OfType<ToggleBuilder>()
                     .Where(b => b != feature)) {
                        var otherTags = other.GetTags();
                        
                        if (otherTags.Contains(tag)) {
                            if (target == 0) triggers.Add((param, other.getParam(), 0));
                            else triggers.Add((param, other.getParam(), other.model.slider ? target : 1));
                        }
                }
            }

            foreach (var trigger in drivenToggles) {
                var (param, path, target) = trigger;
                var control = manager.GetMenu().GetMenuItem(path);
                if (control == null) continue;
                if (target == 0) triggers.Add((param, control.parameter.name, 0));
                else if (control.type == ControlType.RadialPuppet) triggers.Add((param, control.parameter.name, target));
                else triggers.Add((param, control.parameter.name, control.value));
            }

            foreach (var trigger in drivenSyncParams) {
                var (triggerParam, param, target) = trigger;
                triggers.Add((triggerParam, param, target));
            }

            foreach (var trigger in triggers) {
                var (triggerParam, param, value) = trigger;
                driveOtherTypesFromFloatService.Drive(triggerParam, param, value, false);
            }

            var rewrites = new Dictionary<string, string>();
            foreach (var (floatParam,targetParam,onValue) in drivenParams) {
                if (nonFloatParams.Contains(targetParam)) {
                    driveOtherTypesFromFloatService.Drive(floatParam, targetParam, onValue);
                } else {
                    rewrites.Add(floatParam, targetParam);
                }
            }
        }
        
        public static IList<Renderer> FindRenderers(
            bool allRenderers,
            Renderer singleRenderer,
            VFGameObject avatarObject
        ) {
            IList<Renderer> renderers;
            if (allRenderers) {
                renderers = avatarObject.GetComponentsInSelfAndChildren<Renderer>();
            } else {
                renderers = new[] { singleRenderer };
            }
            renderers = renderers.NotNull().ToArray();
            return renderers;
        }

        public static ShaderUtil.ShaderPropertyType? FindMaterialPropertyType(
            IList<Renderer> renderers,
            string propName
        ) {
            return renderers
                .Select(r => r.GetPropertyType(propName))
                .NotNull()
                .DefaultIfEmpty(null)
                .First();
        }

        public static MaterialPropertyAction.Type GetMaterialPropertyActionTypeToUse(
            IList<Renderer> renderers,
            string propName,
            MaterialPropertyAction.Type setting,
            bool forceRedetect
        ) {
            if (!forceRedetect && setting != MaterialPropertyAction.Type.LegacyAuto) {
                return setting;
            }
            switch (FindMaterialPropertyType(renderers, propName)) {
                case ShaderUtil.ShaderPropertyType.Color:
                    return MaterialPropertyAction.Type.Color;
                case ShaderUtil.ShaderPropertyType.Vector:
                    return MaterialPropertyAction.Type.Vector;
                case MaterialExtensions.StPropertyType:
                    return MaterialPropertyAction.Type.St;
                case null:
                    return setting == MaterialPropertyAction.Type.LegacyAuto
                        ? MaterialPropertyAction.Type.Float
                        : setting;
                default:
                    return MaterialPropertyAction.Type.Float;
            }
        }
    }
}
