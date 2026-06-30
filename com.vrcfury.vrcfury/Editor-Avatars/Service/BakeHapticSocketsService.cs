using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using VF.Builder;
using VF.Builder.Haptics;
using VF.Component;
using VF.Exceptions;
using VF.Feature;
using VF.Feature.Base;
using VF.Injector;
using VF.Inspector;
using VF.Menu;
using VF.Model.Feature;
using VF.Utils;
using VF.Utils.Controller;
using VRC.Dynamics;
using VRC.SDK3.Avatars.Components;
using VRC.SDK3.Dynamics.Constraint.Components;
using VRC.SDK3.Dynamics.Contact.Components;

namespace VF.Service {
    [VFService]
    internal class BakeHapticSocketsService {

        [VFAutowired] private readonly GlobalsService globals;
        [VFAutowired] private readonly VFGameObject avatarObject;
        [VFAutowired] private readonly ActionClipService actionClipService;
        [VFAutowired] private readonly HapticAnimContactsService _hapticAnimContactsService;
        [VFAutowired] private readonly ForceStateInAnimatorService _forceStateInAnimatorService;
        [VFAutowired] private readonly SpsOptionsService spsOptions;
        [VFAutowired] private readonly HapticContactsService hapticContacts;
        [VFAutowired] private readonly DbtLayerService directTreeService;
        [VFAutowired] private readonly ClipFactoryService clipFactory;
        [VFAutowired] private readonly ScaleFactorService scaleFactorService;
        [VFAutowired] private readonly VRCAvatarDescriptor avatar;
        [VFAutowired] private readonly ControllersService controllers;
        [VFAutowired] private readonly FrameTimeService frameTimeService;
        [VFAutowired] private readonly OgbEnabledService ogbEnabledService;
        [VFAutowired] private readonly SpsPlayerIdService spsPlayerIdService;
        [VFAutowired] private readonly SpsMarkersService spsMarkersService;
        private ControllerManager fx => controllers.GetFx();
        [VFAutowired] private readonly MenuService menuService;
        private MenuManager menu => menuService.GetMenu();
        private readonly Lazy<AnimationClip> materialPropertiesClip;

        public BakeHapticSocketsService() {
            materialPropertiesClip = new Lazy<AnimationClip>(() => {
                var clip = clipFactory.NewClip("SpsSocketMarkerProperties");
                directTreeService.Create("SPS Socket Marker Properties").Add(clip);
                return clip;
            });
        }

        private void RegisterMaterialProperties(IEnumerable<SpsConfigurer.MaterialProperty> properties) {
            SpsConfigurer.AddMaterialPropertyCurves(materialPropertiesClip.Value, avatarObject, properties);
        }

        [FeatureBuilderAction]
        public void Apply() {
            var saved = spsOptions.GetOptions().saveSockets;

            var enableAuto = avatarObject.GetComponentsInSelfAndChildren<VRCFuryHapticSocket>()
                .Where(o => o.addMenuItem && o.enableAuto)
                .ToArray()
                .Length >= 2;
            VFABool autoOn = null;
            AnimationClip autoOnClip = null;
            if (enableAuto) {
                autoOn = fx.NewBool("autoMode", synced: true, networkSynced: false, saved: saved);
                menu.NewMenuToggle($"{spsOptions.GetOptionsPath()}/<b>Auto Mode<\\/b>\n<size=20>Activates hole nearest to a VRCFury plug", autoOn);
                autoOnClip = clipFactory.NewClip("Enable SPS Auto Contacts");
                var directTree = directTreeService.Create($"Auto Mode Toggle");
                directTree.Add(
                    BlendtreeMath.GreaterThan(fx.IsLocal().AsFloat(), 0, name: "SPS: Auto Contacts").And(
                    BlendtreeMath.GreaterThan(autoOn.AsFloat(), 0, name: "When Local")
                ).create(autoOnClip, null));
            }

            var enableStealth = avatarObject.GetComponentsInSelfAndChildren<VRCFuryHapticSocket>()
                .Where(o => o.addMenuItem)
                .ToArray()
                .Length >= 1;
            VFABool stealthOn = null;
            AnimationClip stealthClip = null;
            if (enableStealth) {
                stealthOn = fx.NewBool("stealth", synced: true, saved: saved);
                menu.NewMenuToggle($"{spsOptions.GetOptionsPath()}/<b>Stealth Mode<\\/b>\n<size=20>Only local haptics,\nInvisible to others", stealthOn);
                stealthClip = clipFactory.NewClip($"SPS (Stealth)");
                var directTree = directTreeService.Create($"SPS - Stealth");
                directTree.Add(BlendtreeMath.GreaterThan(stealthOn.AsFloat(), 0).create(stealthClip, null));
            }

            var enableLegacy = avatarObject.GetComponentsInSelfAndChildren<VRCFuryHapticSocket>()
                .Where(o => o.addMenuItem)
                .ToArray()
                .Length >= 1;
            VFABool legacyOn = null;
            AnimationClip legacyOffClip = null;
            if (enableLegacy) {
                legacyOn = fx.NewBool("legacy", synced: true, networkSynced: false, saved: saved, def: true);
                menu.NewMenuToggle($"{spsOptions.GetOptionsPath()}/<b>Legacy Compatibility<\\/b>\n<size=20>DPS \\/ TPS \\/ SPS1\nOne socket at a time", legacyOn);
                legacyOffClip = clipFactory.NewClip("Turn off SPS Legacy lights");
                var directTree = directTreeService.Create($"SPS Legacy");
                directTree.Add(BlendtreeMath.GreaterThan(legacyOn.AsFloat(), 0f).create(null, legacyOffClip));
            }

            var autoSockets = new List<Tuple<string, VFABool, VFGameObject>>();
            var exclusiveTriggers = new List<(string,VFABool)>();
            var usedMenuNames = new HashSet<string>();
            var usedOscIds = new HashSet<string>();
            foreach (var socket in avatarObject.GetComponentsInSelfAndChildren<VRCFuryHapticSocket>()) {
                try {
                    VFGameObject obj = socket.owner();
                    PhysboneUtils.RemoveFromPhysbones(socket.owner());

                    var menuName = HapticUtils.MakeUniqueId(
                        usedMenuNames,
                        HapticUtils.GetPreferredId(socket, s => s.name, s => HapticUtils.GetFallbackId(s.owner()))
                    );
                    var oscId = HapticUtils.MakeUniqueId(
                        usedOscIds,
                        HapticUtils.GetPreferredId(socket, s => s.oscId, _ => menuName)
                    );
                    Debug.Log("Baking haptic component in " + socket.owner().GetPath() + " as " + oscId);

                    VFABool toggleParam = null;
                    if (socket.addMenuItem) {
                        toggleParam = fx.NewBool(oscId, synced: true, saved: saved);
                        var icon = socket.menuIcon?.Get();
                        menu.NewMenuToggle($"{spsOptions.GetMenuPath()}/{menuName}", toggleParam, icon: icon);
                        exclusiveTriggers.Add((oscId, toggleParam));
                    }

                    if (!BuildTargetUtils.IsDesktop()) {
                        continue;
                    }

                    var bakeResult = VRCFuryHapticSocketEditor.Bake(socket, spsMarkersService);
                    if (bakeResult == null) continue;
                    var screenMarkers = bakeResult.screenMarkers ?? new List<VFGameObject>();
                    var screenMarkerResults = bakeResult.screenMarkerResults ?? new List<VRCFuryHapticSocketEditor.ScreenMarkerResult>();
                    foreach (var screenMarkerResult in screenMarkerResults) {
                        var renderer = screenMarkerResult.renderer;
                        if (renderer != null) {
                            RegisterMaterialProperties(screenMarkerResult.materialProperties);
                            spsPlayerIdService.Register(renderer);
                        }
                    }

                    globals.addOtherFeature(new ShowInFirstPerson {
                        useObjOverride = true,
                        objOverride = bakeResult.bakeRoot,
                        onlyIfChildOfHead = true
                    });

                    VFGameObject haptics = null;
                    if (HapticsToggleMenuItem.Get() && !socket.fromSpsForAll) {
                        // Haptic receivers

                        // This is *90 because capsule length is actually "height", so we have to rotate it to make it a length
                        var capsuleRotation = Quaternion.Euler(90,0,0);

                        var paramPrefix = "OGB/Orf/" + oscId.Replace('/','_');

                        // Receivers
                        var handTouchZoneSize = VRCFuryHapticSocketEditor.GetHandTouchZoneSize(socket);
                        haptics = GameObjects.Create("Haptics", bakeResult.worldSpace);

                        var baseReq = new HapticContactsService.ReceiverRequest() {
                            obj = haptics,
                            usePrefix = false,
                            localOnly = true,
                            useHipAvoidance = socket.useHipAvoidance
                        };

                        if (handTouchZoneSize != null) {
                            var oscDepth = handTouchZoneSize.Item1;
                            var closeRadius = handTouchZoneSize.Item2;
                            {
                                var r = baseReq.Clone();
                                r.pos = Vector3.forward * -oscDepth;
                                r.paramName = paramPrefix + "/TouchSelf";
                                r.objName = "TouchSelf";
                                r.radius = oscDepth;
                                r.tags = HapticUtils.SelfContacts;
                                r.party = HapticUtils.ReceiverParty.Self;
                                hapticContacts.AddReceiver(r);
                                r.paramName = paramPrefix + "/TouchOthers";
                                r.objName = "TouchOthers";
                                r.tags = HapticUtils.BodyContacts;
                                r.party = HapticUtils.ReceiverParty.Others;
                                hapticContacts.AddReceiver(r);
                                // Legacy non-upgraded TPS detection
                                r.paramName = paramPrefix + "/PenOthers";
                                r.objName = "PenOthers";
                                r.tags = new[] { HapticUtils.CONTACT_PEN_MAIN };
                                hapticContacts.AddReceiver(r);
                            }
                            {
                                var r = baseReq.Clone();
                                r.pos = Vector3.forward * -(oscDepth / 2);
                                r.paramName = paramPrefix + "/TouchSelfClose";
                                r.objName = "TouchSelfClose";
                                r.radius = closeRadius;
                                r.tags = HapticUtils.SelfContacts;
                                r.party = HapticUtils.ReceiverParty.Self;
                                r.height = oscDepth;
                                r.rotation = capsuleRotation;
                                r.type = ContactReceiver.ReceiverType.Constant;
                                hapticContacts.AddReceiver(r);
                                r.paramName = paramPrefix + "/TouchOthersClose";
                                r.objName = "TouchOthersClose";
                                r.tags = HapticUtils.BodyContacts;
                                r.party = HapticUtils.ReceiverParty.Others;
                                hapticContacts.AddReceiver(r);
                                // Legacy non-upgraded TPS detection
                                r.paramName = paramPrefix + "/PenOthersClose";
                                r.objName = "PenOthersClose";
                                r.tags = new[] { HapticUtils.CONTACT_PEN_MAIN };
                                hapticContacts.AddReceiver(r);
                            }
                            {
                                var frotRadius = 0.1f;
                                var frotPos = 0.05f;
                                var r = baseReq.Clone();
                                r.pos = Vector3.forward * frotPos;
                                r.paramName = paramPrefix + "/FrotOthers";
                                r.objName = "FrotOthers";
                                r.radius = frotRadius;
                                r.tags = new[] { HapticUtils.TagTpsOrfRoot };
                                r.party = HapticUtils.ReceiverParty.Others;
                                hapticContacts.AddReceiver(r);
                            }
                        }

                        var req = baseReq.Clone();
                        req.radius = 1f;
                        req.paramName = paramPrefix + "/PenSelfNewRoot";
                        req.objName = "PenSelfNewRoot";
                        req.tags = new[] { HapticUtils.CONTACT_PEN_ROOT };
                        req.party = HapticUtils.ReceiverParty.Self;
                        hapticContacts.AddReceiver(req);
                        req.paramName = paramPrefix + "/PenOthersNewRoot";
                        req.objName = "PenOthersNewRoot";
                        req.party = HapticUtils.ReceiverParty.Others;
                        hapticContacts.AddReceiver(req);
                        req.paramName = paramPrefix + "/PenSelfNewTip";
                        req.objName = "PenSelfNewTip";
                        req.tags = new[] { HapticUtils.CONTACT_PEN_MAIN };
                        req.party = HapticUtils.ReceiverParty.Self;
                        hapticContacts.AddReceiver(req);
                        req.paramName = paramPrefix + "/PenOthersNewTip";
                        req.objName = "PenOthersNewTip";
                        req.party = HapticUtils.ReceiverParty.Others;
                        hapticContacts.AddReceiver(req);
                        ogbEnabledService.Register(haptics);
                    }

                    var animObjects = new List<VFGameObject>();
                    var Contacts = new Lazy<SpsDepthContacts>(() => {
                        var animRoot = GameObjects.Create("Animations", bakeResult.worldSpace);
                        animObjects.Add(animRoot);
                        var worldScale = scaleFactorService.GetWorldScale(animRoot);
                        var directTree = directTreeService.Create($"{oscId} - Depth Calculations");
                        var math = directTreeService.GetMath(directTree);
                        return new SpsDepthContacts(animRoot, oscId, hapticContacts, directTree, math, fx, frameTimeService, socket.useHipAvoidance, worldScale);
                    });

                    if (socket.depthActions2.Count > 0) {
                        _hapticAnimContactsService.CreateAnims(
                            $"{oscId} - Depth Animations",
                            socket.depthActions2,
                            socket.owner(),
                            oscId,
                            Contacts.Value
                        );
                    }

                    var injectDepthToFullControllerParams = globals.allBuildersInRun
                        .OfType<FullControllerBuilder>()
                        .Where(fc => fc.featureBaseObject.IsChildOf(socket.owner()))
                        .Select(fc => fc.injectSpsDepthParam)
                        .NotNull()
                        .ToList();
                    foreach (var i in injectDepthToFullControllerParams) {
                        directTreeService.GetMath(Contacts.Value.directTree)
                            .CopyInPlace(Contacts.Value.closestDistancePlugLengths.Value, i);
                    }
                    var injectVelocityToFullControllerParams = globals.allBuildersInRun
                        .OfType<FullControllerBuilder>()
                        .Where(fc => fc.featureBaseObject.IsChildOf(socket.owner()))
                        .Select(fc => fc.injectSpsVelocityParam)
                        .NotNull()
                        .ToList();
                    foreach (var i in injectVelocityToFullControllerParams) {
                        directTreeService.GetMath(Contacts.Value.directTree)
                            .CopyInPlace(Contacts.Value.velocity.Value, i);
                    }
                    if (socket.IsValidPlugLength) {
                        directTreeService.GetMath(Contacts.Value.directTree)
                            .CopyInPlace(Contacts.Value.closestLength.Value, socket.plugLengthParameterName);
                    }
                    if (socket.IsValidPlugWidth) {
                        directTreeService.GetMath(Contacts.Value.directTree)
                            .CopyInPlace(Contacts.Value.closestRadius.Value, socket.plugWidthParameterName);
                    }

                    if (stealthClip != null) {
                        foreach (var child in new[] { bakeResult.lights, bakeResult.senders }
                                     .Concat(animObjects)
                                     .Concat(screenMarkers)
                                     .NotNull()
                        ) {
                            stealthClip.SetEnabled(child, false);
                        }
                    }

                    if (legacyOffClip != null) {
                        foreach (var child in new[] { bakeResult.lights }.NotNull()) {
                            legacyOffClip.SetEnabled(child, false);
                        }
                    }

                    // Do the toggle last so all the objects have been generated and can be toggled on/off
                    if (toggleParam != null) {
                        obj.active = true;
                        _forceStateInAnimatorService.ForceEnable(obj);

                        bakeResult.bakeRoot.active = false;
                        var onClip = clipFactory.NewClip($"{oscId}");
                        onClip.SetEnabled(bakeResult.bakeRoot, true);
                        var directTree = directTreeService.Create($"{oscId} - Toggle");
                        directTree.Add(toggleParam.AsFloat(), onClip);

                        var activeClip = actionClipService.LoadState($"SPS - Active Animation for {oscId}", socket.activeActions);
                        if (new AnimatorIterator.Clips().From(activeClip).SelectMany(clip => clip.GetAllBindings()).Any()) {
                            var activeAnimParam = fx.NewFloat($"SPS - Active Animation for {oscId}");
                            var activeAnimLayer = fx.NewLayer($"SPS - Active Animation for {oscId}");
                            var off = activeAnimLayer.NewState("Off");
                            var on = activeAnimLayer.NewState("On").WithAnimation(activeClip);

                            off.TransitionsTo(on).When(activeAnimParam.IsGreaterThan(0));
                            on.TransitionsTo(off).When(activeAnimParam.IsLessThan(1));

                            onClip.SetAap(activeAnimParam, 1);
                        }

                        var gizmo = obj.GetComponent<VRCFurySocketGizmo>();
                        if (gizmo != null) {
                            gizmo.show = false;
                            onClip.SetCurve(gizmo, "show", 1);
                        }

                        if (socket.enableAuto && autoOnClip != null) {
                            autoSockets.Add(Tuple.Create(oscId, toggleParam, bakeResult.bakeRoot));
                        }
                    }
                } catch (Exception e) {
                    throw new ExceptionWithCause($"Failed to bake SPS Socket: {socket.owner().GetPath(avatarObject)}", e);
                }
            }

            if (exclusiveTriggers.Count >= 2) {
                var exclusiveLayer = fx.NewLayer("SPS - Socket Exclusivity");
                exclusiveLayer.NewState("Start");
                foreach (var i in Enumerable.Range(0, exclusiveTriggers.Count)) {
                    var (name, on) = exclusiveTriggers[i];
                    var state = exclusiveLayer.NewState(name);
                    var when = on.IsTrue();
                    when = when.And(fx.IsLocal().IsTrue());
                    if (legacyOn != null) when = when.And(legacyOn.IsTrue());
                    if (stealthOn != null) when = when.And(stealthOn.IsFalse());
                    state.TransitionsFromAny().When(when);
                    foreach (var j in Enumerable.Range(0, exclusiveTriggers.Count)) {
                        if (i == j) continue;
                        var (_, otherOn) = exclusiveTriggers[j];
                        state.Drives(otherOn, false);
                    }
                }
            }

#if VRCSDK_HAS_VRCCONSTRAINTS
            if (autoOn != null && autoSockets.Count > 0) {
                var autoActiveNum = fx.NewInt("AutoSocketNum");
                var autoActiveDist = fx.NewFloat("AutoActiveDist");
                var autoCurrentDist = fx.NewFloat("AutoCurrentDist");
                var dbt = directTreeService.Create("SPS - Auto Mode");
                var math = directTreeService.GetMath(dbt);
                var diff = math.Subtract(autoCurrentDist, autoActiveDist);

                var autoReceiverObj = GameObjects.Create("SpsAutoDistance", avatarObject);
                ConstraintUtils.MakeWorldSpace(autoReceiverObj);

                var receiver = autoReceiverObj.AddComponent<VRCContactReceiver>();
                receiver.parameter = autoCurrentDist;
                receiver.radius = 1f;
                receiver.receiverType = ContactReceiver.ReceiverType.Proximity;
                receiver.collisionTags.Add(HapticUtils.CONTACT_PEN_MAIN);
                receiver.allowOthers = true;
                receiver.allowSelf = false;
                receiver.localOnly = true;
                receiver.shapeType = ContactBase.ShapeType.Sphere;

                var constraint = autoReceiverObj.AddComponent<VRCParentConstraint>();
                constraint.IsActive = true;
                constraint.Locked = true;

                var layer = fx.NewLayer("SPS - Auto Socket Comparison");
                var remoteTrap = layer.NewState("Remote trap");
                var stopped = layer.NewState("Stopped");
                remoteTrap.TransitionsTo(stopped).When(fx.IsLocal().IsTrue());

                var lastState = stopped;
                Action<VFState> addNext = (next) => stopped.TransitionsTo(next).When(autoOn.IsTrue());

                var settleTime = 0.05f;

                for (var i = 0; i < autoSockets.Count && i < 16; i++) {
                    var (name, enabled, obj) = autoSockets[i];
                    constraint.Sources.Add(new VRCConstraintSource(obj, 0, Vector3.zero, Vector3.zero));

                    // We need to settle for a frame for the constraint to move
                    var evalClip = clipFactory.NewClip($"Settle1 {name}");
                    evalClip.SetCurve(constraint, $"Sources.source{i}.Weight", 1);
                    var settleState = layer.NewState($"Settle {name}").WithAnimation(evalClip).Move(lastState, 1, 0);
                    lastState = settleState;
                    if (addNext != null) addNext(settleState);

                    // We need to settle for a moment because contacts don't update every frame
                    var settleState2 = layer.NewState($"Settle2 {name}").WithAnimation(evalClip);
                    settleState.TransitionsTo(settleState2).When().WithTransitionExitTime(settleTime);

                    // We need to settle for another frame for the dbt subtraction to apply
                    var settleState3 = layer.NewState($"Settle3 {name}").WithAnimation(evalClip);
                    settleState2.TransitionsTo(settleState3).When(fx.Always());

                    var updateState = layer.NewState($"Update {name}").WithAnimation(evalClip);
                    updateState.DrivesCopy(autoCurrentDist, autoActiveDist);
                    settleState3.TransitionsTo(updateState).When(autoActiveNum.IsEqualTo(i));

                    var switchToState = layer.NewState($"Switch To {name}").WithAnimation(evalClip);
                    switchToState.Drives(autoActiveNum, i);
                    switchToState.DrivesCopy(autoCurrentDist, autoActiveDist);
                    foreach (var o in autoSockets) {
                        if (o.Item2 != enabled) switchToState.Drives(o.Item2, false);
                    }
                    switchToState.Drives(enabled, true);
                    settleState3.TransitionsTo(switchToState).When(diff.IsGreaterThan(0));

                    addNext = (next) => {
                        settleState3.TransitionsTo(next).When(fx.Always());
                        updateState.TransitionsTo(next).When(fx.Always());
                        switchToState.TransitionsTo(next).When(fx.Always());
                    };
                }

                if (addNext != null) addNext(stopped);
            }
#endif
        }
    }
}
