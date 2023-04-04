using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using VF.Builder;
using VF.Builder.Exceptions;
using VF.Builder.Haptics;
using VF.Component;
using VF.Feature.Base;
using VF.Inspector;
using VF.Model;
using VF.Model.Feature;
using VF.Model.StateAction;
using VRC.Dynamics;
using VRC.SDK3.Dynamics.Contact.Components;
using Object = UnityEngine.Object;

namespace VF.Feature {
    public class BakeHapticsBuilder : FeatureBuilder {

        [FeatureBuilderAction(FeatureOrder.BakeHaptics)]
        public void Apply() {
            var usedNames = new List<string>();
            var fakeHead = allBuildersInRun.OfType<FakeHeadBuilder>().First();

            // When you first load into a world, contact receivers already touching a sender register as 0 proximity
            // until they are removed and then reintroduced to each other.
            var objectsToDisableTemporarily = new HashSet<GameObject>();
            // This is here so if users have an existing toggle that turns off holes, we forcefully turn it back
            // on if it's managed by our new menu system.
            var objectsToForceEnable = new HashSet<GameObject>();
            
            foreach (var c in avatarObject.GetComponentsInChildren<VRCFuryHapticPlug>(true)) {
                var bakeInfo = VRCFuryHapticPlugEditor.Bake(c, usedNames, tmpDir: tmpDir);

                if (bakeInfo != null) {
                    var (name, bakeRoot, renderer, worldLength, worldRadius) = bakeInfo;
                    foreach (var r in bakeRoot.GetComponentsInChildren<VRCContactReceiver>(true)) {
                        objectsToDisableTemporarily.Add(r.gameObject);
                    }
                }
            }

            var holesMenu = "Holes";
            var optionsFolder = $"{holesMenu}/<b>Hole Options";

            var enableAuto = avatarObject.GetComponentsInChildren<VRCFuryHapticSocket>(true)
                .Where(o => o.addMenuItem && o.enableAuto)
                .ToArray()
                .Length >= 2;
            VFABool autoOn = null;
            AnimationClip autoOnClip = null;
            if (enableAuto) {
                var fx = GetFx();
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
            
            var enableStealth = avatarObject.GetComponentsInChildren<VRCFuryHapticSocket>(true)
                .Where(o => o.addMenuItem)
                .ToArray()
                .Length >= 1;
            VFABool stealthOn = null;
            if (enableStealth) {
                var fx = GetFx();
                stealthOn = fx.NewBool("stealth", synced: true);
                manager.GetMenu().NewMenuToggle($"{optionsFolder}/<b>Stealth Mode<\\/b>\n<size=20>Only local haptics,\nInvisible to others", stealthOn);
            }
            
            var enableMulti = avatarObject.GetComponentsInChildren<VRCFuryHapticSocket>(true)
                .Where(o => o.addMenuItem)
                .ToArray()
                .Length >= 2;
            VFABool multiOn = null;
            if (enableMulti) {
                var fx = GetFx();
                multiOn = fx.NewBool("multi", synced: true, networkSynced: false);
                var multiFolder = $"{optionsFolder}/<b>Dual Mode<\\/b>\n<size=20>Allows 2 active holes";
                manager.GetMenu().NewMenuToggle($"{multiFolder}/Enable Dual Mode", multiOn);
                manager.GetMenu().NewMenuButton($"{multiFolder}/<b>WARNING<\\/b>\n<size=20>Everyone else must use TPS, >NO DPS!<");
                manager.GetMenu().NewMenuButton($"{multiFolder}/<b>WARNING<\\/b>\n<size=20>Nobody else can use a hole at the same time");
                manager.GetMenu().NewMenuButton($"{multiFolder}/<b>WARNING<\\/b>\n<size=20>DO NOT ENABLE MORE THAN 2");
            }

            manager.GetMenu().SetIconGuid(optionsFolder, "16e0846165acaa1429417e757c53ef9b");

            var autoSockets = new List<Tuple<string, VFABool, VFANumber>>();
            var exclusiveTriggers = new List<Tuple<VFABool, VFAState>>();
            foreach (var c in avatarObject.GetComponentsInChildren<VRCFuryHapticSocket>(true)) {
                fakeHead.MarkEligible(c.gameObject);
                var (name,bakeRoot) = VRCFuryHapticSocketEditor.Bake(c, usedNames);
                
                foreach (var r in bakeRoot.GetComponentsInChildren<VRCContactReceiver>(true)) {
                    objectsToDisableTemporarily.Add(r.gameObject);
                }
                
                var animRoot = new GameObject("Animations");
                animRoot.transform.SetParent(bakeRoot.transform, false);

                if (c.addMenuItem) {
                    var fx = GetFx();

                    c.gameObject.SetActive(true);
                    objectsToForceEnable.Add(c.gameObject);

                    ICollection<GameObject> FindChildren(params string[] names) {
                        return names.Select(n => bakeRoot.transform.Find(n))
                            .Where(t => t != null)
                            .Select(t => t.gameObject)
                            .ToArray();
                    }

                    foreach (var obj in FindChildren("Senders", "Receivers", "Lights", "VersionLocal", "VersionBeacon", "Animations")) {
                        obj.SetActive(false);
                    }
                    var onLocalClip = fx.NewClip($"{name} (Local)");
                    foreach (var obj in FindChildren("Senders", "Receivers", "Lights", "VersionLocal", "Animations")) {
                        clipBuilder.Enable(onLocalClip, obj);
                    }
                    var onRemoteClip = fx.NewClip($"{name} (Remote)");
                    foreach (var obj in FindChildren("Senders", "Lights", "VersionBeacon", "Animations")) {
                        clipBuilder.Enable(onRemoteClip, obj);
                    }
                    var onStealthClip = fx.NewClip($"{name} (Stealth)");
                    foreach (var obj in FindChildren("Receivers", "VersionLocal")) {
                        clipBuilder.Enable(onStealthClip, obj);
                    }
                    
                    var holeOn = fx.NewBool(name, synced: true);
                    manager.GetMenu().NewMenuToggle($"Holes/{name}", holeOn);

                    var layer = fx.NewLayer(name);
                    var offState = layer.NewState("Off");
                    var stealthState = layer.NewState("On Local Stealth").WithAnimation(onStealthClip).Move(offState, 1, 0);
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

                    if (c.enableAuto && autoOnClip) {
                        var distParam = fx.NewFloat(name + "/AutoDistance");
                        var distReceiver = HapticUtils.AddReceiver(bakeRoot, Vector3.zero, distParam.Name(), "AutoDistance", 0.3f,
                            new[] { HapticUtils.CONTACT_PEN_MAIN });
                        distReceiver.SetActive(false);
                        clipBuilder.Enable(autoOnClip, distReceiver);
                        autoSockets.Add(Tuple.Create(name, holeOn, distParam));
                    }
                }

                var actionNum = 0;
                foreach (var depthAction in c.depthActions) {
                    actionNum++;
                    var prefix = name + actionNum;
                    if (depthAction.state == null || depthAction.state.IsEmpty()) continue;

                    var minDepth = depthAction.minDepth;

                    var maxDepth = depthAction.maxDepth;
                    if (maxDepth <= minDepth) maxDepth = 0.25f;
                    if (maxDepth <= minDepth) continue;

                    var length = maxDepth - minDepth;

                    var fx = GetFx();

                    var contactingRootParam = fx.NewBool(prefix + "/AnimContacting");
                    HapticUtils.AddReceiver(animRoot, Vector3.forward * -minDepth, contactingRootParam.Name(), "AnimRoot" + actionNum, 0.01f, new []{HapticUtils.CONTACT_PEN_MAIN}, allowSelf:depthAction.enableSelf, type: ContactReceiver.ReceiverType.Constant);
                    
                    var depthParam = fx.NewFloat(prefix + "/AnimDepth");
                    HapticUtils.AddReceiver(animRoot, Vector3.forward * -(minDepth + length), depthParam.Name(), "AnimInside" + actionNum, length, new []{HapticUtils.CONTACT_PEN_MAIN}, allowSelf:depthAction.enableSelf);

                    var layer = fx.NewLayer("Depth Animation " + actionNum + " for " + name);
                    var off = layer.NewState("Off");
                    var on = layer.NewState("On");

                    var clip = LoadState(prefix, depthAction.state);
                    if (ClipBuilder.IsStaticMotion(clip)) {
                        var tree = fx.NewBlendTree(prefix + " tree");
                        tree.blendType = BlendTreeType.Simple1D;
                        tree.useAutomaticThresholds = false;
                        tree.blendParameter = depthParam.Name();
                        tree.AddChild(fx.GetNoopClip(), 0);
                        tree.AddChild(clip, 1);
                        on.WithAnimation(tree);
                    } else {
                        on.WithAnimation(clip).MotionTime(depthParam);
                    }

                    var onWhen = depthParam.IsGreaterThan(0).And(contactingRootParam.IsTrue());
                    off.TransitionsTo(on).When(onWhen);
                    on.TransitionsTo(off).When(onWhen.Not());
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
                var fx = GetFx();
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
                var vs1 = fx.NewClip("vs1");
                vs1.SetCurve("", typeof(Animator), vsParam.Name(), AnimationCurve.Constant(0, 0, 1f));
                var vs0 = fx.NewClip("vs0");
                vs0.SetCurve("", typeof(Animator), vsParam.Name(), AnimationCurve.Constant(0, 0, 0f));

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
                        var tree = fx.NewBlendTree($"{aName} vs {bName}");
                        tree.useAutomaticThresholds = false;
                        tree.blendType = BlendTreeType.FreeformCartesian2D;
                        tree.AddChild(vs0, new Vector2(1f, 0));
                        tree.AddChild(vs1, new Vector2(0, 1f));
                        tree.blendParameter = aDist.Name();
                        tree.blendParameterY = bDist.Name();
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

                start.TransitionsTo(states[Tuple.Create(0, 1)]).When(fx.Always());
            }

            if (objectsToDisableTemporarily.Count > 0) {
                var fx = GetFx();
                var layer = fx.NewLayer("Haptics Off Temporarily Upon Load");
                var off = layer.NewState("Off");
                var on = layer.NewState("On");
                off.TransitionsTo(on).When().WithTransitionExitTime(1);
                
                var firstFrameClip = fx.NewClip("Load (First Frame)");
                foreach (var obj in objectsToDisableTemporarily) {
                    clipBuilder.Enable(firstFrameClip, obj, false);
                }
                off.WithAnimation(firstFrameClip);
                
                var onClip = fx.NewClip("Load (On)");
                foreach (var obj in objectsToForceEnable) {
                    clipBuilder.Enable(onClip, obj);
                }
                on.WithAnimation(onClip);
            }
        }
    }
}
