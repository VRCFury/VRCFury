using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using JetBrains.Annotations;
using UnityEditor;
using UnityEditor.Animations;
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

namespace VF.Service {
    /** Turns VRCFury actions into clips */
    [VFService]
    public class ActionClipService {
        [VFAutowired] private readonly AvatarManager manager;
        [VFAutowired] private readonly MutableManager mutableManager;
        [VFAutowired] private readonly RestingStateBuilder restingState;
        [VFAutowired] private readonly AvatarManager avatarManager;
        [VFAutowired] private readonly ClipBuilderService clipBuilder;
        [VFAutowired] private readonly FullBodyEmoteService fullBodyEmoteService;
        [VFAutowired] private readonly TrackingConflictResolverBuilder trackingConflictResolverBuilder;
        [VFAutowired] private readonly PhysboneResetService physboneResetService;
        [VFAutowired] private readonly DriveOtherTypesFromFloatService driveOtherTypesFromFloatService;

        private readonly List<(VFAFloat,string,float)> drivenParams = new List<(VFAFloat,string,float)>();

        public AnimationClip LoadState(string name, State state, VFGameObject animObjectOverride = null, bool applyOffClip = true) {
            var fx = avatarManager.GetFx();
            var avatarObject = avatarManager.AvatarObject;

            if (state == null) {
                // Don't use fx.GetEmptyClip(), since this clip may be mutated later
                return new AnimationClip();
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
                return new AnimationClip();
            }

            var rewriter = AnimationRewriter.Combine(
                ClipRewriter.CreateNearestMatchPathRewriter(
                    animObject: animObjectOverride ?? avatarManager.CurrentComponentObject,
                    rootObject: avatarObject
                ),
                ClipRewriter.AdjustRootScale(avatarObject),
                ClipRewriter.AnimatorBindingsAlwaysTargetRoot()
            );

            var offClip = new AnimationClip();
            var onClip = fx.NewClip(name);

            var firstClip = actions
                .OfType<AnimationClipAction>()
                .Select(action => action.clip.Get())
                .FirstOrDefault(clip => clip != null);
            if (firstClip) {
                var copy = MutableManager.CopyRecursive(firstClip);
                copy.Rewrite(rewriter);
                var nameBak = onClip.name;
                EditorUtility.CopySerialized(copy, onClip);
                onClip.name = nameBak;
            }

            var physbonesToReset = new HashSet<VFGameObject>();

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
                            clipBuilder.GetPath(renderer.owner()),
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
                            clipBuilder.GetPath(renderer.owner()),
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
                                clipBuilder.GetPath(renderer.owner()),
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
                                    clipBuilder.GetPath(renderer.owner()),
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
                        if (clipActionClip && clipActionClip != firstClip) {
                            var copy = MutableManager.CopyRecursive(clipActionClip);
                            copy.Rewrite(rewriter);
                            onClip.CopyFrom(copy);
                        }
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

                        clipBuilder.Enable(offClip, toggle.obj, !onState);
                        clipBuilder.Enable(onClip, toggle.obj, onState);
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
                                clipBuilder.GetPath(skin.owner()),
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
                            clipBuilder.Scale(offClip, scaleAction.obj, localScale);
                            clipBuilder.Scale(onClip, scaleAction.obj, newScale);
                        }
                        break;
                    case MaterialAction matAction: {
                        var renderer = matAction.renderer;
                        if (renderer == null) break;
                        var mat = matAction.mat?.Get();
                        if (mat == null) break;
                        
                        var binding = EditorCurveBinding.PPtrCurve(
                            clipBuilder.GetPath(renderer.owner()),
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
                            clipBuilder.GetPath(spsAction.target.gameObject),
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

                        var myFloat = fx.NewFloat(name + "_paramDriver");
                        var binding = EditorCurveBinding.FloatCurve(
                            "",
                            typeof(Animator),
                            myFloat.Name()
                        );
                        onClip.SetCurve(binding, fxFloatAction.value);
                        drivenParams.Add((myFloat, fxFloatAction.name, fxFloatAction.value));
                        break;
                    }
                    case BlockBlinkingAction blockBlinkingAction: {
                        var blockTracking = trackingConflictResolverBuilder.AddInhibitor(name, TrackingConflictResolverBuilder.TrackingEyes);
                        onClip.SetCurve(EditorCurveBinding.FloatCurve("", typeof(Animator), blockTracking.Name()), 1);
                        break;
                    }
                    case BlockVisemesAction blockVisemesAction: {
                        var blockTracking = trackingConflictResolverBuilder.AddInhibitor(name, TrackingConflictResolverBuilder.TrackingMouth);
                        onClip.SetCurve(EditorCurveBinding.FloatCurve("", typeof(Animator), blockTracking.Name()), 1);
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
                            .Select((substate,i) => ((float)i, LoadState("tmp", substate, animObjectOverride, false)))
                            .ToArray();
                        var built = clipBuilder.MergeSingleFrameClips(sources);
                        built.UseConstantTangents();

                        onClip.CopyFrom(built);
                        
                        break;
                    }
                }
            }

            if (physbonesToReset.Count > 0) {
                var param = physboneResetService.CreatePhysBoneResetter(physbonesToReset, name);
                onClip.SetCurve(EditorCurveBinding.FloatCurve("", typeof(Animator), param.Name()), 1);
            }

            if (applyOffClip) {
                restingState.ApplyClipToRestingState(offClip);
            }

            if (onClip.CollapseProxyBindings().Count > 0) {
                throw new Exception(
                    "VRChat proxy clips cannot be used within VRCFury actions. Please use an alternate clip.");
            }

            var types = onClip.GetMuscleBindingTypes();
            if (types.Contains(EditorCurveBindingExtensions.MuscleBindingType.Other)) {
                types = ImmutableHashSet.Create(EditorCurveBindingExtensions.MuscleBindingType.Other);
            }
            foreach (var muscleType in types) {
                var trigger = fullBodyEmoteService.AddClip(onClip, muscleType);
                onClip.SetCurve(EditorCurveBinding.FloatCurve("", typeof(Animator), trigger.Name()), 1);
            }

            return onClip;
        }

        public static (IList<Renderer>, ShaderUtil.ShaderPropertyType? type) MatPropLookup(
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
                return (renderers, null);
            }

            var found = renderers
                .Select(r => (r, r.GetPropertyType(propName)))
                .Where(pair => pair.Item2 != null)
                .ToArray();
            if (found.Length == 0) {
                return (new Renderer[] {}, null);
            }
            // Limit to the material type of the first found renderer
            var type = found[0].Item2;
            renderers = found
                .Where(pair => pair.Item2 == type)
                .Select(pair => pair.Item1)
                .ToArray();
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

            var rewrites = new Dictionary<string, string>();
            foreach (var (floatParam,targetParam,onValue) in drivenParams) {
                if (nonFloatParams.Contains(targetParam)) {
                    driveOtherTypesFromFloatService.Drive(floatParam, targetParam, onValue);
                } else {
                    rewrites.Add(floatParam.Name(), targetParam);
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
