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
using VF.Inspector;
using VF.Model.Feature;
using VF.Utils;

namespace VF.Feature {
    public class TpsScaleFixBuilder : FeatureBuilder<TpsScaleFix> {
        [FeatureBuilderAction(FeatureOrder.TpsScaleFix)]
        public void Apply() {
            if (this != allBuildersInRun.OfType<TpsScaleFixBuilder>().First()) {
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

                    if (TpsConfigurer.IsLocked(mat)) {
                        throw new VRCFBuilderException(
                            "TpsScaleFix requires that all deforming materials using poiyomi must be unlocked. " +
                            "Please unlock the material on " +
                            pathToRenderer);
                    }
                    mat = mutableManager.MakeMutable(mat);
                    if (isTps) {
                        mat.SetOverrideTag("_TPS_PenetratorLengthAnimated", "1");
                        mat.SetOverrideTag("_TPS_PenetratorScaleAnimated", "1");
                    }
                    if (isSps) {
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
                    var one = GetFx().NewFloat("one", def: 1);
                    directTree.AddChild(zeroClip);
                    SetLastParam(directTree, one);
                }

                var scaleClip = GetFx().NewClip("tpsScale_" + objectNumber);
                foreach (var scaledProp in scaledProps) {
                    var propertyName = scaledProp.Key;
                    if (scaledProp.Value is float f) {
                        var lengthOffset = f / handledScale;
                        scaleClip.SetCurve(pathToRenderer, renderer.GetType(), $"material.{propertyName}", ClipBuilder.OneFrame(lengthOffset));
                        zeroClip.SetCurve(pathToRenderer, renderer.GetType(), $"material.{propertyName}", ClipBuilder.OneFrame(0));
                    } else if (scaledProp.Value is Vector4 vec) {
                        var scaleOffset = vec.z / handledScale;
                        scaleClip.SetCurve(pathToRenderer, renderer.GetType(), $"material.{propertyName}.x", ClipBuilder.OneFrame(scaleOffset));
                        scaleClip.SetCurve(pathToRenderer, renderer.GetType(), $"material.{propertyName}.y", ClipBuilder.OneFrame(scaleOffset));
                        scaleClip.SetCurve(pathToRenderer, renderer.GetType(), $"material.{propertyName}.z", ClipBuilder.OneFrame(scaleOffset));
                        zeroClip.SetCurve(pathToRenderer, renderer.GetType(), $"material.{propertyName}.x", ClipBuilder.OneFrame(0));
                        zeroClip.SetCurve(pathToRenderer, renderer.GetType(), $"material.{propertyName}.y", ClipBuilder.OneFrame(0));
                        zeroClip.SetCurve(pathToRenderer, renderer.GetType(), $"material.{propertyName}.z", ClipBuilder.OneFrame(0));
                    }
                }

                pathToParam["nativeScale"] = GetFx().NewFloat("ScaleFactor", def: 1, usePrefix: false);
                
                var tree = directTree;
                foreach (var (param,index) in pathToParam.Values.Select((p,index) => (p,index))) {
                    var isLast = index == pathToParam.Count - 1;
                    if (isLast) {
                        tree.AddChild(scaleClip);
                        SetLastParam(tree, param);
                    } else {
                        var subTree = GetFx().NewBlendTree("shaderScaleSub");
                        subTree.blendType = BlendTreeType.Direct;
                        tree.AddChild(subTree);
                        SetLastParam(tree, param);
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

        private static void SetLastParam(BlendTree tree, VFAParam param) {
            var children = tree.children;
            var child = children[children.Length - 1];
            child.directBlendParameter = param.Name();
            children[children.Length - 1] = child;
            tree.children = children;
        }

        public override string GetEditorTitle() {
            return "TPS Scale Fix (BETA)";
        }

        public override VisualElement CreateEditor(SerializedProperty prop) {
            return VRCFuryEditorUtils.Info(
                "This feature will allow Poiyomi TPS to work properly with scaling. While active, avatar scaling, " +
                "object scaling, or any combination of the two may be used in conjunction with TPS.\n\n" +
                "Beware: this feature is BETA and may not work properly.");
        }

        public override bool AvailableOnProps() {
            return false;
        }
    }
}
