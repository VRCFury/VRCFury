using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using JetBrains.Annotations;
using UnityEditor;
using UnityEngine;
using VF.Builder;
using VF.Component;
using VF.Feature;
using VF.Feature.Base;
using VF.Injector;
using VF.Model;
using VF.Model.StateAction;
using VF.Utils;
using VF.Utils.Controller;
using VRC.SDK3.Avatars.Components;
using static VRC.SDK3.Avatars.ScriptableObjects.VRCExpressionsMenu.Control;

namespace VF.Service {
    /** Turns VRCFury actions into clips */
    [VFService]
    [VFPrototypeScope]
    internal class ActionClipService {
        [VFAutowired] private readonly AvatarManager manager;
        [VFAutowired] private readonly AvatarManager avatarManager;
        [VFAutowired] private readonly ClipBuilderService clipBuilder;
        [VFAutowired] private readonly FullBodyEmoteService fullBodyEmoteService;
        [VFAutowired] private readonly TrackingConflictResolverBuilder trackingConflictResolverBuilder;
        [VFAutowired] private readonly PhysboneResetService physboneResetService;
        [VFAutowired] private readonly DriveOtherTypesFromFloatService driveOtherTypesFromFloatService;
        [VFAutowired] private readonly ClipFactoryService clipFactory;
        [VFAutowired] private readonly GlobalsService globals;

        private readonly List<(VFAFloat,string,float)> drivenParams = new List<(VFAFloat,string,float)>();
        private readonly List<(VFAFloat,string,float)> drivenSyncParams = new List<(VFAFloat,string,float)>();
        private readonly List<(VFAFloat,string,float)> drivenToggles = new List<(VFAFloat,string,float)>();
        private readonly List<(VFAFloat,string,float,FeatureBuilder)> drivenTags = new List<(VFAFloat,string,float,FeatureBuilder)>();

        public AnimationClip LoadState(string name, State state, VFGameObject animObjectOverride = null, ToggleBuilder toggleFeature = null) {
            return LoadStateAdv(name, state, animObjectOverride, toggleFeature: toggleFeature).onClip;
        }
        

        public BuiltAction LoadStateAdv(string name, State state, VFGameObject animObjectOverride = null, ToggleBuilder toggleFeature = null) {
            var result = LoadStateAdv(name, state, avatarManager.AvatarObject, animObjectOverride ?? avatarManager.CurrentComponentObject, this, toggleFeature);
            result.onClip.name = $"{clipFactory.GetPrefix()}/{name}";
            return result;
        }

        public class BuiltAction {
            // Don't use fx.GetEmptyClip(), since this clip may be mutated later
            public AnimationClip onClip = VrcfObjectFactory.Create<AnimationClip>();
            public AnimationClip implicitRestingClip = VrcfObjectFactory.Create<AnimationClip>();
        }

        public static BuiltAction LoadStateAdv(string name, State state, VFGameObject avatarObject, VFGameObject animObject, [CanBeNull] ActionClipService service = null, ToggleBuilder toggleFeature = null) {

            if (state == null) {
                return new BuiltAction();
            }

            if (state.actions.Any(action => action == null)) {
                throw new Exception("Action list contains a corrupt action");
            }

            var actions = state.actions.Where(action => {
                if (action.desktopActive || action.androidActive) {
                    var isAndroid = EditorUserBuildSettings.activeBuildTarget == BuildTarget.Android;
                    if (isAndroid && !action.androidActive) return false;
                    if (!isAndroid && !action.desktopActive) return false;
                }
                return true;
            }).ToArray();
            if (actions.Length == 0) {
                return new BuiltAction();
            }

            var rewriter = AnimationRewriter.Combine(
                ClipRewriter.CreateNearestMatchPathRewriter(
                    animObject: animObject,
                    rootObject: avatarObject
                ),
                ClipRewriter.AdjustRootScale(avatarObject),
                ClipRewriter.AnimatorBindingsAlwaysTargetRoot()
            );

            var offClip = VrcfObjectFactory.Create<AnimationClip>();
            var onClip = VrcfObjectFactory.Create<AnimationClip>();
            
            void AddFullBodyClip(AnimationClip clip) {
                if (service == null) return;
                var types = clip.GetMuscleBindingTypes();
                if (types.Contains(EditorCurveBindingExtensions.MuscleBindingType.NonMuscle)) {
                    types = types.Remove(EditorCurveBindingExtensions.MuscleBindingType.NonMuscle);
                }
                if (types.Contains(EditorCurveBindingExtensions.MuscleBindingType.Body)) {
                    types = ImmutableHashSet.Create(EditorCurveBindingExtensions.MuscleBindingType.Body);
                }
                var copy = clip.Clone();
                foreach (var muscleType in types) {
                    var trigger = service.fullBodyEmoteService.AddClip(copy, muscleType);
                    clip.SetAap(trigger, 1);
                }
                clip.Rewrite(AnimationRewriter.RewriteBinding(b => {
                    if (b.GetPropType() == EditorCurveBindingType.Muscle) return null;
                    return b;
                }));
            }

            var firstClip = actions
                .OfType<AnimationClipAction>()
                .Select(action => action.clip.Get())
                .NotNull()
                .FirstOrDefault();
            if (firstClip) {
                var copy = firstClip.Clone();
                AddFullBodyClip(copy);
                copy.Rewrite(rewriter);
                copy.name = onClip.name;
                onClip = copy;
            }

            var physbonesToReset = new HashSet<VFGameObject>();

            VFAFloat triggerParam = null;

            foreach (var action in actions) {
                switch (action) {
                    case FlipbookAction flipbook: {
                        var renderer = flipbook.renderer;
                        if (renderer == null) break;

                        // If we animate the frame to a flat number, unity can internally do some weird tweening
                        // which can result in it being just UNDER our target, (say 0.999 instead of 1), resulting
                        // in unity displaying frame 0 instead of 1. Instead, we target framenum+0.5, so there's
                        // leniency around it.
                        var frameAnimNum = (float)(Math.Floor((double)flipbook.frame) + 0.5);
                        var binding = EditorCurveBinding.FloatCurve(
                            renderer.owner().GetPath(avatarObject),
                            renderer.GetType(),
                            "material._FlipbookCurrentFrame"
                        );
                        onClip.SetCurve(binding, frameAnimNum);
                        break;
                    }
                    case ShaderInventoryAction shaderInventoryAction: {
                        var renderer = shaderInventoryAction.renderer;
                        if (renderer == null) break;
                        var binding = EditorCurveBinding.FloatCurve(
                            renderer.owner().GetPath(avatarObject),
                            renderer.GetType(),
                            $"material._InventoryItem{shaderInventoryAction.slot:D2}Animated"
                        );
                        offClip.SetCurve(binding, 0);
                        onClip.SetCurve(binding, 1);
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
                            var binding = EditorCurveBinding.FloatCurve(
                                renderer.owner().GetPath(avatarObject),
                                renderer.GetType(),
                                $"material.{propertyName}"
                            );
                            offClip.SetCurve(binding, 1f);
                            onClip.SetCurve(binding, 0f);
                        }
                        break;
                    }
                    case MaterialPropertyAction materialPropertyAction: {
                        var (renderers,type) = MatPropLookup(
                            materialPropertyAction.affectAllMeshes,
                            materialPropertyAction.renderer,
                            avatarObject,
                            materialPropertyAction.propertyName
                        );

                        foreach (var renderer in renderers) {
                            void AddOne(string suffix, float value) {
                                var binding = EditorCurveBinding.FloatCurve(
                                    renderer.owner().GetPath(avatarObject),
                                    renderer.GetType(),
                                    $"material.{materialPropertyAction.propertyName}{suffix}"
                                );
                                onClip.SetCurve(binding, value);
                            }

                            if (type == ShaderUtil.ShaderPropertyType.Float || type == ShaderUtil.ShaderPropertyType.Range) {
                                AddOne("", materialPropertyAction.value);
                            } else if (type == ShaderUtil.ShaderPropertyType.Color) {
                                AddOne(".r", materialPropertyAction.valueColor.r);
                                AddOne(".g", materialPropertyAction.valueColor.g);
                                AddOne(".b", materialPropertyAction.valueColor.b);
                                AddOne(".a", materialPropertyAction.valueColor.a);
                            } else if (type == ShaderUtil.ShaderPropertyType.Vector) {
                                AddOne(".x", materialPropertyAction.valueVector.x);
                                AddOne(".y", materialPropertyAction.valueVector.y);
                                AddOne(".z", materialPropertyAction.valueVector.z);
                                AddOne(".w", materialPropertyAction.valueVector.w);
                            }
                        }
                        break;
                    }
                    case AnimationClipAction clipAction:
                        var clipActionClip = clipAction.clip.Get();
                        if (clipActionClip == null || clipActionClip == firstClip) break;

                        var copy = clipActionClip.Clone();
                        AddFullBodyClip(copy);
                        copy.Rewrite(rewriter);
                        onClip.CopyFrom(copy);
                        break;
                    case ObjectToggleAction toggle: {
                        if (toggle.obj == null) {
                            Debug.LogWarning("Missing object in action: " + name);
                            break;
                        }

                        var onState = true;
                        if (toggle.mode == ObjectToggleAction.Mode.TurnOff) {
                            onState = false;
                        } else if (toggle.mode == ObjectToggleAction.Mode.Toggle) {
                            onState = !toggle.obj.activeSelf;
                        }

                        ClipBuilderService.Enable(offClip, toggle.obj.asVf().GetPath(avatarObject), !onState);
                        ClipBuilderService.Enable(onClip, toggle.obj.asVf().GetPath(avatarObject), onState);
                        break;
                    }
                    case BlendShapeAction blendShape:
                        var foundOne = false;
                        foreach (var skin in avatarObject.GetComponentsInSelfAndChildren<SkinnedMeshRenderer>()) {
                            if (!blendShape.allRenderers && blendShape.renderer != skin) continue;
                            if (!skin.HasBlendshape(blendShape.blendShape)) continue;
                            foundOne = true;
                            //var defValue = skin.GetBlendShapeWeight(blendShapeIndex);
                            var binding = EditorCurveBinding.FloatCurve(
                                skin.owner().GetPath(avatarObject),
                                typeof(SkinnedMeshRenderer),
                                "blendShape." + blendShape.blendShape
                            );
                            onClip.SetCurve(binding, blendShape.blendShapeValue);
                        }
                        if (!foundOne) {
                            Debug.LogWarning("BlendShape not found: " + blendShape.blendShape);
                        }
                        break;
                    case ScaleAction scaleAction:
                        if (scaleAction.obj == null) {
                            Debug.LogWarning("Missing object in action: " + name);
                        } else {
                            var localScale = scaleAction.obj.transform.localScale;
                            var newScale = localScale * scaleAction.scale;
                            ClipBuilderService.Scale(offClip, scaleAction.obj.asVf().GetPath(avatarObject), localScale);
                            ClipBuilderService.Scale(onClip, scaleAction.obj.asVf().GetPath(avatarObject), newScale);
                        }
                        break;
                    case MaterialAction matAction: {
                        var renderer = matAction.renderer;
                        if (renderer == null) break;
                        var mat = matAction.mat?.Get();
                        if (mat == null) break;
                        
                        var binding = EditorCurveBinding.PPtrCurve(
                            renderer.owner().GetPath(avatarObject),
                            renderer.GetType(),
                            "m_Materials.Array.data[" + matAction.materialIndex + "]"
                        );
                        onClip.SetCurve(binding, mat);
                        break;
                    }
                    case SpsOnAction spsAction: {
                        if (spsAction.target == null) {
                            Debug.LogWarning("Missing target in action: " + name);
                            break;
                        }

                        var binding = EditorCurveBinding.FloatCurve(
                            spsAction.target.gameObject.asVf().GetPath(avatarObject),
                            typeof(VRCFuryHapticPlug),
                            "spsAnimatedEnabled"
                        );
                        offClip.SetCurve(binding, 0);
                        onClip.SetCurve(binding, 1);
                        break;
                    }
                    case FxFloatAction fxFloatAction: {
                        if (string.IsNullOrWhiteSpace(fxFloatAction.name)) {
                            break;
                        }

                        if (FullControllerBuilder.VRChatGlobalParams.Contains(fxFloatAction.name)) {
                            throw new Exception("Set an FX Float cannot set built-in vrchat parameters");
                        }

                        if (service == null) break;

                        var myFloat = service.manager.GetFx().NewFloat("vrcfParamDriver");
                        onClip.SetAap(myFloat, fxFloatAction.value);
                        service.drivenParams.Add((myFloat, fxFloatAction.name, fxFloatAction.value));
                        break;
                    }
                    case BlockBlinkingAction blockBlinkingAction: {
                        if (service == null) break;
                        var blockTracking = service.trackingConflictResolverBuilder.AddInhibitor(name, TrackingConflictResolverBuilder.TrackingEyes);
                        onClip.SetAap(blockTracking, 1);
                        break;
                    }
                    case BlockVisemesAction blockVisemesAction: {
                        if (service == null) break;
                        var blockTracking = service.trackingConflictResolverBuilder.AddInhibitor(name, TrackingConflictResolverBuilder.TrackingMouth);
                        onClip.SetAap(blockTracking, 1);
                        break;
                    }
                    case ResetPhysboneAction resetPhysbone: {
                        if (resetPhysbone.physBone != null) {
                            physbonesToReset.Add(resetPhysbone.physBone.owner());
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
                                var loaded = LoadStateAdv("tmp", substate, avatarObject, animObject, service);
                                return ((float)i, loaded.onClip);
                            })
                            .ToArray();

                        if (service != null) {
                            var built = service.clipBuilder.MergeSingleFrameClips(sources);
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
                        var clip1 = LoadStateAdv("tmp", smoothLoopAction.state1, avatarObject, animObject, service);
                        var clip2 = LoadStateAdv("tmp", smoothLoopAction.state2, avatarObject, animObject, service);

                        if (service != null) {
                            var built = service.clipBuilder.MergeSingleFrameClips(
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
                        if (service == null) break;
                        if (triggerParam == null) {
                            triggerParam = service.manager.GetFx().NewFloat(name + " (Param Trigger)");
                            onClip.SetCurve(EditorCurveBinding.FloatCurve("", typeof(Animator), triggerParam.Name()), 1);
                        }
                        service.drivenSyncParams.Add((triggerParam, syncParamAction.param, syncParamAction.value));
                        break;
                    }
                    case ToggleStateAction toggleStateAction: {
                         if (service == null) break;
                         if (triggerParam == null) {
                            triggerParam = service.manager.GetFx().NewFloat(name + " (Param Trigger)");
                            onClip.SetCurve(EditorCurveBinding.FloatCurve("", typeof(Animator), triggerParam.Name()), 1);
                        }
                        service.drivenToggles.Add((triggerParam, toggleStateAction.toggle, toggleStateAction.value));
                        break;
                    }
                    case TagStateAction tagStateAction: {
                        if (service == null) break;
                        if (triggerParam == null) {
                            triggerParam = service.manager.GetFx().NewFloat(name + " (Param Trigger)");
                            onClip.SetCurve(EditorCurveBinding.FloatCurve("", typeof(Animator), triggerParam.Name()), 1);
                        }
                        service.drivenTags.Add((triggerParam, tagStateAction.tag, tagStateAction.value, toggleFeature));
                        break;
                    }
                }
            }

            if (physbonesToReset.Count > 0 && service != null) {
                var param = service.physboneResetService.CreatePhysBoneResetter(physbonesToReset, name);
                onClip.SetAap(param, 1);
            }

            return new BuiltAction() {
                onClip = onClip,
                implicitRestingClip = offClip
            };
        }

        public static (IList<Renderer>, ShaderUtil.ShaderPropertyType type) MatPropLookup(
            bool allRenderers,
            Renderer singleRenderer,
            VFGameObject avatarObject,
            [CanBeNull] string propName
        ) {
            IList<Renderer> renderers;
            if (allRenderers) {
                renderers = avatarObject.GetComponentsInSelfAndChildren<Renderer>();
            } else {
                renderers = new[] { singleRenderer };
            }
            renderers = renderers.NotNull().ToArray();
            if (propName == null) {
                return (renderers, ShaderUtil.ShaderPropertyType.Float);
            }

            var type = renderers
                .Select(r => r.GetPropertyType(propName))
                .Where(t => t.HasValue)
                .Select(t => t.Value)
                .DefaultIfEmpty(ShaderUtil.ShaderPropertyType.Float)
                .First();
            return (renderers, type);
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

            if (rewrites.Count > 0) {
                foreach (var c in manager.GetAllUsedControllers()) {
                    c.GetRaw().RewriteParameters(from =>
                        rewrites.TryGetValue(from, out var to) ? to : from
                    );
                }
            }
        }
    }
}
