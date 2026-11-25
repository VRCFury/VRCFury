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

namespace VF.Service {
    [VFService]
    internal class BakeHapticPlugsService {

        [VFAutowired] private readonly SelfAndOthersReceiverAnimatorService receiverAnimator;
        [VFAutowired] private readonly GlobalsService globals;
        [VFAutowired] private readonly VFGameObject avatarObject;
        [VFAutowired] private readonly ActionClipService actionClipService;
        [VFAutowired] private readonly RestingStateService restingState;
        [VFAutowired] private readonly SpsDepthAnimationService depthAnimations;
        [VFAutowired] private readonly SpsOptionsService spsOptions;
        [VFAutowired] private readonly HapticContactsService hapticContacts;
        [VFAutowired] private readonly AllClipsService allClipsService;
        [VFAutowired] private readonly AvatarBindingStateService avatarBindingStateService;
        [VFAutowired] private readonly WorldScaleDetectorService worldScaleService;
        [VFAutowired] private readonly ObjectTreeService objectTrees;
        [VFAutowired] private readonly IsObjectEnabledService objectEnabled;
        [VFAutowired] private readonly DbtLayerService directTreeService;
        [VFAutowired] private readonly ControllersService controllers;
        [VFAutowired] private readonly ExpressionParamsService expressionParams;
        [VFAutowired] private readonly VRCFuryHapticPlugBaker plugBaker;
        [VFAutowired] private readonly SpsChannelsService channels;
        [VFAutowired] private readonly SpsPlugRadiusService plugRadiusService;
        [VFAutowired] private readonly SpsPlugMeasurementsService plugMeasurements;
        [VFAutowired] private readonly ParameterInjectService parameterInjectService;
        private VFAvatarController fx => controllers.GetFx();


        private readonly Dictionary<VRCFuryHapticPlug, VRCFuryHapticPlugEditor.BakeResult> bakeResults =
            new Dictionary<VRCFuryHapticPlug, VRCFuryHapticPlugEditor.BakeResult>();
        private readonly List<Action> finishRendererSwaps = new();

        public IEnumerable<(VRCFuryHapticPlug plug, VFGameObject bakeRoot)> GetBakedPlugs() =>
            bakeResults.Select(pair => (pair.Key, pair.Value.bakeRoot));

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

                    bakeResults[plug] = plugBaker.Bake(
                        plug,
                        usedRenderers,
                        deferMaterialConfig: true,
                        legacySocketCompatibility: spsOptions.GetOptions().legacySocketCompatibility,
                        finishRendererSwaps: finishRendererSwaps
                    );
                    bakeResults[plug].bakeRoot.active = plug.spsAnimatedEnabled > 0;
                    channels.Register(plug, new Lazy<VFBlendTreeDirect>(() => objectTrees.Get(bakeResults[plug].bakeRoot)));

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
            if (!BuildTargetUtils.IsDesktop()) return;
            var usedNames = new HashSet<string>();

            var plugs = avatarObject.GetComponentsInSelfAndChildren<VRCFuryHapticPlug>();
            var options = spsOptions.GetOptions();
            var addDpsTipLight = options.legacySocketCompatibility && options.addDpsTipLight;

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

            var bakeRoot = bakeInfo.bakeRoot;
            var renderers = bakeInfo.renderers;
            var localLength = bakeInfo.localLength;
            var worldLength = bakeInfo.worldLength;
            var worldRadius = bakeInfo.worldRadius;

            globals.addOtherFeature(new ShowInFirstPerson {
                useObjOverride = true,
                objOverride = bakeRoot,
                onlyIfChildOfHead = true
            });
            
            // Haptics
            if (HapticsToggleMenuItem.Get()) {
                // Haptic Receivers
                var paramPrefix = "OGB/Plug/" + name;
                worldScaleService.GetRawWorldScale(bakeRoot, name, paramPrefix + "/LengthMult1");
                expressionParams.AddOrMerge(ExpressionParam.Create(paramPrefix + "/LengthMult2")
                    .Float().Default(worldLength * WorldScaleDetectorService.MaxDetectedScale).Unsynced());
                var halfWay = Vector3.forward * (worldLength / 2);
                var extraRadiusForTouch = Math.Min(worldRadius, 0.08f /* 8cm */);
                // Extra rub radius should always match for everyone, so when two plugs collide, both trigger at the same time
                var extraRadiusForRub = 0.08f;

                var req = new HapticContactsService.ReceiverRequest() {
                    obj = bakeRoot,
                    pos = halfWay,
                    // The SDK measures toward +Z. Face the base so deeper touches read higher.
                    rotation = Quaternion.Euler(0, 180, 0),
                    useFaceProximity = true,
                    enableWithOgb = true,
                    localOnly = true,
                    usePrefix = false
                };
                foreach (var self in new[] { true, false }) {
                    var c = req.Clone();
                    c.paramName = paramPrefix + (self ? "/TouchSelf" : "/TouchOthers");
                    c.objName = self ? "Receiver - Touch (Self)" : "Receiver - Touch (Others)";
                    c.size = new Vector3(2 * (worldRadius + extraRadiusForTouch),
                        2 * (worldRadius + extraRadiusForTouch), worldLength);
                    c.tags = HapticUtils.SelfContacts;
                    c.party = self ? HapticUtils.ReceiverParty.Self : HapticUtils.ReceiverParty.Others;
                    hapticContacts.AddReceiver(c);
                }
                receiverAnimator.RegisterOgbDepth(bakeInfo.socketReceivers, paramPrefix);
                {
                    var c = req.Clone();
                    c.paramName = paramPrefix + "/FrotOthers";
                    c.objName = "Receiver - Plugs (Others)";
                    c.size = new Vector3(2 * (worldRadius + extraRadiusForRub),
                        2 * (worldRadius + extraRadiusForRub), worldLength);
                    c.tags = new []{SpsTags.PlugClose};
                    c.party = HapticUtils.ReceiverParty.Others;
                    hapticContacts.AddReceiver(c);
                }

            }

            if (bakeInfo.mainSender != null) {
                plugRadiusService.Create(bakeInfo, name);
            }

            // SPS
            if (plug.enableSps) {
                var on = VFClip.Create("SPS Plug Object On");
                var off = VFClip.Create("SPS Plug Object Off");
                foreach (var r in renderers) {
                    on.SetCurve(r.renderer, "material." + SpsConfigurer.SpsObjectEnabled, 1);
                    off.SetCurve(r.renderer, "material." + SpsConfigurer.SpsObjectEnabled, 0);
                    spsRewritesToDo.Add(new SpsRewriteToDo {
                        plugObject = plug.owner(),
                        skin = r.renderer,
                        bakeRoot = bakeRoot,
                        configureMaterial = r.configureMaterial,
                        spsBlendshapes = r.spsBlendshapes
                    });
                }
                // Disabled contacts leave their material data intact, even on a visible renderer.
                // Keep this writer outside the plug's guarded calculation tree so it writes zero when off.
                directTreeService.Create($"{name} - Plug Object Enabled").Add(
                    BlendtreeMath.GreaterThan(objectEnabled.Get(bakeRoot), 0).create(on, off)
                );
            }
            
            // DPS Tip Light
            if (addDpsTipLight) {
                bakeInfo.channelContacts.OnAllSetsAvailable(sets => {
                    var publicSets = sets.Where(s => s.isPublic).ToArray();
                    if (publicSets.Length == 0) return;
                    var tip = GameObjects.Create("Light - Legacy DPS Trigger", bakeRoot);
                    var light = tip.AddComponent<Light>();
                    light.type = LightType.Point;
                    light.color = Color.black;
                    light.range = 0.49f;
                    light.shadows = LightShadows.None;
                    light.renderMode = LightRenderMode.ForceVertex;
                    light.intensity = worldLength;

                    worldScaleService.AddScaledProp(bakeRoot,
                        new[] { ((UnityEngine.Component)light, "m_Intensity", localLength) });
                    spsOptions.RegisterLegacyObject(tip);
                    foreach (var set in publicSets) set.Register(tip);
                });
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
                    bakeRoot,
                    weight: GetEnabledWeight(plug)
                );
            }
            foreach (var inject in injectRequests) {
                var value = measurements.Value(Party.Merged)(inject.sourceParam);
                if (value != null) {
                    math.Value.CopyInPlace(value, inject.resolvedParam);
                    parameterInjectService.MarkResolved(inject);
                }
            }

            if (bakeInfo.pathReceiverRoot != null) {
                var self = receiverAnimator.GetParameter(bakeInfo.socketReceivers, Party.Self);
                var others = receiverAnimator.GetParameter(bakeInfo.socketReceivers, Party.Others);
                var hasSocket = BlendtreeMath.GreaterThan(self, 0).Or(BlendtreeMath.GreaterThan(others, 0));
                var pathRoot = bakeInfo.pathReceiverRoot;
                pathRoot.active = false;
                // Channel changes control the individual variants under this shared gate.
                var on = VFClip.Create("Enable SPS Path Receivers");
                on.SetEnabled(pathRoot, true);
                var off = VFClip.Create("Disable SPS Path Receivers");
                off.SetEnabled(pathRoot, false);
                objectTrees.Get(bakeRoot).Add(hasSocket.create(on, off));
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
        private readonly Dictionary<VRCFuryHapticPlug, VFAParam> enabledWeights = new();

        private VFAParam GetEnabledWeight(VRCFuryHapticPlug plug) => enabledWeights.GetOrCreate(plug, () => {
            var weight = fx.CreateUniqueFloat(plug.name + "/SPS Enabled", plug.spsAnimatedEnabled);
            var on = VFClip.Create("Enable SPS");
            var off = VFClip.Create("Disable SPS");
            on.SetEnabled(bakeResults[plug].bakeRoot, true);
            off.SetEnabled(bakeResults[plug].bakeRoot, false);
            directTreeService.Create(plug.name + " - SPS Enabled").Add(
                BlendtreeMath.GreaterThan(weight, 0).create(on, off)
            );
            return weight;
        });

        [FeatureBuilderAction(FeatureOrder.HapticsAnimationRewrites)]
        public void ApplySpsRewrites() {
            foreach (var finish in finishRendererSwaps) finish();
            finishRendererSwaps.Clear();

            // Enable SPS also controls non-deforming plugs, which have no renderer rewrites.
            foreach (var clip in allClipsService.GetAllClips()) {
                foreach (var (binding, curve) in clip.GetFloatCurves()) {
                    if (binding.type != typeof(VRCFuryHapticPlug) || binding.propertyName != "spsAnimatedEnabled") continue;
                    var plug = binding.target?.GetComponent<VRCFuryHapticPlug>();
                    if (plug != null && bakeResults.ContainsKey(plug)) clip.SetAap(GetEnabledWeight(plug), curve);
                }
            }
            foreach (var rewrite in spsRewritesToDo) {
                var rendererObject = rewrite.skin.owner();
                void RewriteClip(VFClip clip) {
                    foreach (var pair in clip.GetAllCurves(
                        binding => binding.Targets(rendererObject) || binding.Targets(rewrite.plugObject)
                    )) {
                        var binding = pair.Item1;
                        var curve = pair.Item2;
                        var isRendererBinding = binding.Targets(rendererObject);
                        var isPlugBinding = binding.Targets(rewrite.plugObject);
                        var finalBinding = binding;

                        if (curve.IsFloat && (
                            isRendererBinding && binding.propertyName == "material._TPS_AnimatedToggle"
                            || isPlugBinding && binding.propertyName == "spsAnimatedEnabled"
                        )) {
                            clip.SetCurve(rewrite.skin, $"material.{SpsConfigurer.SpsEnabled}", curve);
                            clip.SetAap(GetEnabledWeight(rewrite.plugObject.GetComponent<VRCFuryHapticPlug>()), curve);
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
