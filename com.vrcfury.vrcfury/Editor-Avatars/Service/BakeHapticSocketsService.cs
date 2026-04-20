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
#if VRCSDK_HAS_VRCCONSTRAINTS
using VRC.SDK3.Dynamics.Constraint.Components;
#endif

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
        [VFAutowired] private readonly IsObjectEnabledService isObjectEnabledService;
        private ControllerManager fx => controllers.GetFx();
        [VFAutowired] private readonly MenuService menuService;
        private MenuManager menu => menuService.GetMenu();

        [FeatureBuilderAction]
        public void Apply() {
            var sockets = avatarObject.GetComponentsInSelfAndChildren<VRCFuryHapticSocket>().ToArray();
            if (sockets.Length == 0) return;

            var saved = spsOptions.GetOptions().saveSockets;

            var enableAuto = sockets
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
            
            var enableStealth = sockets
                .Where(o => o.addMenuItem)
                .ToArray()
                .Length >= 1;
            VFABool stealthOn = null;
            if (enableStealth) {
                stealthOn = fx.NewBool("stealth", synced: true, saved: saved);
                menu.NewMenuToggle($"{spsOptions.GetOptionsPath()}/<b>Stealth Mode<\\/b>\n<size=20>Only local haptics,\nInvisible to others", stealthOn);
            }
            
            var enableMulti = sockets
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
            var usedMenuNames = new HashSet<string>();
            var usedOscIds = new HashSet<string>();
            var anchorEnabledParams = new List<VFAFloat>();
            VRCFuryHapticSocketEditor.BakeResult sharedBakeResult = null;
            VFGameObject sharedHaptics = null;
            var sharedAnimObjects = new List<VFGameObject>();
            Lazy<SpsDepthContacts> sharedContacts = null;
            VFBlendTreeDirect socketAnchorTree = null;
#if VRCSDK_HAS_VRCCONSTRAINTS
            VRCParentConstraint socketAnchorConstraint = null;
#endif

            VFBlendTreeDirect GetSocketAnchorTree() {
                return socketAnchorTree ?? (socketAnchorTree = directTreeService.Create("SPS Socket Anchor"));
            }

            void EnableWhenSocketIsEnabled(VFAFloat enabled, VFGameObject obj) {
                if (obj == null) return;
                var clip = clipFactory.NewClip($"Enable {obj.name}");
                clip.SetEnabled(obj, true);
                GetSocketAnchorTree().Add(enabled, clip);
            }

            if (BuildTargetUtils.IsDesktop()) {
                var sharedSocketObj = GameObjects.Create("Shared SPS Socket", avatarObject);
                var sharedSocket = sharedSocketObj.AddComponent<VRCFuryHapticSocket>();
                sharedSocket.addLight = VRCFuryHapticSocket.AddLight.None;
                sharedSocket.enableHandTouchZone2 = VRCFuryHapticSocket.EnableTouchZone.Off;
                sharedSocket.addMenuItem = false;
                sharedSocket.enableAuto = false;
                sharedSocket.fromSpsForAll = true;
                sharedSocket.useHipAvoidance = false;

                sharedBakeResult = VRCFuryHapticSocketEditor.Bake(sharedSocket);
                if (sharedBakeResult != null) {
                    sharedBakeResult.bakeRoot.SetParent(avatarObject, true);
                    sharedBakeResult.bakeRoot.active = false;

                    var sharedOscId = "Shared";
                    if (HapticsToggleMenuItem.Get()) {
                        var capsuleRotation = Quaternion.Euler(90,0,0);
                        var paramPrefix = "OGB/Orf/" + sharedOscId.Replace('/','_');
                        var handTouchZoneSize = VRCFuryHapticSocketEditor.GetHandTouchZoneSize(sharedSocket);
                        sharedHaptics = GameObjects.Create("Haptics", sharedBakeResult.worldSpace);

                        var baseReq = new HapticContactsService.ReceiverRequest() {
                            obj = sharedHaptics,
                            usePrefix = false,
                            localOnly = true,
                            useHipAvoidance = false
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

                        sharedHaptics.active = false;
                    }

                    sharedContacts = new Lazy<SpsDepthContacts>(() => {
                        var scale = scaleFactorService.GetAdv(sharedBakeResult.bakeRoot, sharedBakeResult.worldSpace);
                        if (scale == null) throw new Exception("Scale cannot be null at this point. Is this a mobile build somehow?");
                        var (scaleFactor, scaleFactorContact1, scaleFactorContact2) = scale.Value;
                        var animRoot = GameObjects.Create("Animations", sharedBakeResult.worldSpace);
                        sharedAnimObjects.Add(animRoot);
                        sharedAnimObjects.Add(scaleFactorContact1);
                        sharedAnimObjects.Add(scaleFactorContact2);
                        var directTree = directTreeService.Create($"{sharedOscId} - Depth Calculations");
                        var math = directTreeService.GetMath(directTree);
                        return new SpsDepthContacts(animRoot, sharedOscId, hapticContacts, directTree, math, fx, frameTimeService, false, scaleFactor);
                    });
                }
                sharedSocketObj.Destroy();
            }

            foreach (var socket in sockets) {
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

                    var objectEnabled = isObjectEnabledService.Get(obj);
                    if (sharedBakeResult == null) continue;
                    var (lightType, localPosition, localRotation) = VRCFuryHapticSocketEditor.GetInfoFromLightsOrComponent(socket);
                    if (lightType != VRCFuryHapticSocket.AddLight.None && !socket.fromSpsForAll) {
                        VRCFuryHapticSocketEditor.ForEachPossibleLight(socket.owner(), false, light => {
                            light.Destroy();
                        });
                    }
                    var bakeResult = sharedBakeResult;

                    anchorEnabledParams.Add(objectEnabled);
                    var anchor = GameObjects.Create("SPS Socket Anchor", obj);
                    anchor.localPosition = localPosition;
                    anchor.localRotation = localRotation;
                    globals.addOtherFeature(new ShowInFirstPerson {
                        useObjOverride = true,
                        objOverride = anchor,
                        onlyIfChildOfHead = true
                    });
#if VRCSDK_HAS_VRCCONSTRAINTS
                    if (socketAnchorConstraint == null) {
                        socketAnchorConstraint = sharedBakeResult.bakeRoot.AddComponent<VRCParentConstraint>();
                        socketAnchorConstraint.IsActive = true;
                        socketAnchorConstraint.Locked = true;
                    }

                    var sourceId = socketAnchorConstraint.Sources.Count;
                    socketAnchorConstraint.Sources.Add(new VRCConstraintSource(anchor, 0, Vector3.zero, Vector3.zero));

                    var moveSocketClip = clipFactory.NewClip($"Move SPS Socket {sourceId}");
                    moveSocketClip.SetCurve(socketAnchorConstraint, $"Sources.source{sourceId}.Weight", 1);
                    GetSocketAnchorTree().Add(objectEnabled, moveSocketClip);
#endif

                    var Contacts = sharedContacts;

                    if (socket.depthActions2.Count > 0) {
                        _hapticAnimContactsService.CreateAnims(
                            $"{oscId} - Depth Animations",
                            socket.depthActions2,
                            socket.owner(),
                            oscId,
                            Contacts.Value,
                            objectEnabled
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

                    // Do the toggle last so all the objects have been generated and can be toggled on/off
                    if (toggleParam != null) {
                        obj.active = true;
                        _forceStateInAnimatorService.ForceEnable(obj);

                        foreach (var child in new []{sharedHaptics}.Concat(sharedAnimObjects).NotNull()) {
                            child.active = false;
                        }

                        var onLocalClip = clipFactory.NewClip($"{oscId} (Local)");
                        foreach (var child in new []{sharedHaptics}.Concat(sharedAnimObjects).NotNull()) {
                            onLocalClip.SetEnabled(child, true);
                        }

                        var onRemoteClip = clipFactory.NewClip($"{oscId} (Remote)");
                        foreach (var child in sharedAnimObjects.NotNull()) {
                            onRemoteClip.SetEnabled(child, true);
                        }
                        
                        var onStealthClip = clipFactory.NewClip($"{oscId} (Stealth)");
                        foreach (var child in new []{sharedHaptics}.NotNull()) {
                            onStealthClip.SetEnabled(child, true);
                        }

                        var activeClip = actionClipService.LoadState($"SPS - Active Animation for {oscId}", socket.activeActions);
                        if (new AnimatorIterator.Clips().From(activeClip).SelectMany(clip => clip.GetAllBindings()).Any()) {
                            var activeAnimParam = fx.NewFloat($"SPS - Active Animation for {oscId}");
                            var activeAnimLayer = fx.NewLayer($"SPS - Active Animation for {oscId}");
                            var off = activeAnimLayer.NewState("Off");
                            var on = activeAnimLayer.NewState("On").WithAnimation(activeClip);

                            off.TransitionsTo(on).When(activeAnimParam.IsGreaterThan(0));
                            on.TransitionsTo(off).When(activeAnimParam.IsLessThan(1));

                            onLocalClip.SetAap(activeAnimParam, 1);
                            onRemoteClip.SetAap(activeAnimParam, 1);
                            var activeAnimWhenSocketEnabled = clipFactory.NewClip($"Set {activeAnimParam.Name()}");
                            activeAnimWhenSocketEnabled.SetAap(activeAnimParam.Name(), 1);
                            GetSocketAnchorTree().Add(objectEnabled, activeAnimWhenSocketEnabled);
                        }

                        var gizmo = obj.GetComponent<VRCFurySocketGizmo>();
                        if (gizmo != null) {
                            gizmo.show = false;
                            onLocalClip.SetCurve(gizmo, "show", 1);
                            onRemoteClip.SetCurve(gizmo, "show", 1);
                        }

                        var localTree = stealthOn == null
                            ? onLocalClip
                            : BlendtreeMath.GreaterThan(stealthOn.AsFloat(), 0, name: "When Local")
                                .create(onStealthClip, onLocalClip);
                        var remoteTree = stealthOn == null
                            ? onRemoteClip
                            : BlendtreeMath.GreaterThan(stealthOn.AsFloat(), 0, name: "When Remote")
                                .create(null, onRemoteClip);
                        var onTree = BlendtreeMath.GreaterThan(fx.IsLocal().AsFloat(), 0, name: $"SPS: When {oscId} On")
                            .create(localTree, remoteTree);
                        var directTree = directTreeService.Create($"{oscId} - Toggle");
                        directTree.Add(toggleParam.AsFloat(), onTree);

                        if (socket.enableAuto && autoOnClip != null) {
                            var autoReceiverObj = GameObjects.Create("AutoDistance", bakeResult.worldSpace);
                            var distParam = hapticContacts.AddReceiver(new HapticContactsService.ReceiverRequest() {
                                obj = autoReceiverObj,
                                paramName = oscId + "/AutoDistance",
                                objName = "Receiver",
                                radius = 0.3f,
                                tags = new[] { HapticUtils.CONTACT_PEN_MAIN },
                                party = HapticUtils.ReceiverParty.Others,
                                useHipAvoidance = socket.useHipAvoidance
                            });
                            autoReceiverObj.active = false;
                            foreach (var child in new []{autoReceiverObj}.NotNull()) {
                                autoOnClip.SetEnabled(child, true);
                            }
                            autoSockets.Add(Tuple.Create(oscId, toggleParam, distParam));
                        }
                    }
                } catch (Exception e) {
                    throw new ExceptionWithCause($"Failed to bake SPS Socket: {socket.owner().GetPath(avatarObject)}", e);
                }
            }
            if (sharedBakeResult != null) {
                var anySocketEnabled = anchorEnabledParams[0];
                if (anchorEnabledParams.Count > 1) {
                    var anySocketEnabledOutput = fx.MakeAap(
                        "SPS Socket Anchor/Any Enabled",
                        anchorEnabledParams.Any(param => param.GetDefault() > 0) ? 1 : 0
                    );
                    var anySocketEnabledCondition = anchorEnabledParams
                        .Select(param => BlendtreeMath.GreaterThan(param, 0))
                        .Aggregate(BlendtreeMath.False(), (a, b) => a.Or(b));
                    directTreeService.GetMath(GetSocketAnchorTree()).SetValueWithConditions(
                        (anySocketEnabledOutput.MakeSetter(1), anySocketEnabledCondition),
                        (anySocketEnabledOutput.MakeSetter(0), null)
                    );
                    anySocketEnabled = anySocketEnabledOutput.AsFloat();
                }
                EnableWhenSocketIsEnabled(anySocketEnabled, sharedBakeResult.bakeRoot);
                EnableWhenSocketIsEnabled(anySocketEnabled, sharedBakeResult.senders);
                EnableWhenSocketIsEnabled(anySocketEnabled, sharedBakeResult.lights);
                EnableWhenSocketIsEnabled(anySocketEnabled, sharedHaptics);
                foreach (var child in sharedAnimObjects.NotNull()) {
                    child.active = false;
                    EnableWhenSocketIsEnabled(anySocketEnabled, child);
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

                var vsParam = fx.MakeAap("comparison");

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
                        var tree = VFBlendTreeDirect.Create($"{aName} vs {bName}");
                        tree.Add(bDist, vsParam.MakeSetter(1));
                        tree.Add(aDist, vsParam.MakeSetter(-1));
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
