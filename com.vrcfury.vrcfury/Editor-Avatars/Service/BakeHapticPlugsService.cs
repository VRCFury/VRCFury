using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using VF.Builder;
using VF.Builder.Haptics;
using VF.Component;
using VF.Exceptions;
using VF.Feature.Base;
using VF.Injector;
using VF.Inspector;
using VF.Menu;
using VF.Model.Feature;
using VF.Utils;
using VF.Utils.Controller;
using VRC.Dynamics;

namespace VF.Service {
    [VFService]
    internal class BakeHapticPlugsService {

        [VFAutowired] private readonly GlobalsService globals;
        [VFAutowired] private readonly VFGameObject avatarObject;
        [VFAutowired] private readonly ActionClipService actionClipService;
        [VFAutowired] private readonly RestingStateService restingState;
        [VFAutowired] private readonly HapticAnimContactsService _hapticAnimContactsService;
        [VFAutowired] private readonly ScalePropertyCompensationService scaleCompensationService;
        [VFAutowired] private readonly SpsOptionsService spsOptions;
        [VFAutowired] private readonly HapticContactsService hapticContacts;
        [VFAutowired] private readonly DbtLayerService directTreeService;
        [VFAutowired] private readonly AllClipsService allClipsService;
        [VFAutowired] private readonly ClipFactoryService clipFactory;
        [VFAutowired] private readonly AvatarBindingStateService avatarBindingStateService;
        [VFAutowired] private readonly ScaleFactorService scaleFactorService;
        [VFAutowired] private readonly ControllersService controllers;
        [VFAutowired] private readonly FrameTimeService frameTimeService;
        [VFAutowired] private readonly OgbEnabledService ogbEnabledService;
        [VFAutowired] private readonly SpsPlayerIdService spsPlayerIdService;
        [VFAutowired] private readonly SpsMarkersService spsMarkersService;
        private ControllerManager fx => controllers.GetFx();
        [VFAutowired] private readonly MenuService menuService;
        private MenuManager menu => menuService.GetMenu();
        private readonly Lazy<AnimationClip> materialPropertiesClip;

        public BakeHapticPlugsService() {
            materialPropertiesClip = new Lazy<AnimationClip>(() => {
                var clip = clipFactory.NewClip("SpsPlugMarkerProperties");
                directTreeService.Create("SPS Plug Marker Properties").Add(clip);
                return clip;
            });
        }

        private void RegisterMaterialProperties(IEnumerable<SpsConfigurer.MaterialProperty> properties) {
            SpsConfigurer.AddMaterialPropertyCurves(materialPropertiesClip.Value, avatarObject, properties);
        }

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
                    if (!BuildTargetUtils.IsDesktop()) continue;
                    var bakeInfo = VRCFuryHapticPlugEditor.Bake(
                        plug,
                        spsMarkersService,
                        usedRenderers,
                        deferMaterialConfig: true
                    );
                    if (bakeInfo == null) continue;
                    bakeResults[plug] = bakeInfo;

                    var postBakeClip = actionClipService.LoadStateAdv("sps_postbake", plug.postBakeActions, plug.owner());
                    restingState.ApplyClipToRestingState(postBakeClip.onClip.FlattenAll(), owner: "Post-bake clip for plug on " + plug.owner().GetPath(avatarObject));
                } catch (Exception e) {
                    throw new ExceptionWithCause($"Failed to bake SPS Plug: {plug.owner().GetPath(avatarObject)}", e);
                }
            }
        }

        [FeatureBuilderAction]
        public void Apply() {
            AnimationClip tipLightOnClip = null;
            var usedNames = new HashSet<string>();

            var plugs = avatarObject.GetComponentsInSelfAndChildren<VRCFuryHapticPlug>();

            if (plugs.Any(plug => plug.addDpsTipLight)) {
                var param = fx.NewBool("tipLight", synced: true);
                menu.NewMenuToggle(
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
                    ApplyPlug(plug, bakeInfo, tipLightOnClip, usedNames);
                } catch (Exception e) {
                    throw new ExceptionWithCause($"Failed to bake SPS Plug: {plug.owner().GetPath(avatarObject)}", e);
                }
            }
        }
        
        private void ApplyPlug(VRCFuryHapticPlug plug, VRCFuryHapticPlugEditor.BakeResult bakeInfo, AnimationClip tipLightOnClip, ISet<string> usedNames) {
            var bakeRoot = bakeInfo.bakeRoot;
            var worldSpace = bakeInfo.worldSpace;
            var renderers = bakeInfo.renderers;
            var worldRadius = bakeInfo.worldRadius;
            var worldLength = bakeInfo.worldLength;
            var localLength = worldLength / bakeRoot.worldScale.x;
            var propsToScale = new List<(UnityEngine.Component, string, float)>();
            var worldScale = new Lazy<VFAFloat>(() => scaleFactorService.GetWorldScale(bakeRoot));
            
            globals.addOtherFeature(new ShowInFirstPerson {
                useObjOverride = true,
                objOverride = bakeRoot,
                onlyIfChildOfHead = true
            });

            var name = HapticUtils.MakeUniqueId(usedNames, bakeInfo.oscId);
            Debug.Log("Baking haptic component in " + plug.owner().GetPath() + " as " + name);
            
            // Haptics
            if (HapticsToggleMenuItem.Get() && !plug.fromSpsForAll) {
                // Haptic Receivers
                var paramPrefix = "OGB/Pen/" + name.Replace('/','_');
                var haptics = GameObjects.Create("Haptics", bakeRoot);
                var halfWay = Vector3.forward * (worldLength / 2);
                var extraRadiusForTouch = Math.Min(worldRadius, 0.08f /* 8cm */);
                // Extra rub radius should always match for everyone, so when two plugs collide, both trigger at the same time
                var extraRadiusForRub = 0.08f;
                // This is *90 because capsule length is actually "height", so we have to rotate it to make it a length
                var capsuleRotation = Quaternion.Euler(90,0,0);

                var req = new HapticContactsService.ReceiverRequest() {
                    obj = haptics,
                    localOnly = true,
                    useHipAvoidance = plug.useHipAvoidance,
                    usePrefix = false
                };
                {
                    var c = req.Clone();
                    c.pos = halfWay;
                    c.paramName = paramPrefix + "/TouchSelfClose";
                    c.objName = "TouchSelfClose";
                    c.radius = worldRadius + extraRadiusForTouch;
                    c.tags = HapticUtils.SelfContacts;
                    c.party = HapticUtils.ReceiverParty.Self;
                    c.rotation = capsuleRotation;
                    c.height = worldLength + extraRadiusForTouch * 2;
                    c.type = ContactReceiver.ReceiverType.Constant;
                    hapticContacts.AddReceiver(c);
                    c.paramName = paramPrefix + "/TouchOthersClose";
                    c.objName = "TouchOthersClose";
                    c.party = HapticUtils.ReceiverParty.Others;
                    hapticContacts.AddReceiver(c);
                }
                {
                    var c = req.Clone();
                    c.paramName = paramPrefix + "/TouchSelf";
                    c.objName = "TouchSelf";
                    c.radius = worldLength + extraRadiusForTouch;
                    c.tags = HapticUtils.SelfContacts;
                    c.party = HapticUtils.ReceiverParty.Self;
                    hapticContacts.AddReceiver(c);
                    c.paramName = paramPrefix + "/TouchOthers";
                    c.objName = "TouchOthers";
                    c.party = HapticUtils.ReceiverParty.Others;
                    hapticContacts.AddReceiver(c);
                }
                {
                    var c = req.Clone();
                    c.paramName = paramPrefix + "/PenSelf";
                    c.objName = "PenSelf";
                    c.radius = worldLength;
                    c.tags = new []{HapticUtils.TagTpsOrfRoot};
                    c.party = HapticUtils.ReceiverParty.Self;
                    hapticContacts.AddReceiver(c);
                    c.paramName = paramPrefix + "/PenOthers";
                    c.objName = "PenOthers";
                    c.party = HapticUtils.ReceiverParty.Others;
                    hapticContacts.AddReceiver(c);
                }
                {
                    var c = req.Clone();
                    c.paramName = paramPrefix + "/FrotOthers";
                    c.objName = "FrotOthers";
                    c.radius = worldLength;
                    c.tags = new []{HapticUtils.CONTACT_PEN_CLOSE};
                    c.party = HapticUtils.ReceiverParty.Others;
                    hapticContacts.AddReceiver(c);
                }
                {
                    var c = req.Clone();
                    c.pos = halfWay;
                    c.paramName = paramPrefix + "/FrotOthersClose";
                    c.objName = "FrotOthersClose";
                    c.radius = worldRadius+extraRadiusForRub;
                    c.tags = new []{HapticUtils.CONTACT_PEN_CLOSE};
                    c.party = HapticUtils.ReceiverParty.Others;
                    c.rotation = capsuleRotation;
                    c.height = worldLength;
                    c.type = ContactReceiver.ReceiverType.Constant;
                    hapticContacts.AddReceiver(c);
                }
                ogbEnabledService.Register(haptics);
            }

            // TPS
            if (plug.configureTps) {
                foreach (var r in renderers) {
                    var renderer = r.renderer;
                    globals.addOtherFeature(new TpsScaleFix() { singleRenderer = renderer });
                }
            }

            // SPS
            if (plug.enableSps) {
                foreach (var r in renderers) {
                    spsRewritesToDo.Add(new SpsRewriteToDo {
                        plugObject = plug.owner(),
                        skin = r.renderer,
                        resolverRenderer = bakeInfo.resolverRenderer,
                        bakeRoot = bakeRoot,
                        configureMaterial = r.configureMaterial,
                        spsBlendshapes = r.spsBlendshapes
                    });
                    spsPlayerIdService.Register(r.renderer);
                }

                if (bakeInfo.resolverRenderer != null) {
                    RegisterMaterialProperties(bakeInfo.resolverMaterialProperties);
                    spsPlayerIdService.Register(bakeInfo.resolverRenderer);
                }
            }
            
            // DPS Tip Light
            if (tipLightOnClip != null) {
                var tip = GameObjects.Create("LegacyDpsTip", worldSpace);
                tip.active = false;
                var light = tip.AddComponent<Light>();
                light.type = LightType.Point;
                light.color = Color.black;
                light.range = 0.49f;
                light.shadows = LightShadows.None;
                light.renderMode = LightRenderMode.ForceVertex;
                light.intensity = worldLength;

                propsToScale.Add((light, "m_Intensity", localLength));

                tipLightOnClip.SetEnabled(tip, true);
            }

            // Depth Actions
            if (plug.depthActions2.Count > 0) {
                var directTree = directTreeService.Create($"{name} - Depth Calculations");
                var math = directTreeService.GetMath(directTree);
                var contacts = new SpsDepthContacts(
                    worldSpace,
                    name,
                    hapticContacts,
                    directTree,
                    math,
                    fx,
                    frameTimeService,
                    plug.useHipAvoidance,
                    worldScale.Value,
                    localLength
                );
                _hapticAnimContactsService.CreateAnims(
                    $"{name} - Depth Animations",
                    plug.depthActions2,
                    plug.owner(),
                    name,
                    contacts
                );
            }

            if (propsToScale.Count > 0) {
                scaleCompensationService.AddScaledProp(worldScale.Value, propsToScale);
            }
        }
        
        public class SpsRewriteToDo {
            public VFGameObject plugObject;
            public Renderer skin;
            public MeshRenderer resolverRenderer;
            public VFGameObject bakeRoot;
            public Func<int, Material, Material> configureMaterial;
            public IList<string> spsBlendshapes;
        }
        private readonly List<SpsRewriteToDo> spsRewritesToDo = new List<SpsRewriteToDo>();

        [FeatureBuilderAction(FeatureOrder.HapticsAnimationRewrites)]
        public void ApplySpsRewrites() {
            foreach (var rewrite in spsRewritesToDo) {
                var pathToPlug = rewrite.plugObject.GetPath(avatarObject);
                var pathToRenderer = rewrite.skin.owner().GetPath(avatarObject);

                void RewriteClip(AnimationClip clip) {
                    foreach (var pair in clip.GetAllCurves()) {
                        var binding = pair.Item1;
                        var curve = pair.Item2;

                        if (curve.IsFloat) {
                            if (binding.path == pathToRenderer) {
                                if (binding.propertyName == "material._TPS_AnimatedToggle") {
                                    if (rewrite.resolverRenderer != null) {
                                        clip.SetCurve(rewrite.resolverRenderer, $"material.{SpsConfigurer.SpsEnabled}", curve);
                                    }
                                    clip.SetEnabled(rewrite.bakeRoot, curve);
                                }
                                if (rewrite.resolverRenderer != null && SpsConfigurer.PropagateToResolver(binding.propertyName)) {
                                    clip.SetCurve(rewrite.resolverRenderer, binding.propertyName, curve);
                                }
                            }
                            if (binding.path == pathToPlug) {
                                if (binding.propertyName == "spsAnimatedEnabled") {
                                    if (rewrite.resolverRenderer != null) {
                                        clip.SetCurve(rewrite.resolverRenderer, $"material.{SpsConfigurer.SpsEnabled}", curve);
                                    }
                                    clip.SetEnabled(rewrite.bakeRoot, curve);
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
                                    rewrite.skin,
                                    $"material._SPS_Blendshape{blendshapeMeshIndex}",
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
                allClipsService.GetAllClips().ForEach(RewriteClip);

                rewrite.skin.sharedMaterials = rewrite.skin.sharedMaterials
                    .Select((mat,slotNum) => rewrite.configureMaterial(slotNum, mat))
                    .ToArray();
            }
        }
    }
}
