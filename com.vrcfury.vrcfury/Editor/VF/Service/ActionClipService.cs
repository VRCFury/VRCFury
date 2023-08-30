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
using VRC.SDK3.Avatars.Components;

namespace VF.Service {
    /** Turns VRCFury actions into clips */
    [VFService]
    public class ActionClipService {
        [VFAutowired] private readonly MutableManager mutableManager;
        [VFAutowired] private readonly RestingStateBuilder restingState;
        [VFAutowired] private readonly AvatarManager avatarManager;
        [VFAutowired] private readonly ClipBuilderService clipBuilder;
        
        public AnimationClip LoadState(string name, State state, VFGameObject animObjectOverride = null) {
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
                .Select(action => action.clip)
                .FirstOrDefault()
                .Get();
            if (firstClip) {
                var copy = mutableManager.CopyRecursive(firstClip);
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
                        if (poiyomiUVTileAction.slot1 > 3 || poiyomiUVTileAction.slot1 < 0) {
                            throw new ArgumentException("Poiyomi UV Tiles are ranges between 0-3, check if slots are within these ranges.");
                        }
                        if (poiyomiUVTileAction.slot2 > 3 || poiyomiUVTileAction.slot2 < 0) {
                            throw new ArgumentException("Poiyomi UV Tiles are ranges between 0-3, check if slots are within these ranges.");
                        }
                        if (renderer != null) {
                            var propertyName = poiyomiUVTileAction.dissolveAlpha ? "_UVTileDissolveAlpha_Row" : "_UDIMDiscardRow";
                            var binding = EditorCurveBinding.FloatCurve(
                                clipBuilder.GetPath(renderer.gameObject),
                                renderer.GetType(),
                                $"material.{propertyName+poiyomiUVTileAction.slot1}_{(poiyomiUVTileAction.slot2)}"
                            );
                            if (!poiyomiUVTileAction.toggled)
                                offClip.SetConstant(binding, poiyomiUVTileAction.invert ? 1f: 0f); //Ternary Operator easier to than If statement
                            onClip.SetConstant(binding, poiyomiUVTileAction.invert ? 0f : 1f);
                        }
                        if (onClip != firstClip) {
                            var copy = mutableManager.CopyRecursive(onClip);
                            copy.Rewrite(rewriter);
                            onClip.CopyFrom(copy);
                        }
                        break;
                    }
                    case AnimationClipAction clipAction:
                        var clipActionClip = clipAction.clip.Get();
                        if (clipActionClip && clipActionClip != firstClip) {
                            var copy = mutableManager.CopyRecursive(clipActionClip);
                            copy.Rewrite(rewriter);
                            onClip.CopyFrom(copy);
                        }
                        break;
                    case ObjectToggleAction toggle:
                        if (toggle.obj == null) {
                            Debug.LogWarning("Missing object in action: " + name);
                        } else {
                            clipBuilder.Enable(offClip, toggle.obj, toggle.obj.activeSelf);
                            clipBuilder.Enable(onClip, toggle.obj, !toggle.obj.activeSelf);
                        }
                        break;
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
                        if (matAction.mat == null) {
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
                }
            }

            restingState.ApplyClipToRestingState(offClip);

            return onClip;
        }
    }
}