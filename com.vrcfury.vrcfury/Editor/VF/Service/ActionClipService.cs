using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
using VF.Builder;
using VF.Component;
using VF.Feature;
using VF.Injector;
using VF.Model;
using VF.Model.StateAction;
using VF.Utils;

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
        
        public AnimationClip LoadState(string name, State state, VFGameObject animObjectOverride = null, bool applyOffClip = true) {
            var fx = avatarManager.GetFx();
            var avatarObject = avatarManager.AvatarObject;

            if (state == null || state.actions.Count == 0) {
                return avatarManager.GetFx().GetEmptyClip();
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

            var firstClip = state.actions
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

            foreach (var action in state.actions) {
                switch (action) {
                    case FlipbookAction flipbook:
                        if (flipbook.obj != null) {
                            // If we animate the frame to a flat number, unity can internally do some weird tweening
                            // which can result in it being just UNDER our target, (say 0.999 instead of 1), resulting
                            // in unity displaying frame 0 instead of 1. Instead, we target framenum+0.5, so there's
                            // leniency around it.
                            var frameAnimNum = (float)(Math.Floor((double)flipbook.frame) + 0.5);
                            var binding = EditorCurveBinding.FloatCurve(
                                clipBuilder.GetPath(flipbook.obj),
                                typeof(SkinnedMeshRenderer),
                                "material._FlipbookCurrentFrame"
                            );
                            onClip.SetConstant(binding, frameAnimNum);
                        }
                        break;
                    case ShaderInventoryAction shaderInventoryAction: {
                        var renderer = shaderInventoryAction.renderer;
                        if (renderer != null) {
                            var binding = EditorCurveBinding.FloatCurve(
                                clipBuilder.GetPath(renderer.gameObject),
                                renderer.GetType(),
                                $"material._InventoryItem{shaderInventoryAction.slot:D2}Animated"
                            );
                            offClip.SetConstant(binding, 0);
                            onClip.SetConstant(binding, 1);
                        }
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
                                clipBuilder.GetPath(renderer.gameObject),
                                renderer.GetType(),
                                $"material.{propertyName}"
                            );
                            offClip.SetConstant(binding, 1f);
                            onClip.SetConstant(binding, 0f);
                        }
                        break;
                    }
                    case MaterialPropertyAction materialPropertyAction: {
                        if (materialPropertyAction.renderer == null && !materialPropertyAction.affectAllMeshes) break;
                        var renderers = new[] { materialPropertyAction.renderer };
                        if (materialPropertyAction.affectAllMeshes) {
                            renderers = avatarObject.GetComponentsInSelfAndChildren<Renderer>();
                        }

                        foreach (var renderer in renderers) {
                            var binding = EditorCurveBinding.FloatCurve(
                                clipBuilder.GetPath(renderer.gameObject),
                                renderer.GetType(),
                                $"material.{materialPropertyAction.propertyName}"
                            );
                            if (renderer.sharedMaterials.Any(mat =>
                                    mat != null && mat.HasProperty(materialPropertyAction.propertyName))) {
                                onClip.SetConstant(binding, materialPropertyAction.value);
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
                            if (!skin.sharedMesh) continue;
                            var blendShapeIndex = skin.sharedMesh.GetBlendShapeIndex(blendShape.blendShape);
                            if (blendShapeIndex < 0) continue;
                            foundOne = true;
                            //var defValue = skin.GetBlendShapeWeight(blendShapeIndex);
                            clipBuilder.BlendShape(onClip, skin, blendShape.blendShape, blendShape.blendShapeValue);
                        }
                        if (!foundOne) {
                            Debug.LogWarning("BlendShape not found in avatar: " + blendShape.blendShape);
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
                    case MaterialAction matAction:
                        if (matAction.obj == null) {
                            Debug.LogWarning("Missing object in action: " + name);
                            break;
                        }
                        if (matAction.mat?.Get() == null) {
                            Debug.LogWarning("Missing material in action: " + name);
                            break;
                        }
                        clipBuilder.Material(onClip, matAction.obj, matAction.materialIndex, matAction.mat.Get());
                        break;
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
                        offClip.SetConstant(binding, 0);
                        onClip.SetConstant(binding, 1);
                        break;
                    }
                    case FxFloatAction fxFloatAction: {
                        if (string.IsNullOrWhiteSpace(fxFloatAction.name)) {
                            break;
                        }

                        if (FullControllerBuilder.VRChatGlobalParams.Contains(fxFloatAction.name)) {
                            throw new Exception("Set an FX Float cannot set built-in vrchat parameters");
                        }

                        var binding = EditorCurveBinding.FloatCurve(
                            "",
                            typeof(Animator),
                            fxFloatAction.name
                        );
                        onClip.SetConstant(binding, fxFloatAction.value);
                        break;
                    }
                    case BlockBlinkingAction blockBlinkingAction: {
                        var blockTracking = trackingConflictResolverBuilder.AddInhibitor(name, TrackingConflictResolverBuilder.TrackingEyes);
                        onClip.SetConstant(EditorCurveBinding.FloatCurve("", typeof(Animator), blockTracking.Name()), 1);
                        break;
                    }
                }
            }

            if (applyOffClip) {
                restingState.ApplyClipToRestingState(offClip);
            }

            if (onClip.CollapseProxyBindings().Count > 0) {
                throw new Exception(
                    "VRChat proxy clips cannot be used within VRCFury actions. Please use an alternate clip.");
            }

            if (onClip.GetFloatBindings().Any(b =>
                    b.GetMuscleBindingType() == EditorCurveBindingExtensions.MuscleBindingType.Other)) {
                var trigger = fullBodyEmoteService.AddClip(onClip);
                onClip.SetConstant(EditorCurveBinding.FloatCurve("", typeof(Animator), trigger.Name()), 1);
            }

            return onClip;
        }
    }
}