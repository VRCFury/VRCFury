using Party = VF.Builder.Haptics.SelfAndOthersReceiverService.Party;
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

        [VFAutowired] private readonly SelfAndOthersReceiverAnimatorService receiverAnimator;
        [VFAutowired] private readonly GlobalsService globals;
        [VFAutowired] private readonly VFGameObject avatarObject;
        [VFAutowired] private readonly ActionClipService actionClipService;
        [VFAutowired] private readonly RestingStateService restingState;
        [VFAutowired] private readonly SpsDepthAnimationService depthAnimations;
        [VFAutowired] private readonly ScalePropertyCompensationService scaleCompensationService;
        [VFAutowired] private readonly SpsOptionsService spsOptions;
        [VFAutowired] private readonly HapticContactsService hapticContacts;
        [VFAutowired] private readonly AllClipsService allClipsService;
        [VFAutowired] private readonly AvatarBindingStateService avatarBindingStateService;
        [VFAutowired] private readonly WorldScaleDetectorService worldScaleService;
        [VFAutowired] private readonly ObjectTreeService objectTrees;
        [VFAutowired] private readonly ControllersService controllers;
        [VFAutowired] private readonly ExpressionParamsService expressionParams;
        [VFAutowired] private readonly OgbEnabledService ogbEnabledService;
        [VFAutowired] private readonly VRCFuryHapticPlugBaker plugBaker;
        [VFAutowired] private readonly SpsPlugRadiusService plugRadiusService;
        [VFAutowired] private readonly SpsPlugMeasurementsService plugMeasurements;
        [VFAutowired] private readonly ParameterInjectService parameterInjectService;
        private VFAvatarController fx => controllers.GetFx();


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

                    if (BuildTargetUtils.IsDesktop()) {
                        var bakeInfo = plugBaker.Bake(plug, usedRenderers, deferMaterialConfig: true);
                        if (bakeInfo != null) {
                            bakeResults[plug] = bakeInfo;
                        }
                    }

                    var postBakeClip = actionClipService.LoadStateAdv("sps_postbake", plug.postBakeActions, plug.owner());
                    restingState.ApplyClipToRestingState(
                        postBakeClip.onClip.EvaluateMotion(1).FlattenToClip(VFMotionFlattenMode.DefaultVisibleClips),
                        owner: "Post-bake clip for plug on " + plug.owner().GetPath(avatarObject)
                    );
                } catch (Exception e) {
                    throw new ExceptionWithCause($"Failed to build SPS Plug: {plug.owner().GetDebugPath()}", e);
                }
            }
        }

        [FeatureBuilderAction]
        public void Apply() {
            var usedNames = new HashSet<string>();

            var plugs = avatarObject.GetComponentsInSelfAndChildren<VRCFuryHapticPlug>();
            var options = spsOptions.GetOptions();
            var addDpsTipLight = options.legacyModeAvailable && options.addDpsTipLight;

            foreach (var plug in plugs) {
                try {
                    if (!bakeResults.TryGetValue(plug, out var bakeInfo)) continue;
                    ApplyPlug(plug, bakeInfo, addDpsTipLight, usedNames);
                } catch (Exception e) {
                    throw new ExceptionWithCause($"Failed to build SPS Plug: {plug.owner().GetPath(avatarObject)}", e);
                }
            }
        }
        
        private void ApplyPlug(
            VRCFuryHapticPlug plug,
            VRCFuryHapticPlugEditor.BakeResult bakeInfo,
            bool addDpsTipLight,
            ISet<string> usedNames
        ) {
            var name = HapticUtils.MakeUniqueOgbId(usedNames, bakeInfo.oscId);
            Debug.Log("Baking haptic component in " + plug.owner().GetDebugPath() + " as " + name);

            var bakeRoot = bakeInfo.bakeRoot;
            var worldSpace = bakeInfo.worldSpace;
            var renderers = bakeInfo.renderers;
            var localLength = bakeInfo.localLength;
            var worldLength = bakeInfo.worldLength;
            var worldRadius = bakeInfo.worldRadius;
            var propsToScale = new List<(UnityEngine.Component, string, float)>();

            globals.addOtherFeature(new ShowInFirstPerson {
                useObjOverride = true,
                objOverride = bakeRoot,
                onlyIfChildOfHead = true
            });
            
            // Haptics
            if (HapticsToggleMenuItem.Get() && !plug.fromSpsForAll) {
                // Haptic Receivers
                var paramPrefix = "OGB/Plug/" + name;
                worldScaleService.GetRawWorldScale(bakeRoot, name, paramPrefix + "/LengthMult1");
                expressionParams.AddOrMerge(ExpressionParam.Create(paramPrefix + "/LengthMult2")
                    .Float().Default(worldLength * WorldScaleDetectorService.MaxDetectedScale).Unsynced());
                var haptics = GameObjects.Create("Haptics", bakeInfo.oneSpace);
                var halfWay = Vector3.forward * (worldLength / 2);
                var extraRadiusForTouch = Math.Min(worldRadius, 0.08f /* 8cm */);
                // Extra rub radius should always match for everyone, so when two plugs collide, both trigger at the same time
                var extraRadiusForRub = 0.08f;
                // This is *90 because capsule length is actually "height", so we have to rotate it to make it a length
                var capsuleRotation = Quaternion.Euler(90,0,0);

                var req = new HapticContactsService.ReceiverRequest() {
                    obj = haptics,
                    localOnly = true,
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
                foreach (var self in new[] { true, false }) {
                    receiverAnimator.GetParameter(bakeInfo.socketReceivers, self, paramPrefix + (self ? "/DepthSelf" : "/DepthOthers")
#if VRCSDK_HAS_CONTACT_PARAMETER_FLAGS
                        , ContactReceiver.AdvancedParameter.All
#endif
                    );
                }
                {
                    var c = req.Clone();
                    c.paramName = paramPrefix + "/FrotOthers";
                    c.objName = "FrotOthers";
                    c.radius = worldLength;
                    c.tags = new []{SpsTags.PlugClose};
                    c.party = HapticUtils.ReceiverParty.Others;
                    hapticContacts.AddReceiver(c);
                }
                {
                    var c = req.Clone();
                    c.pos = halfWay;
                    c.paramName = paramPrefix + "/FrotOthersClose";
                    c.objName = "FrotOthersClose";
                    c.radius = worldRadius+extraRadiusForRub;
                    c.tags = new []{SpsTags.PlugClose};
                    c.party = HapticUtils.ReceiverParty.Others;
                    c.rotation = capsuleRotation;
                    c.height = worldLength;
                    c.type = ContactReceiver.ReceiverType.Constant;
                    hapticContacts.AddReceiver(c);
                }
                ogbEnabledService.Register(haptics);
            }

            if (bakeInfo.mainSender != null) {
                plugRadiusService.Create(bakeInfo, name);
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
                        bakeRoot = bakeRoot,
                        configureMaterial = r.configureMaterial,
                        spsBlendshapes = r.spsBlendshapes
                    });
                }
            }
            
            // DPS Tip Light
            if (addDpsTipLight) {
                var tip = GameObjects.Create("LegacyDpsTip", worldSpace);
                var light = tip.AddComponent<Light>();
                light.type = LightType.Point;
                light.color = Color.black;
                light.range = 0.49f;
                light.shadows = LightShadows.None;
                light.renderMode = LightRenderMode.ForceVertex;
                light.intensity = worldLength;

                propsToScale.Add((light, "m_Intensity", localLength));

            }

            var injectRequests = parameterInjectService.GetRequests()
                .Where(r => r.sourceObject == plug.owner())
                .ToList();

            var math = new Lazy<BlendtreeMath>(() => new BlendtreeMath(fx, objectTrees.Get(bakeRoot)));
            var measurements = new Lazy<Func<Party, Func<string, VFAParam>>>(() =>
                plugMeasurements.GetMeasurements(bakeInfo, name));

            // Depth Actions / Injected SPS params
            if (plug.depthActions2.Count > 0) {
                depthAnimations.CreateAnims(
                    plug.depthActions2,
                    plug.owner(),
                    name,
                    party => measurements.Value(party),
                    bakeRoot
                );
            }
            foreach (var inject in injectRequests) {
                var value = measurements.Value(Party.Merged)(inject.sourceParam);
                if (value != null) {
                    math.Value.CopyInPlace(value, inject.resolvedParam);
                }
            }

            if (propsToScale.Count > 0) {
                scaleCompensationService.AddScaledProp(bakeRoot, propsToScale);
            }
        }
        
        public class SpsRewriteToDo {
            public VFGameObject plugObject;
            public Renderer skin;
            public VFGameObject bakeRoot;
            public Func<int, Material, Material> configureMaterial;
            public IList<string> spsBlendshapes;
        }
        private readonly List<SpsRewriteToDo> spsRewritesToDo = new List<SpsRewriteToDo>();

        [FeatureBuilderAction(FeatureOrder.HapticsAnimationRewrites)]
        public void ApplySpsRewrites() {
            foreach (var rewrite in spsRewritesToDo) {
                void RewriteClip(VFClip clip) {
                    foreach (var pair in clip.GetAllCurves()) {
                        var binding = pair.Item1;
                        var curve = pair.Item2;
                        var isRendererBinding = binding.Targets(rewrite.skin.owner());
                        var isPlugBinding = binding.Targets(rewrite.plugObject);
                        var finalBinding = binding;

                        if (curve.IsFloat && (
                            isRendererBinding && binding.propertyName == "material._TPS_AnimatedToggle"
                            || isPlugBinding && binding.propertyName == "spsAnimatedEnabled"
                        )) {
                            clip.SetCurve(rewrite.skin, $"material.{SpsConfigurer.SpsEnabled}", curve);
                            clip.SetEnabled(rewrite.bakeRoot, curve);
                        }

                        if (isRendererBinding
                            && binding.type == typeof(MeshRenderer)
                            && rewrite.skin is SkinnedMeshRenderer) {
                            finalBinding = binding.WithType(typeof(SkinnedMeshRenderer));
                            clip.SetCurve(binding, null);
                            clip.SetCurve(finalBinding, curve);
                        }

                        if (curve.IsFloat && isRendererBinding && finalBinding.type == typeof(SkinnedMeshRenderer) && finalBinding.propertyName.StartsWith("blendShape.")) {
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

                        if (!curve.IsFloat && isRendererBinding && avatarBindingStateService.TryParseMaterialSlot(finalBinding, out _, out var slotNum)) {
                            var newKeys = curve.ObjectCurve.Select(frame => {
                                if (frame.value is Material m) frame.value = rewrite.configureMaterial(slotNum, m);
                                return frame;
                            }).ToArray();
                            clip.SetCurve(finalBinding, newKeys);
                        }
                    }
                }

                try {
                    allClipsService.GetAllClips().ForEach(clip => {
                        try {
                            RewriteClip(clip);
                        } catch (Exception e) {
                            throw new ExceptionWithCause($"Failed to patch clip: {clip.name}", e);
                        }
                    });

                    rewrite.skin.sharedMaterials = rewrite.skin.sharedMaterials
                        .Select((mat,slotNum) => rewrite.configureMaterial(slotNum, mat))
                        .ToArray();
                } catch (Exception e) {
                    throw new ExceptionWithCause(
                        $"Failed to patch materials for SPS Plug: {rewrite.plugObject.GetDebugPath()}", e);
                }
            }
        }
    }
}
