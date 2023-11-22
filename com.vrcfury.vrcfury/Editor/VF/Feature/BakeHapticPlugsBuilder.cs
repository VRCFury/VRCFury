using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Animations;
using VF.Builder;
using VF.Builder.Exceptions;
using VF.Builder.Haptics;
using VF.Component;
using VF.Feature.Base;
using VF.Injector;
using VF.Inspector;
using VF.Model.Feature;
using VF.Service;
using VF.Utils;
using VRC.Dynamics;
using VRC.SDK3.Dynamics.Contact.Components;

namespace VF.Feature {
    [VFService]
    public class BakeHapticPlugsBuilder : FeatureBuilder {
        
        [VFAutowired] private readonly ActionClipService actionClipService;
        [VFAutowired] private readonly RestingStateBuilder restingState;
        [VFAutowired] private readonly HapticAnimContactsService _hapticAnimContactsService;
        [VFAutowired] private readonly ForceStateInAnimatorService _forceStateInAnimatorService;
        [VFAutowired] private readonly TriangulationService _triangulationService;
        [VFAutowired] private readonly ScalePropertyCompensationService scaleCompensationService;
        [VFAutowired] private readonly SpsOptionsService spsOptions;
        [VFAutowired] private readonly HapticContactsService hapticContacts;

        private Dictionary<VRCFuryHapticPlug, VRCFuryHapticPlugEditor.BakeResult> bakeResults =
            new Dictionary<VRCFuryHapticPlug, VRCFuryHapticPlugEditor.BakeResult>();

        /**
         * We do mesh analysis early, and in a separate step from the rest, so that the post-bake action can be applied
         * before things like toggles are built
         */
        [FeatureBuilderAction(FeatureOrder.BakeHapticPlugs)]
        public void ApplyEarlyBake() {
            var usedRenderers = new Dictionary<VFGameObject, VRCFuryHapticPlug>();
            foreach (var plug in avatarObject.GetComponentsInSelfAndChildren<VRCFuryHapticPlug>()) {
                try {
                    PhysboneUtils.RemoveFromPhysbones(plug.transform);
                    var bakeInfo = VRCFuryHapticPlugEditor.Bake(
                        plug,
                        usedRenderers,
                        mutableManager: mutableManager,
                        deferMaterialConfig: true
                    );
                    if (bakeInfo == null) continue;
                    bakeResults[plug] = bakeInfo;
                    
                    var postBakeClip = actionClipService.LoadState("sps_postbake", plug.postBakeActions, plug.owner());
                    restingState.ApplyClipToRestingState(postBakeClip);
                } catch (Exception e) {
                    throw new ExceptionWithCause($"Failed to bake Haptic Plug: {plug.owner().GetPath()}", e);
                }
            }
        }

        [FeatureBuilderAction]
        public void Apply() {
            var fx = GetFx();
            var usedNames = new List<string>();

            AnimationClip tipLightOnClip = null;
            AnimationClip triangulationOnClip = null;
            var i = 0;

            foreach (var plug in avatarObject.GetComponentsInSelfAndChildren<VRCFuryHapticPlug>()) {
                try {
                    var bakeInfo = bakeResults[plug];
                    if (bakeInfo == null) continue;

                    var bakeRoot = bakeInfo.bakeRoot;
                    var renderers = bakeInfo.renderers;
                    var worldRadius = bakeInfo.worldRadius;
                    var worldLength = bakeInfo.worldLength;
                    foreach (var r in bakeRoot.GetComponentsInSelfAndChildren<VRCContactReceiver>()) {
                        _forceStateInAnimatorService.DisableDuringLoad(r.transform);
                    }

                    var name = plug.name;
                    if (string.IsNullOrWhiteSpace(name)) {
                        if (renderers.Count > 0) {
                            name = HapticUtils.GetName(renderers.First().renderer.owner());
                        } else {
                            name = HapticUtils.GetName(plug.owner());
                        }
                    }
                    if (usedNames != null) name = HapticUtils.GetNextName(usedNames, name);
                    Debug.Log("Baking haptic component in " + plug.owner().GetPath() + " as " + name);
                    
                    // Haptic Receivers
                    {
                        var paramPrefix = "OGB/Pen/" + name.Replace('/','_');
                        var receivers = GameObjects.Create("Receivers", bakeRoot);
                        var halfWay = Vector3.forward * (worldLength / 2);
                        var extraRadiusForTouch = Math.Min(worldRadius, 0.08f /* 8cm */);
                        // Extra rub radius should always match for everyone, so when two plugs collide, both trigger at the same time
                        var extraRadiusForRub = 0.08f;
                        // This is *90 because capsule length is actually "height", so we have to rotate it to make it a length
                        var capsuleRotation = Quaternion.Euler(90,0,0);
                        hapticContacts.AddReceiver(receivers, halfWay, paramPrefix + "/TouchSelfClose", "TouchSelfClose", worldRadius+extraRadiusForTouch, HapticUtils.SelfContacts, HapticUtils.ReceiverParty.Self, usePrefix: false, localOnly:true, rotation: capsuleRotation, height: worldLength+extraRadiusForTouch*2, type: ContactReceiver.ReceiverType.Constant, useHipAvoidance: plug.useHipAvoidance);
                        hapticContacts.AddReceiver(receivers, Vector3.zero, paramPrefix + "/TouchSelf", "TouchSelf", worldLength+extraRadiusForTouch, HapticUtils.SelfContacts, HapticUtils.ReceiverParty.Self, usePrefix: false, localOnly:true, useHipAvoidance: plug.useHipAvoidance);
                        hapticContacts.AddReceiver(receivers, halfWay, paramPrefix + "/TouchOthersClose", "TouchOthersClose", worldRadius+extraRadiusForTouch, HapticUtils.BodyContacts, HapticUtils.ReceiverParty.Others, usePrefix: false, localOnly:true, rotation: capsuleRotation, height: worldLength+extraRadiusForTouch*2, type: ContactReceiver.ReceiverType.Constant, useHipAvoidance: plug.useHipAvoidance);
                        hapticContacts.AddReceiver(receivers, Vector3.zero, paramPrefix + "/TouchOthers", "TouchOthers", worldLength+extraRadiusForTouch, HapticUtils.BodyContacts, HapticUtils.ReceiverParty.Others, usePrefix: false, localOnly:true, useHipAvoidance: plug.useHipAvoidance);
                        hapticContacts.AddReceiver(receivers, Vector3.zero, paramPrefix + "/PenSelf", "PenSelf", worldLength, new []{HapticUtils.TagTpsOrfRoot}, HapticUtils.ReceiverParty.Self, usePrefix: false, localOnly:true, useHipAvoidance: plug.useHipAvoidance);
                        hapticContacts.AddReceiver(receivers, Vector3.zero, paramPrefix + "/PenOthers", "PenOthers", worldLength, new []{HapticUtils.TagTpsOrfRoot}, HapticUtils.ReceiverParty.Others, usePrefix: false, localOnly:true, useHipAvoidance: plug.useHipAvoidance);
                        hapticContacts.AddReceiver(receivers, Vector3.zero, paramPrefix + "/FrotOthers", "FrotOthers", worldLength, new []{HapticUtils.CONTACT_PEN_CLOSE}, HapticUtils.ReceiverParty.Others, usePrefix: false, localOnly:true, useHipAvoidance: plug.useHipAvoidance);
                        hapticContacts.AddReceiver(receivers, halfWay, paramPrefix + "/FrotOthersClose", "FrotOthersClose", worldRadius+extraRadiusForRub, new []{HapticUtils.CONTACT_PEN_CLOSE}, HapticUtils.ReceiverParty.Others, usePrefix: false, localOnly:true, rotation: capsuleRotation, height: worldLength, type: ContactReceiver.ReceiverType.Constant, useHipAvoidance: plug.useHipAvoidance);
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

                    if (spsOptions.GetOptions().enableLightlessToggle2 && plug.enableSps) {
                        var triRoot = GameObjects.Create("SpsTriangulator", bakeRoot);
                        triRoot.active = false;
                        triRoot.worldScale = Vector3.one;
                        if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android) {
                            var p = triRoot.AddComponent<ScaleConstraint>();
                            p.AddSource(new ConstraintSource() {
                                sourceTransform = VRCFuryEditorUtils.GetResource<Transform>("world.prefab"),
                                weight = 1
                            });
                            p.weight = 1;
                            p.constraintActive = true;
                            p.locked = true;

                            for (var partyI = 0; partyI <= 1; partyI++) {
                                var party = partyI == 0 ? HapticUtils.ReceiverParty.Self : HapticUtils.ReceiverParty.Others;
                                var prefix = partyI == 0 ? "Self" : "Other";
                                
                                var tri = _triangulationService.CreateTriangulator(triRoot, "Target", $"sps_tri_{i}",
                                    new[] { HapticUtils.TagSpsSocketRoot }, party, useHipAvoidance: plug.useHipAvoidance);
                                var triFront = _triangulationService.CreateTriangulator(triRoot, "Norm", $"sps_tri_{i}_front",
                                    new[] { HapticUtils.TagSpsSocketFront }, party, useHipAvoidance: plug.useHipAvoidance);
                                var isHole = hapticContacts.AddReceiver(triRoot, Vector3.zero, $"sps_tri_{i}_lightMarker",
                                    "IsHole", 3f, new[] { HapticUtils.TagSpsSocketIsHole },
                                    party, useHipAvoidance: plug.useHipAvoidance);
                                var isRing = hapticContacts.AddReceiver(triRoot, Vector3.zero, $"sps_tri_{i}_lightMarker",
                                    "IsRing", 3f, new[] { HapticUtils.TagSpsSocketIsRing },
                                    party, useHipAvoidance: plug.useHipAvoidance);
                            
                                foreach (var r in renderers) {
                                    _triangulationService.SendToShader(tri, $"_SPS_Tri_{prefix}_Root", r.renderer);
                                    _triangulationService.SendToShader(triFront, $"_SPS_Tri_{prefix}_Front", r.renderer);
                                    _triangulationService.SendParamToShader(isHole, $"_SPS_Tri_{prefix}_IsHole", r.renderer);
                                    _triangulationService.SendParamToShader(isRing, $"_SPS_Tri_{prefix}_IsRing", r.renderer);
                                }
                            }
                        }
                        
                        if (triangulationOnClip == null) {
                            var param = fx.NewBool("triangulationOn", synced: true);
                            manager.GetMenu()
                                .NewMenuToggle(
                                    $"{spsOptions.GetOptionsPath()}/<b>SPSLL (Alpha)<\\/b>\n<size=20>Use contacts instead of lights - Experimental!",
                                    param);
                            triangulationOnClip = fx.NewClip("TriangulationOn");
                            var layer = fx.NewLayer("Lightless SPS");
                            var off = layer.NewState("Off");
                            var on = layer.NewState("On").WithAnimation(triangulationOnClip);
                            var whenOn = param.IsTrue();
                            off.TransitionsTo(on).When(whenOn);
                            on.TransitionsTo(off).When(whenOn.Not());
                        }

                        clipBuilder.Enable(triangulationOnClip, triRoot);
                        foreach (var r in renderers) {
                            triangulationOnClip.SetConstant(
                                EditorCurveBinding.FloatCurve(r.renderer.owner().GetPath(avatarObject), typeof(SkinnedMeshRenderer), "material._SPS_Tri_Self_Enabled"),
                                1
                            );
                            triangulationOnClip.SetConstant(
                                EditorCurveBinding.FloatCurve(r.renderer.owner().GetPath(avatarObject), typeof(SkinnedMeshRenderer), "material._SPS_Tri_Other_Enabled"),
                                1
                            );
                            triangulationOnClip.SetConstant(
                                EditorCurveBinding.FloatCurve(r.renderer.owner().GetPath(avatarObject), typeof(SkinnedMeshRenderer), "material._SPS_Target_LL_Lights"),
                                0
                            );
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
                                    $"{spsOptions.GetOptionsPath()}/<b>DPS Tip Light<\\/b>\n<size=20>Allows plugs to trigger old DPS animations",
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
                            worldLength,
                            plug.useHipAvoidance
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
                    foreach (var (_binding,curve) in clip.GetAllCurves()) {
                        var binding = _binding;

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
                            clip.SetCurve(binding, null);
                            binding.type = typeof(SkinnedMeshRenderer);
                            clip.SetCurve(binding, curve);
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
                scaleCompensationService.AddScaledProp(light.gameObject,
                    new[] { (light.owner(), typeof(Light), "m_Intensity", light.intensity) });
        }
    }
}
