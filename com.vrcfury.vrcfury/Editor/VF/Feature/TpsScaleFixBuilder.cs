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
    [FeatureTitle("TPS Scale Fix (Deprecated)")]
    [FeatureOnlyOneAllowed]
    [FeatureRootOnly]
    [FeatureHideInMenu]
    internal class TpsScaleFixBuilder : FeatureBuilder<TpsScaleFix> {
        [VFAutowired] private readonly VFGameObject avatarObject;
        [VFAutowired] private readonly ScalePropertyCompensationService scaleCompensationService;
        [VFAutowired] private readonly ControllersService controllers;
        [VFAutowired] private readonly GlobalsService globals;
        private ControllerManager fx => controllers.GetFx();
        
        [FeatureBuilderAction(FeatureOrder.TpsScaleFix)]
        public void Apply() {
            if (!IsFirst()) {
                return;
            }

            var allRenderers = false;
            var renderers = new HashSet<Renderer>();
            foreach (var component in globals.allFeaturesInRun.OfType<TpsScaleFix>()) {
                if (component.singleRenderer) {
                    renderers.Add(component.singleRenderer);
                } else {
                    allRenderers = true;
                    renderers.UnionWith(avatarObject.GetComponentsInSelfAndChildren<Renderer>());
                }
            }

            if (allRenderers) {
                // Remove old fix attempts
                foreach (var clip in fx.GetClips()) {
                    foreach (var binding in clip.GetFloatBindings()) {
                        if (binding.propertyName.Contains("_TPS_PenetratorLength") ||
                            binding.propertyName.Contains("_TPS_PenetratorScale")) {
                            clip.SetCurve(binding, null);
                        }
                    }
                }
            }

            foreach (var renderer in renderers) {
                var scaledProps = GetScaledProps(renderer.sharedMaterials);
                if (scaledProps.Count == 0) {
                    continue;
                }

                renderer.sharedMaterials = renderer.sharedMaterials.Select(mat => {
                    var isTps = TpsConfigurer.IsTps(mat);

                    if (!isTps) return mat;

                    mat = mat.Clone("Needed to mark TPS parameters as animated for TPS Scale Fix");
                    if (TpsConfigurer.IsLocked(mat)) {
                        throw new VRCFBuilderException(
                            "TpsScaleFix requires that all deforming materials using poiyomi must be unlocked. " +
                            $"Please unlock the material on {renderer.owner().GetPath()}");
                    }
                    mat.SetOverrideTag("_TPS_PenetratorLengthAnimated", "1");
                    mat.SetOverrideTag("_TPS_PenetratorScaleAnimated", "1");
                    return mat;
                }).ToArray();

                var rootBone = renderer.owner();
                if (renderer is SkinnedMeshRenderer skin && skin.rootBone != null) {
                    rootBone = skin.rootBone;
                }

                var props = scaledProps.Select(p => (
                    (UnityEngine.Component)renderer,
                    $"material.{p.Key}",
                    p.Value / rootBone.worldScale.x
                )).ToList();
                scaleCompensationService.AddScaledProp(rootBone, props);
            }
        }

        private static Dictionary<string, float> GetScaledProps(IEnumerable<Material> materials) {
            var scaledProps = new Dictionary<string, float>();
            foreach (var mat in materials) {
                void Add(string propName, float val) {
                    if (scaledProps.TryGetValue(propName, out var oldVal) && val != oldVal) {
                        throw new Exception(
                            "This renderer contains multiple materials with different scale values");
                    }
                    scaledProps[propName] = val;
                }
                void AddVector(string propName) {
                    if (!mat.HasProperty(propName)) return;
                    var val = mat.GetVector(propName);
                    Add(propName + ".x", val.x);
                    Add(propName + ".y", val.y);
                    Add(propName + ".z", val.z);
                }
                void AddFloat(string propName) {
                    if (!mat.HasProperty(propName)) return;
                    var val = mat.GetFloat(propName);
                    Add(propName, val);
                }
                
                if (TpsConfigurer.IsTps(mat)) {
                    AddFloat("_TPS_PenetratorLength");
                    AddVector("_TPS_PenetratorScale");
                }
            }
            return scaledProps;
        }

        [FeatureEditor]
        public static VisualElement Editor() {
            var c = new VisualElement();
            c.Add(VRCFuryEditorUtils.Error(
                "This component is deprecated. It still works, but you may wish to migrate from TPS to SPS for" +
                " native scaling support, as well as other benefits.\n\nCheck out https://vrcfury.com/sps"));
            c.Add(VRCFuryEditorUtils.Info(
                "This feature will allow Poiyomi TPS to work properly with scaling. While active, avatar scaling, " +
                "object scaling, or any combination of the two may be used in conjunction with TPS."));
            return c;
        }
    }
}
