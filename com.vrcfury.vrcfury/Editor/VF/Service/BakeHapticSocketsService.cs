using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using VF.Builder;
using VF.Builder.Exceptions;
using VF.Builder.Haptics;
using VF.Component;
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

namespace VF.Service {
    [VFService]
    internal class BakeHapticSocketsService {

        [VFAutowired] private readonly GlobalsService globals;
        [VFAutowired] private readonly VFGameObject avatarObject;
        [VFAutowired] private readonly ActionClipService actionClipService;
        [VFAutowired] private readonly HapticAnimContactsService _hapticAnimContactsService;
        [VFAutowired] private readonly MathService math;
        [VFAutowired] private readonly ForceStateInAnimatorService _forceStateInAnimatorService;
        [VFAutowired] private readonly SpsOptionsService spsOptions;
        [VFAutowired] private readonly HapticContactsService hapticContacts;
        [VFAutowired] private readonly DirectBlendTreeService directTree;
        [VFAutowired] private readonly UniqueHapticNamesService uniqueHapticNamesService;
        [VFAutowired] private readonly ClipFactoryService clipFactory;
        [VFAutowired] private readonly ScaleFactorService scaleFactorService;
        [VFAutowired] private readonly VRCAvatarDescriptor avatar;
        [VFAutowired] private readonly ControllersService controllers;
        private ControllerManager fx => controllers.GetFx();
        [VFAutowired] private readonly MenuService menuService;
        private MenuManager menu => menuService.GetMenu();

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
                directTree.Add(math.And(
                    math.GreaterThan(fx.IsLocal().AsFloat(), 0.5f, name: "SPS: Auto Contacts"),
                    math.GreaterThan(autoOn.AsFloat(), 0.5f, name: "When Local")
                ).create(autoOnClip, null));
            }
            
            var enableStealth = avatarObject.GetComponentsInSelfAndChildren<VRCFuryHapticSocket>()
                .Where(o => o.addMenuItem)
                .ToArray()
                .Length >= 1;
            VFABool stealthOn = null;
            if (enableStealth) {
                stealthOn = fx.NewBool("stealth", synced: true, saved: saved);
                menu.NewMenuToggle($"{spsOptions.GetOptionsPath()}/<b>Stealth Mode<\\/b>\n<size=20>Only local haptics,\nInvisible to others", stealthOn);
            }
            
            var enableMulti = avatarObject.GetComponentsInSelfAndChildren<VRCFuryHapticSocket>()
                .Where(o => o.addMenuItem)
                .ToArray()
                .Length >= 2;
            VFABool multiOn = null;
            if (enableMulti) {
                multiOn = fx.NewBool("multi", synced: true, networkSynced: false, saved: saved);
                var multiFolder = $"{spsOptions.GetOptionsPath()}/<b>Dual Mode<\\/b>\n<size=20>Allows 2 active sockets";
                menu.NewMenuToggle($"{multiFolder}/Enable Dual Mode", multiOn);
                menu.NewMenuButton($"{multiFolder}/<b>WARNING<\\/b>\n<size=20>Everyone else must use SPS or TPS - NO DPS!");
                menu.NewMenuButton($"{multiFolder}/<b>WARNING<\\/b>\n<size=20>Nobody else can use a hole at the same time");
                menu.NewMenuButton($"{multiFolder}/<b>WARNING<\\/b>\n<size=20>DO NOT ENABLE MORE THAN 2");
            }

            var autoSockets = new List<Tuple<string, VFABool, VFAFloat>>();
            var exclusiveTriggers = new List<(string,VFABool)>();
            foreach (var socket in avatarObject.GetComponentsInSelfAndChildren<VRCFuryHapticSocket>()) {
                try {
                    VFGameObject obj = socket.owner();
                    PhysboneUtils.RemoveFromPhysbones(socket.owner());

                    var name = VRCFuryHapticSocketEditor.GetName(socket);
                    name = uniqueHapticNamesService.GetUniqueName(name);
                    Debug.Log("Baking haptic component in " + socket.owner().GetPath() + " as " + name);
                    
                    VFABool toggleParam = null;
                    if (socket.addMenuItem) {
                        toggleParam = fx.NewBool(name, synced: true, saved: saved);
                        var icon = socket.menuIcon?.Get();
                        menu.NewMenuToggle($"{spsOptions.GetMenuPath()}/{name}", toggleParam, icon: icon);
                    }

                    if (!BuildTargetUtils.IsDesktop()) {
                        continue;
                    }

                    var bakeResult = VRCFuryHapticSocketEditor.Bake(socket, hapticContacts);
                    if (bakeResult == null) continue;
                    
                    globals.addOtherFeature(new ShowInFirstPerson {
                        useObjOverride = true,
                        objOverride = bakeResult.bakeRoot,
                        onlyIfChildOfHead = true
                    });

                    VFGameObject haptics = null;
                    if (HapticsToggleMenuItem.Get() && !socket.sendersOnly) {
                        // Haptic receivers

                        // This is *90 because capsule length is actually "height", so we have to rotate it to make it a length
                        var capsuleRotation = Quaternion.Euler(90,0,0);
                        
                        var paramPrefix = "OGB/Orf/" + name.Replace('/','_');
                    
                        // Receivers
                        var handTouchZoneSize = VRCFuryHapticSocketEditor.GetHandTouchZoneSize(socket, avatar);
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
                    }

                    var animObjects = new List<VFGameObject>();
                    var Contacts = new Lazy<SpsDepthContacts>(() => {
                        var scale = scaleFactorService.GetAdv(bakeResult.bakeRoot, bakeResult.worldSpace);
                        if (scale == null) throw new Exception("Scale cannot be null at this point. Is this a mobile build somehow?");
                        var (scaleFactor, scaleFactorContact1, scaleFactorContact2) = scale.Value;
                        var animRoot = GameObjects.Create("Animations", bakeResult.worldSpace);
                        animObjects.Add(animRoot);
                        animObjects.Add(scaleFactorContact1);
                        animObjects.Add(scaleFactorContact2);
                        return new SpsDepthContacts(animRoot, name, hapticContacts, directTree, math, socket.useHipAvoidance, scaleFactor);
                    });

                    if (socket.depthActions2.Count > 0) {
                        _hapticAnimContactsService.CreateAnims(
                            socket.depthActions2,
                            socket.owner(),
                            name,
                            Contacts.Value
                        );
                    }
                    
                    var injectDepthToFullControllerParams = globals.allBuildersInRun
                        .OfType<FullControllerBuilder>()
                        .Where(fc => fc.featureBaseObject.IsChildOf(socket.owner()))
                        .Select(fc => fc.injectSpsDepthParam)
                        .NotNull()
                        .ToList();
                    if (socket.IsValidPlugLength) {
                        math.CopyInPlace(socket.plugLengthParameterName, Contacts.Value.closestLength.Value);
                    }
                    foreach (var i in injectDepthToFullControllerParams) {
                        math.CopyInPlace(i, Contacts.Value.closestDistancePlugLengths.Value);
                    }
                    if (socket.IsValidPlugWidth) {
                        math.Buffer(Contacts.Value.closestRadius.Value, socket.plugWidthParameterName, usePrefix: false);
                    }

                    // Do the toggle last so all the objects have been generated and can be toggled on/off
                    if (toggleParam != null) {
                        obj.active = true;
                        _forceStateInAnimatorService.ForceEnable(obj);

                        foreach (var child in new []{bakeResult.bakeRoot, bakeResult.senders, haptics, bakeResult.lights}.Concat(animObjects).NotNull()) {
                            child.active = false;
                        }

                        var onLocalClip = clipFactory.NewClip($"{name} (Local)");
                        foreach (var child in new []{bakeResult.bakeRoot, bakeResult.senders, haptics, bakeResult.lights}.Concat(animObjects).NotNull()) {
                            onLocalClip.SetEnabled(child, true);
                        }

                        var onRemoteClip = clipFactory.NewClip($"{name} (Remote)");
                        foreach (var child in new []{bakeResult.bakeRoot, bakeResult.senders, bakeResult.lights}.Concat(animObjects).NotNull()) {
                            onRemoteClip.SetEnabled(child, true);
                        }
                        
                        var onStealthClip = clipFactory.NewClip($"{name} (Stealth)");
                        foreach (var child in new []{bakeResult.bakeRoot, haptics}.NotNull()) {
                            onStealthClip.SetEnabled(child, true);
                        }

                        var activeClip = actionClipService.LoadState($"SPS - Active Animation for {name}", socket.activeActions);
                        if (new AnimatorIterator.Clips().From(activeClip).SelectMany(clip => clip.GetAllBindings()).Any()) {
                            var activeAnimParam = fx.NewFloat($"SPS - Active Animation for {name}");
                            var activeAnimLayer = fx.NewLayer($"SPS - Active Animation for {name}");
                            var off = activeAnimLayer.NewState("Off");
                            var on = activeAnimLayer.NewState("On").WithAnimation(activeClip);

                            off.TransitionsTo(on).When(activeAnimParam.IsGreaterThan(0));
                            on.TransitionsTo(off).When(activeAnimParam.IsLessThan(1));

                            onLocalClip.SetAap(activeAnimParam, 1);
                            onRemoteClip.SetAap(activeAnimParam, 1);
                        }

                        var gizmo = obj.GetComponent<VRCFurySocketGizmo>();
                        if (gizmo != null) {
                            gizmo.show = false;
                            onLocalClip.SetCurve(gizmo, "show", 1);
                            onRemoteClip.SetCurve(gizmo, "show", 1);
                        }

                        var localTree = math.GreaterThan(stealthOn.AsFloat(), 0.5f, name: "When Local")
                            .create(onStealthClip, onLocalClip);
                        var remoteTree = math.GreaterThan(stealthOn.AsFloat(), 0.5f, name: "When Remote")
                            .create(null, onRemoteClip);
                        var onTree = math.GreaterThan(fx.IsLocal().AsFloat(), 0.5f, name: $"SPS: When {name} On")
                            .create(localTree, remoteTree);
                        directTree.Add(toggleParam.AsFloat(), onTree);

                        exclusiveTriggers.Add((name, toggleParam));

                        if (socket.enableAuto && autoOnClip) {
                            var autoReceiverObj = GameObjects.Create("AutoDistance", bakeResult.worldSpace);
                            var distParam = hapticContacts.AddReceiver(new HapticContactsService.ReceiverRequest() {
                                obj = autoReceiverObj,
                                paramName = name + "/AutoDistance",
                                objName = "Receiver",
                                radius = 0.3f,
                                tags = new[] { HapticUtils.CONTACT_PEN_MAIN },
                                party = HapticUtils.ReceiverParty.Others,
                                useHipAvoidance = socket.useHipAvoidance
                            });
                            autoReceiverObj.active = false;
                            foreach (var child in new []{bakeResult.bakeRoot, autoReceiverObj}.NotNull()) {
                                autoOnClip.SetEnabled(child, true);
                            }
                            autoSockets.Add(Tuple.Create(name, toggleParam, distParam));
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
                    if (multiOn != null) when = when.And(multiOn.IsFalse());
                    if (stealthOn != null) when = when.And(stealthOn.IsFalse());
                    state.TransitionsFromAny().When(when);
                    foreach (var j in Enumerable.Range(0, exclusiveTriggers.Count)) {
                        if (i == j) continue;
                        var (_, otherOn) = exclusiveTriggers[j];
                        state.Drives(otherOn, false);
                    }
                }
            }

            if (autoOn != null && autoSockets.Count > 0) {
                var layer = fx.NewLayer("SPS - Auto Socket Comparison");
                var remoteTrap = layer.NewState("Remote trap");
                var stopped = layer.NewState("Stopped");
                remoteTrap.TransitionsTo(stopped).When(fx.IsLocal().IsTrue());
                var start = layer.NewState("Start").Move(stopped, 1, 0);
                stopped.TransitionsTo(start).When(autoOn.IsTrue());
                var stop = layer.NewState("Stop").Move(start, 1, 0);
                start.TransitionsTo(stop).When(autoOn.IsFalse());
                foreach (var auto in autoSockets) {
                    var (name, enabled, dist) = auto;
                    stop.Drives(enabled, false);
                }
                stop.TransitionsTo(stopped).When(fx.Always());

                var vsParam = math.MakeAap("comparison");

                var states = new Dictionary<Tuple<int, int>, VFState>();
                for (var i = 0; i < autoSockets.Count; i++) {
                    var (aName, aEnabled, aDist) = autoSockets[i];
                    var triggerOn = layer.NewState($"Start {aName}").Move(start, i, 2);
                    triggerOn.Drives(aEnabled, true);
                    states[Tuple.Create(i,-1)] = triggerOn;
                    var triggerOff = layer.NewState($"Stop {aName}");
                    triggerOff.Drives(aEnabled, false);
                    triggerOff.TransitionsTo(start).When(fx.Always());
                    states[Tuple.Create(i,-2)] = triggerOff;
                    for (var j = 0; j < autoSockets.Count; j++) {
                        if (i == j) continue;
                        var (bName, bEnabled, bDist) = autoSockets[j];
                        var vs = layer.NewState($"{aName} vs {bName}").Move(triggerOff, 0, j+1);
                        var tree = clipFactory.NewDBT($"{aName} vs {bName}");
                        tree.Add(bDist, math.MakeSetter(vsParam, 1));
                        tree.Add(aDist, math.MakeSetter(vsParam, -1));
                        vs.WithAnimation(tree);
                        states[Tuple.Create(i,j)] = vs;
                    }
                }

                for (var i = 0; i < autoSockets.Count; i++) {
                    var (name, enabled, dist) = autoSockets[i];
                    var triggerOn = states[Tuple.Create(i, -1)];
                    var triggerOff = states[Tuple.Create(i, -2)];
                    var firstComparison = states[Tuple.Create(i, i == 0 ? 1 : 0)];
                    start.TransitionsTo(firstComparison).When(enabled.IsTrue());
                    triggerOn.TransitionsTo(firstComparison).When(fx.Always());
                    
                    for (var j = 0; j < autoSockets.Count; j++) {
                        if (i == j) continue;
                        var current = states[Tuple.Create(i, j)];
                        var otherActivate = states[Tuple.Create(j, -1)];

                        current.TransitionsTo(otherActivate).When(vsParam.AsFloat().IsGreaterThan(0));
                        
                        var nextI = j + 1;
                        if (nextI == i) nextI++;
                        if (nextI == autoSockets.Count) {
                            current.TransitionsTo(triggerOff).When(dist.IsGreaterThan(0).Not());
                            current.TransitionsTo(start).When(fx.Always());
                        } else {
                            var next = states[Tuple.Create(i, nextI)];
                            current.TransitionsTo(next).When(fx.Always());
                        }
                    }
                }

                var firstSocket = autoSockets[0];
                // If this isn't here, the first socket will never activate unless another one is already active
                start.TransitionsTo(states[Tuple.Create(0, -1)])
                    .When(firstSocket.Item2.IsFalse().And(firstSocket.Item3.IsGreaterThan(0)));
                start.TransitionsTo(states[Tuple.Create(0, 1)]).When(fx.Always());
            }
        }
    }
}
