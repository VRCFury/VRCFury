using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
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
using VRC.SDK3.Dynamics.Contact.Components;

namespace VF.Feature {
    [VFService]
    internal class BakeHapticSocketsBuilder : FeatureBuilder {

        [VFAutowired] private readonly ActionClipService actionClipService;
        [VFAutowired] private readonly RestingStateService restingState;
        [VFAutowired] private readonly HapticAnimContactsService _hapticAnimContactsService;
        [VFAutowired] private readonly MathService math;
        [VFAutowired] private readonly FakeHeadService fakeHead;
        [VFAutowired] private readonly ObjectMoveService mover;
        [VFAutowired] private readonly ForceStateInAnimatorService _forceStateInAnimatorService;
        [VFAutowired] private readonly SpsOptionsService spsOptions;
        [VFAutowired] private readonly HapticContactsService hapticContacts;
        [VFAutowired] private readonly DirectBlendTreeService directTree;
        [VFAutowired] private readonly UniqueHapticNamesService uniqueHapticNamesService;
        [VFAutowired] private readonly ClipFactoryService clipFactory;

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
                autoOn = fx.NewBool("autoMode", addToParamFile: true, networkSynced: false, saved: saved);
                manager.GetMenu().NewMenuToggle($"{spsOptions.GetOptionsPath()}/<b>Auto Mode<\\/b>\n<size=20>Activates hole nearest to a VRCFury plug", autoOn);
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
                stealthOn = fx.NewBool("stealth", addToParamFile: true, saved: saved);
                manager.GetMenu().NewMenuToggle($"{spsOptions.GetOptionsPath()}/<b>Stealth Mode<\\/b>\n<size=20>Only local haptics,\nInvisible to others", stealthOn);
            }
            
            var enableMulti = avatarObject.GetComponentsInSelfAndChildren<VRCFuryHapticSocket>()
                .Where(o => o.addMenuItem)
                .ToArray()
                .Length >= 2;
            VFABool multiOn = null;
            if (enableMulti) {
                multiOn = fx.NewBool("multi", addToParamFile: true, networkSynced: false, saved: saved);
                var multiFolder = $"{spsOptions.GetOptionsPath()}/<b>Dual Mode<\\/b>\n<size=20>Allows 2 active sockets";
                manager.GetMenu().NewMenuToggle($"{multiFolder}/Enable Dual Mode", multiOn);
                manager.GetMenu().NewMenuButton($"{multiFolder}/<b>WARNING<\\/b>\n<size=20>Everyone else must use SPS or TPS - NO DPS!");
                manager.GetMenu().NewMenuButton($"{multiFolder}/<b>WARNING<\\/b>\n<size=20>Nobody else can use a hole at the same time");
                manager.GetMenu().NewMenuButton($"{multiFolder}/<b>WARNING<\\/b>\n<size=20>DO NOT ENABLE MORE THAN 2");
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

                    var bakeRoot = VRCFuryHapticSocketEditor.Bake(socket, hapticContacts);
                    if (bakeRoot == null) continue;
                    
                    addOtherFeature(new ShowInFirstPerson {
                        useObjOverride = true,
                        objOverride = bakeRoot,
                        onlyIfChildOfHead = true
                    });
                    
                    if (HapticsToggleMenuItem.Get() && !socket.sendersOnly) {
                        // Haptic receivers

                        // This is *90 because capsule length is actually "height", so we have to rotate it to make it a length
                        var capsuleRotation = Quaternion.Euler(90,0,0);
                        
                        var paramPrefix = "OGB/Orf/" + name.Replace('/','_');
                    
                        // Receivers
                        var handTouchZoneSize = VRCFuryHapticSocketEditor.GetHandTouchZoneSize(socket, manager.Avatar);
                        var haptics = GameObjects.Create("Haptics", bakeRoot);
                        if (handTouchZoneSize != null) {
                            var oscDepth = handTouchZoneSize.Item1;
                            var closeRadius = handTouchZoneSize.Item2;
                            hapticContacts.AddReceiver(haptics, Vector3.forward * -oscDepth, paramPrefix + "/TouchSelf", "TouchSelf", oscDepth, HapticUtils.SelfContacts, HapticUtils.ReceiverParty.Self, usePrefix: false, localOnly:true, useHipAvoidance: socket.useHipAvoidance);
                            hapticContacts.AddReceiver(haptics, Vector3.forward * -(oscDepth/2), paramPrefix + "/TouchSelfClose", "TouchSelfClose", closeRadius, HapticUtils.SelfContacts, HapticUtils.ReceiverParty.Self, usePrefix: false, localOnly:true, height: oscDepth, rotation: capsuleRotation, type: ContactReceiver.ReceiverType.Constant, useHipAvoidance: socket.useHipAvoidance);
                            hapticContacts.AddReceiver(haptics, Vector3.forward * -oscDepth, paramPrefix + "/TouchOthers", "TouchOthers", oscDepth, HapticUtils.BodyContacts, HapticUtils.ReceiverParty.Others, usePrefix: false, localOnly:true, useHipAvoidance: socket.useHipAvoidance);
                            hapticContacts.AddReceiver(haptics, Vector3.forward * -(oscDepth/2), paramPrefix + "/TouchOthersClose", "TouchOthersClose", closeRadius, HapticUtils.BodyContacts, HapticUtils.ReceiverParty.Others, usePrefix: false, localOnly:true, height: oscDepth, rotation: capsuleRotation, type: ContactReceiver.ReceiverType.Constant, useHipAvoidance: socket.useHipAvoidance);
                            // Legacy non-upgraded TPS detection
                            hapticContacts.AddReceiver(haptics, Vector3.forward * -oscDepth, paramPrefix + "/PenOthers", "PenOthers", oscDepth, new []{HapticUtils.CONTACT_PEN_MAIN}, HapticUtils.ReceiverParty.Others, usePrefix: false, localOnly:true, useHipAvoidance: socket.useHipAvoidance);
                            hapticContacts.AddReceiver(haptics, Vector3.forward * -(oscDepth/2), paramPrefix + "/PenOthersClose", "PenOthersClose", closeRadius, new []{HapticUtils.CONTACT_PEN_MAIN}, HapticUtils.ReceiverParty.Others, usePrefix: false, localOnly:true, height: oscDepth, rotation: capsuleRotation, type: ContactReceiver.ReceiverType.Constant, useHipAvoidance: socket.useHipAvoidance);
                            
                            var frotRadius = 0.1f;
                            var frotPos = 0.05f;
                            hapticContacts.AddReceiver(haptics, Vector3.forward * frotPos, paramPrefix + "/FrotOthers", "FrotOthers", frotRadius, new []{HapticUtils.TagTpsOrfRoot}, HapticUtils.ReceiverParty.Others, usePrefix: false, localOnly:true, useHipAvoidance: socket.useHipAvoidance);
                        }

                        hapticContacts.AddReceiver(haptics, Vector3.zero, paramPrefix + "/PenSelfNewRoot", "PenSelfNewRoot", 1f, new []{HapticUtils.CONTACT_PEN_ROOT}, HapticUtils.ReceiverParty.Self, usePrefix: false, localOnly:true, useHipAvoidance: socket.useHipAvoidance);
                        hapticContacts.AddReceiver(haptics, Vector3.zero, paramPrefix + "/PenSelfNewTip", "PenSelfNewTip", 1f, new []{HapticUtils.CONTACT_PEN_MAIN}, HapticUtils.ReceiverParty.Self, usePrefix: false, localOnly:true, useHipAvoidance: socket.useHipAvoidance);
                        hapticContacts.AddReceiver(haptics, Vector3.zero, paramPrefix + "/PenOthersNewRoot", "PenOthersNewRoot", 1f, new []{HapticUtils.CONTACT_PEN_ROOT}, HapticUtils.ReceiverParty.Others, usePrefix: false, localOnly:true, useHipAvoidance: socket.useHipAvoidance);
                        hapticContacts.AddReceiver(haptics, Vector3.zero, paramPrefix + "/PenOthersNewTip", "PenOthersNewTip", 1f, new []{HapticUtils.CONTACT_PEN_MAIN}, HapticUtils.ReceiverParty.Others, usePrefix: false, localOnly:true, useHipAvoidance: socket.useHipAvoidance);
                    }

                    // This needs to be created before we make the menu item, because it turns this off.
                    var animRoot = GameObjects.Create("Animations", bakeRoot);

                    if (socket.addMenuItem) {
                        obj.active = true;
                        _forceStateInAnimatorService.ForceEnable(obj);

                        ICollection<VFGameObject> FindChildren(params string[] names) {
                            return names.Select(n => bakeRoot.Find(n))
                                .Where(t => t != null)
                                .ToArray();
                        }

                        foreach (var child in FindChildren("Senders", "Haptics", "Lights", "Animations")) {
                            child.active = false;
                        }

                        var onLocalClip = clipFactory.NewClip($"{name} (Local)");
                        foreach (var child in FindChildren("Senders", "Haptics", "Lights", "Animations")) {
                            onLocalClip.SetEnabled(child, true);
                        }

                        var onRemoteClip = clipFactory.NewClip($"{name} (Remote)");
                        foreach (var child in FindChildren("Senders", "Lights", "Animations")) {
                            onRemoteClip.SetEnabled(child, true);
                        }

                        if (socket.enableActiveAnimation) {
                            var activeAnimParam = fx.NewFloat($"SPS - Active Animation for {name}");
                            var activeAnimLayer = fx.NewLayer($"SPS - Active Animation for {name}");
                            var off = activeAnimLayer.NewState("Off");
                            var clip = actionClipService.LoadState($"SPS - Active Animation for {name}", socket.activeActions);
                            var on = activeAnimLayer.NewState("On").WithAnimation(clip);

                            off.TransitionsTo(on).When(activeAnimParam.IsGreaterThan(0));
                            on.TransitionsTo(off).When(activeAnimParam.IsLessThan(1));

                            onLocalClip.SetAap(activeAnimParam, 1);
                            onRemoteClip.SetAap(activeAnimParam, 1);
                        }

                        var onStealthClip = clipFactory.NewClip($"{name} (Stealth)");
                        foreach (var child in FindChildren("Haptics")) {
                            onStealthClip.SetEnabled(child.gameObject, true);
                        }

                        var gizmo = obj.GetComponent<VRCFurySocketGizmo>();
                        if (gizmo != null) {
                            gizmo.show = false;
                            onLocalClip.SetCurve(gizmo, "show", 1);
                            onRemoteClip.SetCurve(gizmo, "show", 1);
                        }

                        var holeOn = fx.NewBool(name, addToParamFile: true, saved: saved);
                        var icon = socket.menuIcon?.Get();
                        manager.GetMenu().NewMenuToggle($"{spsOptions.GetMenuPath()}/{name}", holeOn, icon: icon);

                        var localTree = math.GreaterThan(stealthOn.AsFloat(), 0.5f, name: "When Local")
                            .create(onStealthClip, onLocalClip);
                        var remoteTree = math.GreaterThan(stealthOn.AsFloat(), 0.5f, name: "When Remote")
                            .create(null, onRemoteClip);
                        var onTree = math.GreaterThan(fx.IsLocal().AsFloat(), 0.5f, name: $"SPS: When {name} On")
                            .create(localTree, remoteTree);
                        directTree.Add(holeOn.AsFloat(), onTree);

                        exclusiveTriggers.Add((name, holeOn));

                        if (socket.enableAuto && autoOnClip) {
                            var autoReceiverObj = GameObjects.Create("AutoDistance", bakeRoot);
                            var distParam = hapticContacts.AddReceiver(
                                autoReceiverObj,
                                Vector3.zero,
                                name + "/AutoDistance",
                                "Receiver",
                                0.3f,
                                new[] { HapticUtils.CONTACT_PEN_MAIN },
                                party: HapticUtils.ReceiverParty.Others,
                                useHipAvoidance: socket.useHipAvoidance
                            );
                            autoReceiverObj.active = false;
                            autoOnClip.SetEnabled(autoReceiverObj, true);
                            autoSockets.Add(Tuple.Create(name, holeOn, distParam));
                        }
                    }

                    if (socket.enableDepthAnimations && socket.depthActions.Count > 0) {
                        _hapticAnimContactsService.CreateSocketAnims(
                            socket.depthActions,
                            socket.owner(),
                            animRoot,
                            name,
                            socket.unitsInMeters,
                            socket.useHipAvoidance
                        );
                    }

                    if (socket.IsValidPlugLength || socket.IsValidPlugWidth) {
                        var penTip = hapticContacts.AddReceiver(animRoot, Vector3.zero, $"{name}/LengthSensor/TipContact", "PenTip", 1f, new[] { HapticUtils.CONTACT_PEN_MAIN }, HapticUtils.ReceiverParty.Both);
                        if (socket.IsValidPlugLength) {
                            var penRoot = hapticContacts.AddReceiver(animRoot, Vector3.zero, $"{name}/LengthSensor/RootContact", "PenRoot", 1f, new[] { HapticUtils.CONTACT_PEN_ROOT }, HapticUtils.ReceiverParty.Both);
                            // Calculate `Plug Length` using `TPS_Pen_Penetrating - TPS_Pen_Root + 0.01 (radius of root sender)`
                            var plugLength = math.Add(
                                $"{name}/LengthSensor/Detected",
                                (penTip,1),
                                (penRoot,-1),
                                (0.01f, 1)
                            );
                            var validWhen = math.And(math.GreaterThan(penRoot, 0), math.LessThan(penTip, 1));
                            // We have to delay the validWhen by 1 frame, because Add takes a frame
                            var validWhenBuffered = math.Buffer(validWhen, $"{name}/LengthSensor/IsValid");
                            var plugLengthValid = math.SetValueWithConditions($"{name}/LengthSensor/Stable", (plugLength, validWhenBuffered));
                            math.Buffer(plugLengthValid, socket.plugLengthParameterName, usePrefix: false);
                        }
                        if (socket.IsValidPlugWidth) {
                            var penWidth = hapticContacts.AddReceiver(animRoot, Vector3.zero, $"{name}/WidthSensor/WidthContact", "PenWidth", 1f, new[] { HapticUtils.CONTACT_PEN_WIDTH }, HapticUtils.ReceiverParty.Both);
                            // Calculate `Plug Width` using `(TPS_Pen_Penetrating - TPS_Pen_Width)/2` 
                            var plugWidth = math.Add(
                                $"{name}/WidthSensor/Detected",
                                (penTip,0.5f),
                                (penWidth,-0.5f)
                            );
                            var validWhen = math.And(math.GreaterThan(penWidth, 0), math.LessThan(penTip, 1));
                            // We have to delay the validWhen by 1 frame, because Add takes a frame
                            var validWhenBuffered = math.Buffer(validWhen, $"{name}/WidthSensor/IsValid");
                            var plugWidthValid = math.SetValueWithConditions($"{name}/WidthSensor/Stable", (plugWidth, validWhenBuffered));
                            math.Buffer(plugWidthValid, socket.plugWidthParameterName, usePrefix: false);
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

                var vsParam = math.MakeAap("comparison", animatedFromDefaultTree: false);

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
                        math.MakeAapSafe(tree, vsParam);
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
