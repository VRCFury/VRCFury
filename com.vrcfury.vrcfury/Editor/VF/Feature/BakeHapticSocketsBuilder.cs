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
using VF.Model.Feature;
using VF.Service;
using VF.Utils;
using VF.Utils.Controller;
using VRC.Dynamics;
using VRC.SDK3.Dynamics.Contact.Components;

namespace VF.Feature {
    [VFService]
    public class BakeHapticSocketsBuilder : FeatureBuilder {

        [VFAutowired] private readonly ActionClipService actionClipService;
        [VFAutowired] private readonly RestingStateBuilder restingState;
        [VFAutowired] private readonly HapticAnimContactsService _hapticAnimContactsService;
        [VFAutowired] private readonly MathService math;
        [VFAutowired] private readonly FakeHeadService fakeHead;
        [VFAutowired] private readonly ObjectMoveService mover;
        [VFAutowired] private readonly ForceStateInAnimatorService _forceStateInAnimatorService;
        [VFAutowired] private readonly SpsOptionsService spsOptions;
        [VFAutowired] private readonly HapticContactsService hapticContacts;
        [VFAutowired] private readonly DirectBlendTreeService directTree;

        [FeatureBuilderAction]
        public void Apply() {
            var fx = GetFx();
            var usedNames = new List<string>();
            var saved = spsOptions.GetOptions().saveSockets;

            var enableAuto = avatarObject.GetComponentsInSelfAndChildren<VRCFuryHapticSocket>()
                .Where(o => o.addMenuItem && o.enableAuto)
                .ToArray()
                .Length >= 2;
            VFABool autoOn = null;
            AnimationClip autoOnClip = null;
            if (enableAuto) {
                autoOn = fx.NewBool("autoMode", synced: true, networkSynced: false, saved: saved);
                manager.GetMenu().NewMenuToggle($"{spsOptions.GetOptionsPath()}/<b>Auto Mode<\\/b>\n<size=20>Activates hole nearest to a VRCFury plug", autoOn);
                autoOnClip = fx.NewClip("EnableAutoReceivers");
                var autoReceiverLayer = fx.NewLayer("Auto - Enable Receivers");
                var off = autoReceiverLayer.NewState("Off");
                var on = autoReceiverLayer.NewState("On").WithAnimation(autoOnClip);
                var whenOn = autoOn.IsTrue().And(fx.IsLocal().IsTrue());
                off.TransitionsTo(on).When(whenOn);
                on.TransitionsTo(off).When(whenOn.Not());
            }
            
            var enableStealth = avatarObject.GetComponentsInSelfAndChildren<VRCFuryHapticSocket>()
                .Where(o => o.addMenuItem)
                .ToArray()
                .Length >= 1;
            VFABool stealthOn = null;
            if (enableStealth) {
                stealthOn = fx.NewBool("stealth", synced: true, saved: saved);
                manager.GetMenu().NewMenuToggle($"{spsOptions.GetOptionsPath()}/<b>Stealth Mode<\\/b>\n<size=20>Only local haptics,\nInvisible to others", stealthOn);
            }
            
            var enableMulti = avatarObject.GetComponentsInSelfAndChildren<VRCFuryHapticSocket>()
                .Where(o => o.addMenuItem)
                .ToArray()
                .Length >= 2;
            VFABool multiOn = null;
            if (enableMulti) {
                multiOn = fx.NewBool("multi", synced: true, networkSynced: false, saved: saved);
                var multiFolder = $"{spsOptions.GetOptionsPath()}/<b>Dual Mode<\\/b>\n<size=20>Allows 2 active sockets";
                manager.GetMenu().NewMenuToggle($"{multiFolder}/Enable Dual Mode", multiOn);
                manager.GetMenu().NewMenuButton($"{multiFolder}/<b>WARNING<\\/b>\n<size=20>Everyone else must use SPS or TPS - NO DPS!");
                manager.GetMenu().NewMenuButton($"{multiFolder}/<b>WARNING<\\/b>\n<size=20>Nobody else can use a hole at the same time");
                manager.GetMenu().NewMenuButton($"{multiFolder}/<b>WARNING<\\/b>\n<size=20>DO NOT ENABLE MORE THAN 2");
            }

            var autoSockets = new List<Tuple<string, VFABool, VFAFloat>>();
            var exclusiveTriggers = new List<Tuple<VFABool, VFState>>();
            foreach (var socket in avatarObject.GetComponentsInSelfAndChildren<VRCFuryHapticSocket>()) {
                try {
                    VFGameObject obj = socket.gameObject;
                    PhysboneUtils.RemoveFromPhysbones(socket.transform);
                    fakeHead.MarkEligible(socket.gameObject);
                    if (HapticUtils.IsChildOfHead(socket.owner())) {
                        var head = VRCFArmatureUtils.FindBoneOnArmatureOrNull(avatarObject, HumanBodyBones.Head);
                        mover.Move(socket.gameObject, head);
                    }
                    
                    var name = VRCFuryHapticSocketEditor.GetName(socket);
                    name = HapticUtils.GetNextName(usedNames, name);
                    Debug.Log("Baking haptic component in " + socket.owner().GetPath() + " as " + name);

                    var bakeRoot = VRCFuryHapticSocketEditor.Bake(socket, hapticContacts);

                    // Haptic receivers
                    {
                        // This is *90 because capsule length is actually "height", so we have to rotate it to make it a length
                        var capsuleRotation = Quaternion.Euler(90,0,0);
                        
                        var paramPrefix = "OGB/Orf/" + name.Replace('/','_');
                    
                        // Receivers
                        var receivers = GameObjects.Create("Receivers", bakeRoot);

                        var handTouchZoneSize = VRCFuryHapticSocketEditor.GetHandTouchZoneSize(socket, manager.Avatar);
                        if (handTouchZoneSize != null) {
                            var touchRadius = handTouchZoneSize.Value;
                            hapticContacts.AddReceiver(receivers, Vector3.zero, paramPrefix + "/TouchSelfNew", "TouchSelf", touchRadius, HapticUtils.SelfContacts, HapticUtils.ReceiverParty.Self, usePrefix: false, localOnly:true, useHipAvoidance: socket.useHipAvoidance);
                            hapticContacts.AddReceiver(receivers, Vector3.zero, paramPrefix + "/TouchOthersNew", "TouchOthers", touchRadius, HapticUtils.BodyContacts, HapticUtils.ReceiverParty.Others, usePrefix: false, localOnly:true, useHipAvoidance: socket.useHipAvoidance);
                            hapticContacts.AddReceiver(receivers, Vector3.zero, paramPrefix + "/FrotOthers", "FrotOthers", touchRadius, new []{HapticUtils.TagTpsOrfRoot}, HapticUtils.ReceiverParty.Others, usePrefix: false, localOnly:true, useHipAvoidance: socket.useHipAvoidance);
                        }

                        hapticContacts.AddReceiver(receivers, Vector3.zero, paramPrefix + "/PenSelfNewRoot", "PenSelfNewRoot", 1f, new []{HapticUtils.CONTACT_PEN_ROOT}, HapticUtils.ReceiverParty.Self, usePrefix: false, localOnly:true, useHipAvoidance: socket.useHipAvoidance);
                        hapticContacts.AddReceiver(receivers, Vector3.zero, paramPrefix + "/PenSelfNewTip", "PenSelfNewTip", 1f, new []{HapticUtils.CONTACT_PEN_MAIN}, HapticUtils.ReceiverParty.Self, usePrefix: false, localOnly:true, useHipAvoidance: socket.useHipAvoidance);
                        hapticContacts.AddReceiver(receivers, Vector3.zero, paramPrefix + "/PenOthersNewRoot", "PenOthersNewRoot", 1f, new []{HapticUtils.CONTACT_PEN_ROOT}, HapticUtils.ReceiverParty.Others, usePrefix: false, localOnly:true, useHipAvoidance: socket.useHipAvoidance);
                        hapticContacts.AddReceiver(receivers, Vector3.zero, paramPrefix + "/PenOthersNewTip", "PenOthersNewTip", 1f, new []{HapticUtils.CONTACT_PEN_MAIN}, HapticUtils.ReceiverParty.Others, usePrefix: false, localOnly:true, useHipAvoidance: socket.useHipAvoidance);
                    }

                    foreach (var receiver in bakeRoot.GetComponentsInSelfAndChildren<VRCContactReceiver>()) {
                        _forceStateInAnimatorService.DisableDuringLoad(receiver.transform);
                    }

                    // This needs to be created before we make the menu item, because it turns this off.
                    var animRoot = GameObjects.Create("Animations", bakeRoot.transform);

                    if (socket.addMenuItem) {
                        obj.active = true;
                        _forceStateInAnimatorService.ForceEnable(obj);

                        ICollection<VFGameObject> FindChildren(params string[] names) {
                            return names.Select(n => bakeRoot.Find(n))
                                .Where(t => t != null)
                                .ToArray();
                        }

                        foreach (var child in FindChildren("Senders", "Receivers", "Lights", "VersionLocal",
                                     "VersionBeacon", "Animations")) {
                            child.active = false;
                        }

                        var onLocalClip = fx.NewClip($"{name} (Local)");
                        foreach (var child in FindChildren("Senders", "Receivers", "Lights", "VersionLocal",
                                     "Animations")) {
                            clipBuilder.Enable(onLocalClip, child.gameObject);
                        }

                        var onRemoteClip = fx.NewClip($"{name} (Remote)");
                        foreach (var child in FindChildren("Senders", "Lights", "VersionBeacon", "Animations")) {
                            clipBuilder.Enable(onRemoteClip, child.gameObject);
                        }

                        if (socket.enableActiveAnimation) {
                            var additionalActiveClip = actionClipService.LoadState("socketActive", socket.activeActions);
                            onLocalClip.CopyFrom(additionalActiveClip);
                            onRemoteClip.CopyFrom(additionalActiveClip);
                        }

                        var onStealthClip = fx.NewClip($"{name} (Stealth)");
                        foreach (var child in FindChildren("Receivers", "VersionLocal")) {
                            clipBuilder.Enable(onStealthClip, child.gameObject);
                        }

                        var gizmo = obj.GetComponent<VRCFurySocketGizmo>();
                        if (gizmo != null) {
                            gizmo.show = false;
                            var path = clipBuilder.GetPath(obj);
                            onLocalClip.SetCurve(EditorCurveBinding.FloatCurve(path, typeof(VRCFurySocketGizmo), "show"), 1);
                            onRemoteClip.SetCurve(EditorCurveBinding.FloatCurve(path, typeof(VRCFurySocketGizmo), "show"), 1);
                        }

                        var holeOn = fx.NewBool(name, synced: true, saved: saved);
                        var icon = socket.menuIcon?.Get();
                        manager.GetMenu().NewMenuToggle($"{spsOptions.GetMenuPath()}/{name}", holeOn, icon: icon);

                        var layer = fx.NewLayer(name);
                        var offState = layer.NewState("Off");
                        var stealthState = layer.NewState("On Local Stealth").WithAnimation(onStealthClip)
                            .Move(offState, 1, 0);
                        var onLocalMultiState = layer.NewState("On Local Multi").WithAnimation(onLocalClip);
                        var onLocalState = layer.NewState("On Local").WithAnimation(onLocalClip);
                        var onRemoteState = layer.NewState("On Remote").WithAnimation(onRemoteClip);

                        var whenOn = holeOn.IsTrue();
                        var whenLocal = fx.IsLocal().IsTrue();
                        var whenStealthEnabled = stealthOn?.IsTrue() ?? fx.Never();
                        var whenMultiEnabled = multiOn?.IsTrue() ?? fx.Never();

                        VFState.FakeAnyState(
                            (stealthState, whenOn.And(whenLocal.And(whenStealthEnabled))),
                            (onLocalMultiState, whenOn.And(whenLocal.And(whenMultiEnabled))),
                            (onLocalState, whenOn.And(whenLocal)),
                            (onRemoteState, whenOn.And(whenStealthEnabled.Not())),
                            (offState, fx.Always())
                        );

                        exclusiveTriggers.Add(Tuple.Create(holeOn, onLocalState));

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
                            clipBuilder.Enable(autoOnClip, autoReceiverObj);
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
                        var penTip = hapticContacts.AddReceiver(animRoot, Vector3.zero, $"{name}/PenTip", "PenTip", 1f, new[] { HapticUtils.CONTACT_PEN_MAIN }, HapticUtils.ReceiverParty.Both);
                        if (socket.IsValidPlugLength) {
                            var penRoot = hapticContacts.AddReceiver(animRoot, Vector3.zero, $"{name}/PenRoot", "PenRoot", 1f, new[] { HapticUtils.CONTACT_PEN_ROOT }, HapticUtils.ReceiverParty.Both);
                            // Calculate `Plug Length` using `TPS_Pen_Penetrating - TPS_Pen_Root + 0.01 (radius of root sender)`
                            var plugLength = math.Subtract(penTip, penRoot);
                            directTree.Add(math.MakeSetter(plugLength, 0.01f));
                            var validWhen = math.And(math.GreaterThan(penRoot, 0), math.LessThan(penTip, 1));
                            var plugLengthValid = math.SetValueWithConditions("ValidPlugLength", (plugLength, validWhen));
                            var plugLengthGlobal = fx.NewFloat(socket.plugLengthParameterName, usePrefix: false);
                            directTree.Add(math.MakeCopier(plugLengthValid, plugLengthGlobal));
                        }
                        if (socket.IsValidPlugWidth) {
                            var penWidth = hapticContacts.AddReceiver(animRoot, Vector3.zero, $"{name}/PenWidth", "PenWidth", 1f, new[] { HapticUtils.CONTACT_PEN_WIDTH }, HapticUtils.ReceiverParty.Both);
                            // Calculate `Plug Width` using `(TPS_Pen_Penetrating - TPS_Pen_Width)/2` 
                            var doubleWidth = math.Subtract(penTip, penWidth);
                            var plugWidth = math.Multiply(socket.plugWidthParameterName, doubleWidth, 0.5f);
                            var validWhen = math.And(math.GreaterThan(penWidth, 0), math.LessThan(penTip, 1));
                            var plugWidthValid = math.SetValueWithConditions("ValidPlugWidth", (plugWidth, validWhen));
                            var plugWidthGlobal = fx.NewFloat(socket.plugWidthParameterName, usePrefix: false);
                            directTree.Add(math.MakeCopier(plugWidthValid, plugWidthGlobal));
                        }
                    }
                } catch (Exception e) {
                    throw new ExceptionWithCause($"Failed to bake Haptic Socket: {socket.owner().GetPath()}", e);
                }
            }

            foreach (var i in Enumerable.Range(0, exclusiveTriggers.Count)) {
                var (_, state) = exclusiveTriggers[i];
                foreach (var j in Enumerable.Range(0, exclusiveTriggers.Count)) {
                    if (i == j) continue;
                    var (param, _) = exclusiveTriggers[j];
                    state.Drives(param, false);
                }
            }

            if (autoOn != null && autoSockets.Count > 0) {
                var layer = fx.NewLayer("Auto Socket Mode");
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

                var vsParam = fx.NewFloat("comparison");

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
                        var tree = math.MakeDirect($"{aName} vs {bName}");
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

                        current.TransitionsTo(otherActivate).When(vsParam.IsGreaterThan(0));
                        
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
