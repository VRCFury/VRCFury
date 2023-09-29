using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using VF.Builder;
using VF.Builder.Exceptions;
using VF.Component;
using VF.Feature.Base;
using VF.Injector;
using VF.Inspector;
using VF.Model.Feature;
using VF.Service;
using VF.Utils;
using VRC.SDK3.Dynamics.Contact.Components;

namespace VF.Feature {
    [VFService]
    public class BakeHapticPlugsBuilder : FeatureBuilder {
        
        [VFAutowired] private readonly ActionClipService actionClipService;
        [VFAutowired] private readonly RestingStateBuilder restingState;
        [VFAutowired] private readonly HapticAnimContactsService _hapticAnimContactsService;
        [VFAutowired] private readonly ForceStateInAnimatorService _forceStateInAnimatorService;
        [VFAutowired] private readonly ScalePropertyCompensationService scaleCompensationService;

        [FeatureBuilderAction(FeatureOrder.BakeHapticPlugs)]
        public void Apply() {
            var fx = GetFx();
            var usedNames = new List<string>();
            var plugRenderers = new Dictionary<VFGameObject, VRCFuryHapticPlug>();

            AnimationClip tipLightOnClip = null;

            foreach (var plug in avatarObject.GetComponentsInSelfAndChildren<VRCFuryHapticPlug>()) {
                try {
                    PhysboneUtils.RemoveFromPhysbones(plug.transform);
                    var bakeInfo = VRCFuryHapticPlugEditor.Bake(
                        plug,
                        usedNames,
                        plugRenderers,
                        mutableManager: mutableManager,
                        deferMaterialConfig: true
                    );

                    if (bakeInfo == null) continue;

                    var name = bakeInfo.name;
                    var bakeRoot = bakeInfo.bakeRoot;
                    var renderers = bakeInfo.renderers;
                    var worldLength = bakeInfo.worldLength;
                    foreach (var r in bakeRoot.GetComponentsInSelfAndChildren<VRCContactReceiver>()) {
                        _forceStateInAnimatorService.DisableDuringLoad(r.transform);
                    }

                    if (plug.configureTps || plug.enableSps) {
                        foreach (var r in renderers) {
                            var renderer = r.renderer;
                            addOtherFeature(new TpsScaleFix() { singleRenderer = renderer });
                            if (renderer is SkinnedMeshRenderer skin) {
                                addOtherFeature(new BoundingBoxFix2() { skipRenderer = skin });
                            }
                        }
                    }

                    var postBakeClip = actionClipService.LoadState("sps_postbake", plug.postBakeActions, plug.owner());
                    restingState.ApplyClipToRestingState(postBakeClip);

                    if (plug.enableSps) {
                        foreach (var r in renderers) {
                            spsRewritesToDo.Add(new SpsRewriteToDo {
                                plugObject = plug.owner(),
                                skin = (SkinnedMeshRenderer)r.renderer,
                                bakeRoot = bakeRoot,
                                configureMaterial = r.configureMaterial,
                                spsBlendshapes = r.spsBlendshapes
                            });
                        }
                    }

                    if (plug.addDpsTipLight) {
                        var tip = GameObjects.Create("LegacyDpsTip", bakeRoot);
                        tip.active = false;
                        if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android) {
                            var light = tip.AddComponent<Light>();
                            light.type = LightType.Point;
                            light.color = Color.black;
                            light.range = 0.49f;
                            light.shadows = LightShadows.None;
                            light.renderMode = LightRenderMode.ForceVertex;
                            light.intensity = worldLength;

                            dpsTipToDo.Add(light);
                        }

                        if (tipLightOnClip == null) {
                            var param = fx.NewBool("tipLight", synced: true);
                            manager.GetMenu()
                                .NewMenuToggle(
                                    $"{BakeHapticSocketsBuilder.optionsFolder}/<b>DPS Tip Light<\\/b>\n<size=20>Allows plugs to trigger old DPS animations",
                                    param);
                            tipLightOnClip = fx.NewClip("EnableAutoReceivers");
                            var layer = fx.NewLayer("Tip Light");
                            var off = layer.NewState("Off");
                            var on = layer.NewState("On").WithAnimation(tipLightOnClip);
                            var whenOn = param.IsTrue();
                            off.TransitionsTo(on).When(whenOn);
                            on.TransitionsTo(off).When(whenOn.Not());
                        }

                        clipBuilder.Enable(tipLightOnClip, tip);
                    }

                    if (plug.enableDepthAnimations && plug.depthActions.Count > 0) {
                        var animRoot = GameObjects.Create("Animations", bakeRoot);
                        _hapticAnimContactsService.CreatePlugAnims(
                            plug.depthActions,
                            plug.owner(),
                            animRoot,
                            name,
                            worldLength
                        );
                    }
                } catch (Exception e) {
                    throw new ExceptionWithCause($"Failed to bake Haptic Plug: {plug.owner().GetPath()}", e);
                }
            }
        }
        
        public class SpsRewriteToDo {
            public VFGameObject plugObject;
            public SkinnedMeshRenderer skin;
            public VFGameObject bakeRoot;
            public Func<Material, Material> configureMaterial;
            public IList<string> spsBlendshapes;
        }
        private List<SpsRewriteToDo> spsRewritesToDo = new List<SpsRewriteToDo>();
        private List<Light> dpsTipToDo = new List<Light>();

        [FeatureBuilderAction(FeatureOrder.HapticsAnimationRewrites)]
        public void ApplySpsRewrites() {
            foreach (var rewrite in spsRewritesToDo) {
                var pathToPlug = rewrite.plugObject.GetPath(avatarObject);
                var pathToRenderer = rewrite.skin.owner().GetPath(avatarObject);
                var pathToBake = rewrite.bakeRoot.GetPath(avatarObject);
                var mesh = rewrite.skin.sharedMesh;
                var spsEnabledBinding = EditorCurveBinding.FloatCurve(
                    pathToRenderer,
                    typeof(SkinnedMeshRenderer),
                    "material._SPS_Enabled"
                );
                var hapticsEnabledBinding = EditorCurveBinding.FloatCurve(
                    pathToBake,
                    typeof(GameObject),
                    "m_IsActive"
                );

                void RewriteClip(AnimationClip clip) {
                    foreach (var (binding,curve) in clip.GetAllCurves()) {
                        if (curve.IsFloat) {
                            if (binding.path == pathToRenderer) {
                                if (binding.propertyName == "material._TPS_AnimatedToggle") {
                                    clip.SetCurve(spsEnabledBinding, curve);
                                    clip.SetCurve(hapticsEnabledBinding, curve);
                                }
                            }
                            if (binding.path == pathToPlug) {
                                if (binding.propertyName == "spsAnimatedEnabled") {
                                    clip.SetCurve(spsEnabledBinding, curve);
                                    clip.SetCurve(hapticsEnabledBinding, curve);
                                }
                            }
                        }
                        if (binding.path == pathToRenderer && binding.type == typeof(MeshRenderer)) {
                            var b = binding;
                            b.type = typeof(SkinnedMeshRenderer);
                            clip.SetCurve(b, curve);
                        }

                        if (curve.IsFloat && binding.path == pathToRenderer && binding.type == typeof(SkinnedMeshRenderer) && binding.propertyName.StartsWith("blendShape.")) {
                            var blendshapeName = binding.propertyName.Substring(11);
                            var blendshapeMeshIndex = rewrite.spsBlendshapes.IndexOf(blendshapeName);
                            if (blendshapeMeshIndex >= 0) {
                                clip.SetCurve(
                                    EditorCurveBinding.FloatCurve(pathToRenderer, typeof(SkinnedMeshRenderer), $"material._SPS_Blendshape{blendshapeMeshIndex}"),
                                    curve
                                );
                            }
                        }

                        if (!curve.IsFloat && binding.path == pathToRenderer && binding.propertyName.StartsWith("m_Materials")) {
                            var newKeys = curve.ObjectCurve.Select(frame => {
                                if (frame.value is Material m) frame.value = rewrite.configureMaterial(m);
                                return frame;
                            }).ToArray();
                            clip.SetCurve(binding, new FloatOrObjectCurve(newKeys));
                        }
                    }
                }
                foreach (var c in manager.GetAllUsedControllers()) {
                    foreach (var clip in c.GetClips()) {
                        RewriteClip(clip);
                    }
                }
                foreach (var clip in restingState.GetPendingClips()) {
                    RewriteClip(clip);
                }

                rewrite.skin.sharedMaterials = rewrite.skin.sharedMaterials.Select(rewrite.configureMaterial).ToArray();
            }
        }

        [FeatureBuilderAction(FeatureOrder.DpsTipScaleFix)]
        public void ApplyDpsTipScale() {
            foreach (var light in dpsTipToDo)
                scaleCompensationService.AddScaledPorperties(light.gameObject,
                    new[] { (AnimationUtility.CalculateTransformPath(light.transform, avatarObject.transform), typeof(Light), "m_Intensity", (object)light.intensity) });
        }
    }
}