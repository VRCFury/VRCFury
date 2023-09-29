using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
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

namespace VF.Feature {
    public class TpsScaleFixBuilder : FeatureBuilder<TpsScaleFix> {
        [VFAutowired] private readonly ScalePropertyCompensationService scaleCompensationService;
        
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

                var props = scaledProps.Select(p => (pathToRenderer, renderer.GetType(), $"material.{p.Key}", p.Value));
                scaleCompensationService.AddScaledPorperties(rootBone, props);
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
