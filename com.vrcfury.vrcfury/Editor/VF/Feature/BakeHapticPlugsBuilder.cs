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
using VF.Menu;
using VF.Model.Feature;
using VF.Service;
using VF.Utils;
using VF.Utils.Controller;
using VRC.Dynamics;

namespace VF.Feature {
    [VFService]
    internal class BakeHapticPlugsBuilder : FeatureBuilder {
        
        [VFAutowired] private readonly ActionClipService actionClipService;
        [VFAutowired] private readonly RestingStateService restingState;
        [VFAutowired] private readonly HapticAnimContactsService _hapticAnimContactsService;
        [VFAutowired] private readonly ForceStateInAnimatorService _forceStateInAnimatorService;
        [VFAutowired] private readonly ScalePropertyCompensationService scaleCompensationService;
        [VFAutowired] private readonly SpsOptionsService spsOptions;
        [VFAutowired] private readonly HapticContactsService hapticContacts;
        [VFAutowired] private readonly MathService math;
        [VFAutowired] private readonly DirectBlendTreeService directTree;
        [VFAutowired] private readonly UniqueHapticNamesService uniqueHapticNamesService;
        [VFAutowired] private readonly ClipRewriteService clipRewriteService;
        [VFAutowired] private readonly ClipFactoryService clipFactory;
        [VFAutowired] private readonly AvatarBindingStateService avatarBindingStateService;

        private readonly Dictionary<VRCFuryHapticPlug, VRCFuryHapticPlugEditor.BakeResult> bakeResults =
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
                    PhysboneUtils.RemoveFromPhysbones(plug.owner());
                    if (EditorUserBuildSettings.activeBuildTarget == BuildTarget.Android) continue;
                    var bakeInfo = VRCFuryHapticPlugEditor.Bake(
                        plug,
                        hapticContacts,
                        tmpDir,
                        usedRenderers,
                        deferMaterialConfig: true
                    );
                    if (bakeInfo == null) continue;
                    bakeResults[plug] = bakeInfo;

                    var postBakeClip = actionClipService.LoadState("sps_postbake", plug.postBakeActions, plug.owner());
                    restingState.ApplyClipToRestingState(postBakeClip, owner: "Post-bake clip for plug on " + plug.owner().GetPath(avatarObject));
                } catch (Exception e) {
                    throw new ExceptionWithCause($"Failed to bake SPS Plug: {plug.owner().GetPath(avatarObject)}", e);
                }
            }
        }

        [FeatureBuilderAction]
        public void Apply() {
            var fx = GetFx();

            AnimationClip tipLightOnClip = null;
            AnimationClip spsPlusClip = null;

            var plugs = avatarObject.GetComponentsInSelfAndChildren<VRCFuryHapticPlug>();

            if (plugs.Any(plug => plug.addDpsTipLight)) {
                var param = fx.NewBool("tipLight", synced: true);
                manager.GetMenu()
                    .NewMenuToggle(
                        $"{spsOptions.GetOptionsPath()}/<b>DPS Tip Light<\\/b>\n<size=20>Allows plugs to trigger old DPS animations",
                        param);
                tipLightOnClip = clipFactory.NewClip("EnableAutoReceivers");
                var layer = fx.NewLayer("Tip Light");
                var off = layer.NewState("Off");
                var on = layer.NewState("On").WithAnimation(tipLightOnClip);
                var whenOn = param.IsTrue();
                off.TransitionsTo(on).When(whenOn);
                on.TransitionsTo(off).When(whenOn.Not());
            }

            foreach (var plug in plugs) {
                try {
                    if (!bakeResults.TryGetValue(plug, out var bakeInfo)) continue;

                    var bakeRoot = bakeInfo.bakeRoot;
                    var renderers = bakeInfo.renderers;
                    var worldRadius = bakeInfo.worldRadius;
                    var worldLength = bakeInfo.worldLength;
                    
                    addOtherFeature(new ShowInFirstPerson {
                        useObjOverride = true,
                        objOverride = bakeRoot,
                        onlyIfChildOfHead = true
                    });

                    var name = plug.name;
                    if (string.IsNullOrWhiteSpace(name)) {
                        if (renderers.Count > 0) {
                            name = HapticUtils.GetName(renderers.First().renderer.owner());
                        } else {
                            name = HapticUtils.GetName(plug.owner());
                        }
                    }
                    name = uniqueHapticNamesService.GetUniqueName(name);
                    Debug.Log("Baking haptic component in " + plug.owner().GetPath() + " as " + name);
                    
                    if (HapticsToggleMenuItem.Get() && !plug.sendersOnly) {
                        // Haptic Receivers
                        var paramPrefix = "OGB/Pen/" + name.Replace('/','_');
                        var haptics = GameObjects.Create("Haptics", bakeRoot);
                        var halfWay = Vector3.forward * (worldLength / 2);
                        var extraRadiusForTouch = Math.Min(worldRadius, 0.08f /* 8cm */);
                        // Extra rub radius should always match for everyone, so when two plugs collide, both trigger at the same time
                        var extraRadiusForRub = 0.08f;
                        // This is *90 because capsule length is actually "height", so we have to rotate it to make it a length
                        var capsuleRotation = Quaternion.Euler(90,0,0);
                        hapticContacts.AddReceiver(haptics, halfWay, paramPrefix + "/TouchSelfClose", "TouchSelfClose", worldRadius+extraRadiusForTouch, HapticUtils.SelfContacts, HapticUtils.ReceiverParty.Self, usePrefix: false, localOnly:true, rotation: capsuleRotation, height: worldLength+extraRadiusForTouch*2, type: ContactReceiver.ReceiverType.Constant, useHipAvoidance: plug.useHipAvoidance);
                        hapticContacts.AddReceiver(haptics, Vector3.zero, paramPrefix + "/TouchSelf", "TouchSelf", worldLength+extraRadiusForTouch, HapticUtils.SelfContacts, HapticUtils.ReceiverParty.Self, usePrefix: false, localOnly:true, useHipAvoidance: plug.useHipAvoidance);
                        hapticContacts.AddReceiver(haptics, halfWay, paramPrefix + "/TouchOthersClose", "TouchOthersClose", worldRadius+extraRadiusForTouch, HapticUtils.BodyContacts, HapticUtils.ReceiverParty.Others, usePrefix: false, localOnly:true, rotation: capsuleRotation, height: worldLength+extraRadiusForTouch*2, type: ContactReceiver.ReceiverType.Constant, useHipAvoidance: plug.useHipAvoidance);
                        hapticContacts.AddReceiver(haptics, Vector3.zero, paramPrefix + "/TouchOthers", "TouchOthers", worldLength+extraRadiusForTouch, HapticUtils.BodyContacts, HapticUtils.ReceiverParty.Others, usePrefix: false, localOnly:true, useHipAvoidance: plug.useHipAvoidance);
                        hapticContacts.AddReceiver(haptics, Vector3.zero, paramPrefix + "/PenSelf", "PenSelf", worldLength, new []{HapticUtils.TagTpsOrfRoot}, HapticUtils.ReceiverParty.Self, usePrefix: false, localOnly:true, useHipAvoidance: plug.useHipAvoidance);
                        hapticContacts.AddReceiver(haptics, Vector3.zero, paramPrefix + "/PenOthers", "PenOthers", worldLength, new []{HapticUtils.TagTpsOrfRoot}, HapticUtils.ReceiverParty.Others, usePrefix: false, localOnly:true, useHipAvoidance: plug.useHipAvoidance);
                        hapticContacts.AddReceiver(haptics, Vector3.zero, paramPrefix + "/FrotOthers", "FrotOthers", worldLength, new []{HapticUtils.CONTACT_PEN_CLOSE}, HapticUtils.ReceiverParty.Others, usePrefix: false, localOnly:true, useHipAvoidance: plug.useHipAvoidance);
                        hapticContacts.AddReceiver(haptics, halfWay, paramPrefix + "/FrotOthersClose", "FrotOthersClose", worldRadius+extraRadiusForRub, new []{HapticUtils.CONTACT_PEN_CLOSE}, HapticUtils.ReceiverParty.Others, usePrefix: false, localOnly:true, rotation: capsuleRotation, height: worldLength, type: ContactReceiver.ReceiverType.Constant, useHipAvoidance: plug.useHipAvoidance);
                    }

                    if (plug.configureTps || plug.enableSps) {
                        foreach (var r in renderers) {
                            var renderer = r.renderer;
                            addOtherFeature(new TpsScaleFix() { singleRenderer = renderer });
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

                    if (plug.enableSps) {
                        var plusRoot = GameObjects.Create("SpsPlus", bakeRoot);
                        plusRoot.active = false;
                        plusRoot.worldScale = Vector3.one;
                        if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android) {
                            var p = plusRoot.AddComponent<ScaleConstraint>();
                            p.AddSource(new ConstraintSource() {
                                sourceTransform = VRCFuryEditorUtils.GetResource<Transform>("world.prefab"),
                                weight = 1
                            });
                            p.weight = 1;
                            p.constraintActive = true;
                            p.locked = true;

                            VFAFloat CreateReceiver(string tag, bool self) {
                                return hapticContacts.AddReceiver(
                                    plusRoot,
                                    Vector3.zero,
                                    $"spsll_{tag}_{(self ? "self" : "others")}",
                                    $"{tag}{(self ? "Self" : "Others")}",
                                    3f,
                                    new[] { tag },
                                    self ? HapticUtils.ReceiverParty.Self : HapticUtils.ReceiverParty.Others,
                                    useHipAvoidance: plug.useHipAvoidance);
                            }
                            void SendParam(string shaderParam, string tag) {
                                var oneClip = clipFactory.NewClip($"{shaderParam}_one");
                                foreach (var r in renderers.Select(r => r.renderer)) {
                                    var path = r.owner().GetPath(manager.AvatarObject);
                                    var binding = EditorCurveBinding.FloatCurve(path, r.GetType(), $"material.{shaderParam}");
                                    oneClip.SetCurve(binding, 1);
                                }

                                var self = CreateReceiver(tag, true);
                                var selfBlend = directTree.Create("selfBlend");
                                selfBlend.Add(self, oneClip);
                                var others = CreateReceiver(tag, false);
                                var othersBlend = directTree.Create("othersBlend");
                                othersBlend.Add(others, oneClip);
                                directTree.Add(math.GreaterThan(self, others).create(selfBlend, othersBlend));
                            }

                            SendParam("_SPS_Plus_Ring", HapticUtils.TagSpsSocketIsRing);
                            SendParam("_SPS_Plus_Hole", HapticUtils.TagSpsSocketIsHole);
                        }
                        
                        if (spsPlusClip == null) {
                            spsPlusClip = clipFactory.NewClip("SpsPlus");
                            directTree.Add(spsPlusClip);
                        }

                        clipBuilder.Enable(spsPlusClip, plusRoot);
                        foreach (var r in renderers) {
                            spsPlusClip.SetCurve(
                                EditorCurveBinding.FloatCurve(r.renderer.owner().GetPath(avatarObject), typeof(SkinnedMeshRenderer), "material._SPS_Plus_Enabled"),
                                1
                            );
                        }
                    }

                    if (tipLightOnClip != null) {
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
                    throw new ExceptionWithCause($"Failed to bake SPS Plug: {plug.owner().GetPath(avatarObject)}", e);
                }
            }
        }
        
        public class SpsRewriteToDo {
            public VFGameObject plugObject;
            public SkinnedMeshRenderer skin;
            public VFGameObject bakeRoot;
            public Func<int, Material, Material> configureMaterial;
            public IList<string> spsBlendshapes;
        }
        private readonly List<SpsRewriteToDo> spsRewritesToDo = new List<SpsRewriteToDo>();
        private readonly List<Light> dpsTipToDo = new List<Light>();

        [FeatureBuilderAction(FeatureOrder.HapticsAnimationRewrites)]
        public void ApplySpsRewrites() {
            foreach (var rewrite in spsRewritesToDo) {
                var pathToPlug = rewrite.plugObject.GetPath(avatarObject);
                var pathToRenderer = rewrite.skin.owner().GetPath(avatarObject);
                var pathToBake = rewrite.bakeRoot.GetPath(avatarObject);
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

                        if (!curve.IsFloat && binding.path == pathToRenderer && avatarBindingStateService.TryParseMaterialSlot(binding, out _, out var slotNum)) {
                            var newKeys = curve.ObjectCurve.Select(frame => {
                                if (frame.value is Material m) frame.value = rewrite.configureMaterial(slotNum, m);
                                return frame;
                            }).ToArray();
                            clip.SetCurve(binding, newKeys);
                        }
                    }
                }
                clipRewriteService.ForAllClips(RewriteClip);

                rewrite.skin.sharedMaterials = rewrite.skin.sharedMaterials
                    .Select((mat,slotNum) => rewrite.configureMaterial(slotNum, mat))
                    .ToArray();
            }
        }

        [FeatureBuilderAction(FeatureOrder.DpsTipScaleFix)]
        public void ApplyDpsTipScale() {
            foreach (var light in dpsTipToDo)
                scaleCompensationService.AddScaledProp(light.owner(),
                    new[] { (light.owner(), typeof(Light), "m_Intensity", light.intensity) });
        }
    }
}
