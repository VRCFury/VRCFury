using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.UIElements;
using VF.Builder;
using VF.Builder.Exceptions;
using VF.Builder.Haptics;
using VF.Feature.Base;
using VF.Injector;
using VF.Inspector;
using VF.Model.Feature;
using VF.Service;
using VF.Utils;
using VF.Utils.Controller;

namespace VF.Feature {
    public class TpsScaleFixBuilder : FeatureBuilder<TpsScaleFix> {
        [VFAutowired] private readonly ScaleFactorService scaleFactorService;
        
        [FeatureBuilderAction(FeatureOrder.TpsScaleFix)]
        public void Apply() {
            if (!IsFirst()) {
                return;
            }

            var allRenderers = false;
            var renderers = new HashSet<Renderer>();
            foreach (var component in allFeaturesInRun.OfType<TpsScaleFix>()) {
                if (component.singleRenderer) {
                    renderers.Add(component.singleRenderer);
                } else {
                    allRenderers = true;
                    renderers.UnionWith(avatarObject.GetComponentsInSelfAndChildren<Renderer>());
                }
            }
            
            var objectNumber = 0;
            BlendTree directTree = null;
            AnimationClip zeroClip = null;

            if (allRenderers) {
                // Remove old fix attempts
                foreach (var clip in GetFx().GetClips()) {
                    foreach (var binding in clip.GetFloatBindings()) {
                        if (binding.propertyName.Contains("_TPS_PenetratorLength") ||
                            binding.propertyName.Contains("_TPS_PenetratorScale")) {
                            clip.SetFloatCurve(binding, null);
                        }
                    }
                }
            }

            var animatedPaths = GetFx().GetClips()
                .SelectMany(clip => clip.GetFloatBindings())
                .Where(IsScaleBinding)
                .Select(b => b.path)
                .ToImmutableHashSet();
            
            foreach (var renderer in renderers) {
                var pathToRenderer =
                    AnimationUtility.CalculateTransformPath(renderer.transform, avatarObject.transform);

                var scaledProps = GetScaledProps(renderer.sharedMaterials);
                if (scaledProps.Count == 0) {
                    continue;
                }

                renderer.sharedMaterials = renderer.sharedMaterials.Select(mat => {
                    var isTps = TpsConfigurer.IsTps(mat);
                    var isSps = SpsConfigurer.IsSps(mat);

                    if (!isTps && !isSps) return mat;

                    mat = mutableManager.MakeMutable(mat, renderer.owner());
                    if (isTps) {
                        if (TpsConfigurer.IsLocked(mat)) {
                            throw new VRCFBuilderException(
                                "TpsScaleFix requires that all deforming materials using poiyomi must be unlocked. " +
                                "Please unlock the material on " +
                                pathToRenderer);
                        }
                        mat.SetOverrideTag("_TPS_PenetratorLengthAnimated", "1");
                        mat.SetOverrideTag("_TPS_PenetratorScaleAnimated", "1");
                    }
                    if (isSps) {
                        // We can assume that SPS-patched poiyomi is always unlocked at this point, since either:
                        // 1. The mat was unlocked before the build, we patched it and it's still unlocked (poi will lock it after vrcf)
                        // 2. The mat was locked before the build, we patched it and now our fields are unlocked even though everything else is locked
                        mat.SetOverrideTag("_SPS_LengthAnimated", "1");
                    }
                    return mat;
                }).ToArray();

                VFGameObject rootBone = renderer.transform;
                if (renderer is SkinnedMeshRenderer skin && skin.rootBone != null) {
                    rootBone = skin.rootBone;
                }

                var parentPaths =
                    rootBone.GetComponentsInSelfAndParents<Transform>()
                        .Select(t => AnimationUtility.CalculateTransformPath(t, avatarObject.transform))
                        .ToList();

                var animatedParentPaths = parentPaths
                    .Where(path => animatedPaths.Contains(path))
                    .Where(path => path != "") // VRChat ignores animations of the root scale now, so we need to as well
                    .ToList();

                objectNumber++;
                Debug.Log("Processing " + pathToRenderer);

                var pathToParam = new Dictionary<string, VFAFloat>();
                var pathNumber = 0;
                foreach (var path in animatedParentPaths) {
                    pathNumber++;
                    var param = GetFx().NewFloat("shaderScale_" + objectNumber + "_" + pathNumber, def: avatarObject.transform.Find(path).localScale.z);
                    pathToParam[path] = param;
                    Debug.Log(path + " " + param.Name());
                }
                foreach (var clip in GetFx().GetClips()) {
                    foreach (var binding in clip.GetFloatBindings()) {
                        if (!IsScaleBinding(binding)) continue;
                        if (!pathToParam.TryGetValue(binding.path, out var param)) continue;
                        var newBinding = new EditorCurveBinding();
                        newBinding.type = typeof(Animator);
                        newBinding.path = "";
                        newBinding.propertyName = param.Name();
                        clip.SetFloatCurve(newBinding, clip.GetFloatCurve(binding));
                    }
                }

                float handledScale = 1;
                foreach (var path in animatedParentPaths) {
                    handledScale *= avatarObject.transform.Find(path).localScale.z;
                }

                if (directTree == null) {
                    Debug.Log("Creating direct layer");
                    var layer = GetFx().NewLayer("shaderScale");
                    var state = layer.NewState("Scale");
                    directTree = GetFx().NewBlendTree("shaderScale");
                    directTree.blendType = BlendTreeType.Direct;
                    state.WithAnimation(directTree);

                    zeroClip = GetFx().NewClip("zeroScale");
                    var one = GetFx().One();
                    directTree.AddDirectChild(one.Name(), zeroClip);
                }

                var scaleClip = GetFx().NewClip("tpsScale_" + objectNumber);
                foreach (var scaledProp in scaledProps) {
                    var propertyName = scaledProp.Key;
                    if (scaledProp.Value is float f) {
                        var lengthOffset = f / handledScale;
                        scaleClip.SetCurve(pathToRenderer, renderer.GetType(), $"material.{propertyName}", ClipBuilderService.OneFrame(lengthOffset));
                        zeroClip.SetCurve(pathToRenderer, renderer.GetType(), $"material.{propertyName}", ClipBuilderService.OneFrame(0));
                    } else if (scaledProp.Value is Vector4 vec) {
                        var scaleOffset = vec.z / handledScale;
                        scaleClip.SetCurve(pathToRenderer, renderer.GetType(), $"material.{propertyName}.x", ClipBuilderService.OneFrame(scaleOffset));
                        scaleClip.SetCurve(pathToRenderer, renderer.GetType(), $"material.{propertyName}.y", ClipBuilderService.OneFrame(scaleOffset));
                        scaleClip.SetCurve(pathToRenderer, renderer.GetType(), $"material.{propertyName}.z", ClipBuilderService.OneFrame(scaleOffset));
                        zeroClip.SetCurve(pathToRenderer, renderer.GetType(), $"material.{propertyName}.x", ClipBuilderService.OneFrame(0));
                        zeroClip.SetCurve(pathToRenderer, renderer.GetType(), $"material.{propertyName}.y", ClipBuilderService.OneFrame(0));
                        zeroClip.SetCurve(pathToRenderer, renderer.GetType(), $"material.{propertyName}.z", ClipBuilderService.OneFrame(0));
                    }
                }

                pathToParam["nativeScale"] = scaleFactorService.Get();
                
                var tree = directTree;
                foreach (var (param,index) in pathToParam.Values.Select((p,index) => (p,index))) {
                    var isLast = index == pathToParam.Count - 1;
                    if (isLast) {
                        tree.AddDirectChild(param.Name(), scaleClip);
                    } else {
                        var subTree = GetFx().NewBlendTree("shaderScaleSub");
                        subTree.blendType = BlendTreeType.Direct;
                        tree.AddDirectChild(param.Name(), subTree);
                        tree = subTree;
                    }
                }
            }
        }

        private static Dictionary<string, object> GetScaledProps(IEnumerable<Material> materials) {
            var scaledProps = new Dictionary<string, object>();
            foreach (var mat in materials) {
                void AddProp(string propName, bool isVector) {
                    if (!mat.HasProperty(propName)) return;
                    if (!isVector) {
                        var newVal = mat.GetFloat(propName);
                        if (scaledProps.TryGetValue(propName, out var oldVal) && newVal != (float)oldVal) {
                            throw new Exception(
                                "This renderer contains multiple materials with different scale values");
                        }
                        scaledProps[propName] = newVal;
                    } else {
                        var newVal = mat.GetVector(propName);
                        if (scaledProps.TryGetValue(propName, out var oldVal) && newVal != (Vector4)oldVal) {
                            throw new Exception(
                                "This renderer contains multiple materials with different scale values");
                        }
                        scaledProps[propName] = newVal;
                    }
                }
                
                if (TpsConfigurer.IsTps(mat)) {
                    AddProp("_TPS_PenetratorLength", false);
                    AddProp("_TPS_PenetratorScale", true);
                } else if (SpsConfigurer.IsSps(mat)) {
                    AddProp("_SPS_Length", false);
                }
            }
            return scaledProps;
        }

        private static bool IsScaleBinding(EditorCurveBinding binding) {
            return binding.type == typeof(Transform) && binding.propertyName == "m_LocalScale.z";
        }

        public override string GetEditorTitle() {
            return "TPS Scale Fix (Deprecated)";
        }

        public override VisualElement CreateEditor(SerializedProperty prop) {
            var c = new VisualElement();
            c.Add(VRCFuryEditorUtils.Error(
                "This component is deprecated. It still works, but you may wish to migrate from TPS to SPS for" +
                " native scaling support, as well as other benefits.\n\nCheck out https://vrcfury.com/sps"));
            c.Add(VRCFuryEditorUtils.Info(
                "This feature will allow Poiyomi TPS to work properly with scaling. While active, avatar scaling, " +
                "object scaling, or any combination of the two may be used in conjunction with TPS."));
            return c;
        }

        public override bool AvailableOnProps() {
            return false;
        }
    }
}
