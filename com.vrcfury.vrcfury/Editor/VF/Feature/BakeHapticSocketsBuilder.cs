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
using VRC.Dynamics;
using VRC.SDK3.Dynamics.Contact.Components;

namespace VF.Feature {
    [VFService]
    public class BakeHapticSocketsBuilder : FeatureBuilder {

        [VFAutowired] private readonly ActionClipService actionClipService;
        [VFAutowired] private readonly RestingStateBuilder restingState;
        [VFAutowired] private readonly HapticAnimContactsService _hapticAnimContactsService;
        [VFAutowired] private readonly ParamSmoothingService paramSmoothing;
        [VFAutowired] private readonly FakeHeadService fakeHead;
        [VFAutowired] private readonly ObjectMoveService mover;
        [VFAutowired] private readonly ForceStateInAnimatorService _forceStateInAnimatorService;
        
        public const string socketsMenu = "Sockets";
        public const string optionsFolder = socketsMenu + "/<b>Options";

        [FeatureBuilderAction(FeatureOrder.BakeHapticSockets)]
        public void Apply() {
            var fx = GetFx();
            var usedNames = new List<string>();

            var enableAuto = avatarObject.GetComponentsInSelfAndChildren<VRCFuryHapticSocket>()
                .Where(o => o.addMenuItem && o.enableAuto)
                .ToArray()
                .Length >= 2;
            VFABool autoOn = null;
            AnimationClip autoOnClip = null;
            if (enableAuto) {
                autoOn = fx.NewBool("autoMode", synced: true, networkSynced: false);
                manager.GetMenu().NewMenuToggle($"{optionsFolder}/<b>Auto Mode<\\/b>\n<size=20>Activates hole nearest to a VRCFury plug", autoOn);
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
                stealthOn = fx.NewBool("stealth", synced: true);
                manager.GetMenu().NewMenuToggle($"{optionsFolder}/<b>Stealth Mode<\\/b>\n<size=20>Only local haptics,\nInvisible to others", stealthOn);
            }
            
            var enableMulti = avatarObject.GetComponentsInSelfAndChildren<VRCFuryHapticSocket>()
                .Where(o => o.addMenuItem)
                .ToArray()
                .Length >= 2;
            VFABool multiOn = null;
            if (enableMulti) {
                multiOn = fx.NewBool("multi", synced: true, networkSynced: false);
                var multiFolder = $"{optionsFolder}/<b>Dual Mode<\\/b>\n<size=20>Allows 2 active holes";
                manager.GetMenu().NewMenuToggle($"{multiFolder}/Enable Dual Mode", multiOn);
                manager.GetMenu().NewMenuButton($"{multiFolder}/<b>WARNING<\\/b>\n<size=20>Everyone else must use SPS or TPS - NO DPS!");
                manager.GetMenu().NewMenuButton($"{multiFolder}/<b>WARNING<\\/b>\n<size=20>Nobody else can use a hole at the same time");
                manager.GetMenu().NewMenuButton($"{multiFolder}/<b>WARNING<\\/b>\n<size=20>DO NOT ENABLE MORE THAN 2");
            }

            manager.GetMenu().SetIconGuid(optionsFolder, "16e0846165acaa1429417e757c53ef9b");

            var autoSockets = new List<Tuple<string, VFABool, VFAFloat>>();
            var exclusiveTriggers = new List<Tuple<VFABool, VFAState>>();
            foreach (var socket in avatarObject.GetComponentsInSelfAndChildren<VRCFuryHapticSocket>()) {
                try {
                    VFGameObject obj = socket.gameObject;
                    PhysboneUtils.RemoveFromPhysbones(socket.transform);
                    fakeHead.MarkEligible(socket.gameObject);
                    if (VRCFuryHapticSocketEditor.IsChildOfHead(socket)) {
                        var head = VRCFArmatureUtils.FindBoneOnArmatureOrNull(avatarObject, HumanBodyBones.Head);
                        mover.Move(socket.gameObject, head);
                    }
                    var (name, bakeRoot) = VRCFuryHapticSocketEditor.Bake(socket, usedNames);

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
                            clipBuilder.OneFrame(onLocalClip, obj, typeof(VRCFurySocketGizmo), "show", 1);
                            clipBuilder.OneFrame(onRemoteClip, obj, typeof(VRCFurySocketGizmo), "show", 1);
                        }

                        var holeOn = fx.NewBool(name, synced: true);
                        manager.GetMenu().NewMenuToggle($"{socketsMenu}/{name}", holeOn);

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

                        VFAState.FakeAnyState(
                            (stealthState, whenOn.And(whenLocal.And(whenStealthEnabled))),
                            (onLocalMultiState, whenOn.And(whenLocal.And(whenMultiEnabled))),
                            (onLocalState, whenOn.And(whenLocal)),
                            (onRemoteState, whenOn.And(whenStealthEnabled.Not())),
                            (offState, fx.Always())
                        );

                        exclusiveTriggers.Add(Tuple.Create(holeOn, onLocalState));

                        if (socket.enableAuto && autoOnClip) {
                            var distParam = fx.NewFloat(name + "/AutoDistance");
                            var distReceiver = HapticUtils.AddReceiver(
                                bakeRoot,
                                Vector3.zero,
                                distParam.Name(),
                                "AutoDistance",
                                0.3f,
                                new[] { HapticUtils.CONTACT_PEN_MAIN },
                                allowSelf: false
                            );
                            distReceiver.SetActive(false);
                            clipBuilder.Enable(autoOnClip, distReceiver);
                            autoSockets.Add(Tuple.Create(name, holeOn, distParam));
                        }
                    }

                    if (socket.enableDepthAnimations && socket.depthActions.Count > 0) {
                        _hapticAnimContactsService.CreateSocketAnims(
                            socket.depthActions,
                            socket.owner(),
                            animRoot,
                            name
                        );
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

            if (autoOn != null) {
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

                var states = new Dictionary<Tuple<int, int>, VFAState>();
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
                        var tree = paramSmoothing.IsBWinningTree(aDist, bDist, vsParam);
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

                        current.TransitionsTo(otherActivate).When(vsParam.IsGreaterThan(0.51f));
                        
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
