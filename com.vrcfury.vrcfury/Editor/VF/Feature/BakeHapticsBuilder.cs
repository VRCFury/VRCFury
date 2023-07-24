using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using VF.Builder;
using VF.Builder.Haptics;
using VF.Component;
using VF.Feature.Base;
using VF.Inspector;
using VF.Model.Feature;
using VF.Utils;
using VRC.Dynamics;
using VRC.SDK3.Dynamics.Contact.Components;

namespace VF.Feature {
    public class BakeHapticsBuilder : FeatureBuilder {

        private List<(VFGameObject, VFGameObject)> spsRewritesToDo
            = new List<(VFGameObject, VFGameObject)>();

        [FeatureBuilderAction(FeatureOrder.HapticsAnimationRewrites)]
        public void ApplySpsRewrites() {
            foreach (var (plugObj, rendererObj) in spsRewritesToDo) {
                var pathToPlug = plugObj.GetPath(avatarObject);
                var pathToRenderer = rendererObj.GetPath(avatarObject);
                var spsEnabledBinding = EditorCurveBinding.FloatCurve(
                    pathToRenderer,
                    typeof(SkinnedMeshRenderer),
                    "material._SPS_Enabled"
                );
                var hapticsEnabledBinding = EditorCurveBinding.FloatCurve(
                    pathToPlug,
                    typeof(GameObject),
                    "m_IsActive"
                );
                foreach (var c in manager.GetAllUsedControllers()) {
                    foreach (var clip in c.GetClips()) {
                        foreach (var binding in clip.GetFloatBindings()) {
                            if (binding.path == pathToRenderer) {
                                if (binding.propertyName == "material._TPS_AnimatedToggle") {
                                    clip.SetFloatCurve(spsEnabledBinding, clip.GetFloatCurve(binding));
                                    clip.SetFloatCurve(hapticsEnabledBinding, clip.GetFloatCurve(binding));
                                }
                            }
                            if (binding.path == pathToPlug) {
                                if (binding.propertyName == "spsAnimatedEnabled") {
                                    clip.SetFloatCurve(spsEnabledBinding, clip.GetFloatCurve(binding));
                                    clip.SetFloatCurve(hapticsEnabledBinding, clip.GetFloatCurve(binding));
                                }
                            }
                        }
                    }
                }
            }
        }

        [FeatureBuilderAction(FeatureOrder.BakeHaptics)]
        public void Apply() {
            var usedNames = new List<string>();
            var plugRenderers = new Dictionary<VFGameObject, VRCFuryHapticPlug>();
            var fakeHead = allBuildersInRun.OfType<FakeHeadBuilder>().First();

            // When you first load into a world, contact receivers already touching a sender register as 0 proximity
            // until they are removed and then reintroduced to each other.
            var objectsToDisableTemporarily = new HashSet<VFGameObject>();
            // This is here so if users have an existing toggle that turns off sockets, we forcefully turn it back
            // on if it's managed by our new menu system.
            var objectsToForceEnable = new HashSet<VFGameObject>();
            
            var socketsMenu = "Sockets";
            var optionsFolder = $"{socketsMenu}/<b>Options";

            AnimationClip tipLightOnClip = null;
            
            foreach (var plug in avatarObject.GetComponentsInSelfAndChildren<VRCFuryHapticPlug>()) {
                PhysboneUtils.RemoveFromPhysbones(plug.transform);
                var bakeInfo = VRCFuryHapticPlugEditor.Bake(plug, usedNames, plugRenderers, mutableManager: mutableManager);

                if (bakeInfo == null) continue;

                var (name, bakeRoot, renderers, worldLength, worldRadius) = bakeInfo;
                foreach (var r in bakeRoot.GetComponentsInSelfAndChildren<VRCContactReceiver>()) {
                    objectsToDisableTemporarily.Add(r.transform);
                }

                if (plug.configureTps || plug.enableSps) {
                    foreach (var renderer in renderers) {
                        addOtherFeature(new TpsScaleFix() { singleRenderer = renderer });
                        if (renderer is SkinnedMeshRenderer skin) {
                            addOtherFeature(new BoundingBoxFix2() { skipRenderer = skin });
                        }
                    }
                }

                var postBakeClip = LoadState("sps_postbake", plug.postBakeActions, plug.owner());
                ApplyClipToRestingState(postBakeClip);

                if (plug.enableSps) {
                    foreach (var renderer in renderers) {
                        spsRewritesToDo.Add((plug.owner(), renderer.owner()));
                    }
                }

                if (plug.addDpsTipLight) {
                    var tip = GameObjects.Create("LegacyDpsTip", bakeRoot);
                    tip.active = false;
                    var light = tip.AddComponent<Light>();
                    light.type = LightType.Point;
                    light.color = Color.black;
                    light.range = 0.49f;
                    light.shadows = LightShadows.None;
                    light.renderMode = LightRenderMode.ForceVertex;

                    if (tipLightOnClip == null) {
                        var fx = GetFx();
                        var param = fx.NewBool("tipLight", synced: true);
                        manager.GetMenu().NewMenuToggle($"{optionsFolder}/<b>DPS Tip Light<\\/b>\n<size=20>Allows plugs to trigger old DPS animations", param);
                        tipLightOnClip = fx.NewClip("EnableAutoReceivers");
                        var layer = fx.NewLayer("Tip Light");
                        var off = layer.NewState("Off");
                        var on = layer.NewState("On").WithAnimation(tipLightOnClip);
                        var whenOn = param.IsTrue();
                        off.TransitionsTo(on).When(whenOn);
                        on.TransitionsTo(off).When(whenOn.Not());
                    }
                    clipBuilder.Enable(tipLightOnClip, tip);
                }
            }

            var enableAuto = avatarObject.GetComponentsInSelfAndChildren<VRCFuryHapticSocket>()
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
            
            var enableStealth = avatarObject.GetComponentsInSelfAndChildren<VRCFuryHapticSocket>()
                .Where(o => o.addMenuItem)
                .ToArray()
                .Length >= 1;
            VFABool stealthOn = null;
            if (enableStealth) {
                var fx = GetFx();
                stealthOn = fx.NewBool("stealth", synced: true);
                manager.GetMenu().NewMenuToggle($"{optionsFolder}/<b>Stealth Mode<\\/b>\n<size=20>Only local haptics,\nInvisible to others", stealthOn);
            }
            
            var enableMulti = avatarObject.GetComponentsInSelfAndChildren<VRCFuryHapticSocket>()
                .Where(o => o.addMenuItem)
                .ToArray()
                .Length >= 2;
            VFABool multiOn = null;
            if (enableMulti) {
                var fx = GetFx();
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
                VFGameObject obj = socket.gameObject;
                PhysboneUtils.RemoveFromPhysbones(socket.transform);
                fakeHead.MarkEligible(socket.gameObject);
                var (name,bakeRoot) = VRCFuryHapticSocketEditor.Bake(socket, usedNames);
                
                foreach (var receiver in bakeRoot.GetComponentsInSelfAndChildren<VRCContactReceiver>()) {
                    objectsToDisableTemporarily.Add(receiver.transform);
                }
                
                // This needs to be created before we make the menu item, because it turns this off.
                var animRoot = GameObjects.Create("Animations", bakeRoot.transform);

                if (socket.addMenuItem) {
                    var fx = GetFx();

                    obj.active = true;
                    objectsToForceEnable.Add(obj);

                    ICollection<VFGameObject> FindChildren(params string[] names) {
                        return names.Select(n => bakeRoot.Find(n))
                            .Where(t => t != null)
                            .ToArray();
                    }

                    var additionalActiveClip = LoadState("socketActive", socket.activeActions);

                    foreach (var child in FindChildren("Senders", "Receivers", "Lights", "VersionLocal", "VersionBeacon", "Animations")) {
                        child.active = false;
                    }
                    var onLocalClip = fx.NewClip($"{name} (Local)");
                    ClipRewriter.Copy(additionalActiveClip, onLocalClip);
                    foreach (var child in FindChildren("Senders", "Receivers", "Lights", "VersionLocal", "Animations")) {
                        clipBuilder.Enable(onLocalClip, child.gameObject);
                    }
                    var onRemoteClip = fx.NewClip($"{name} (Remote)");
                    ClipRewriter.Copy(additionalActiveClip, onRemoteClip);
                    foreach (var child in FindChildren("Senders", "Lights", "VersionBeacon", "Animations")) {
                        clipBuilder.Enable(onRemoteClip, child.gameObject);
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

                    if (socket.enableAuto && autoOnClip) {
                        var distParam = fx.NewFloat(name + "/AutoDistance");
                        var distReceiver = HapticUtils.AddReceiver(bakeRoot, Vector3.zero, distParam.Name(), "AutoDistance", 0.3f,
                            new[] { HapticUtils.CONTACT_PEN_MAIN });
                        distReceiver.SetActive(false);
                        clipBuilder.Enable(autoOnClip, distReceiver);
                        autoSockets.Add(Tuple.Create(name, holeOn, distParam));
                    }
                }

                var actionNum = 0;
                foreach (var depthAction in socket.depthActions) {
                    actionNum++;
                    var prefix = name + actionNum;

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

                    var clip = LoadState(prefix, depthAction.state, socket.owner());
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

                var firstSocket = autoSockets[0];
                // If this isn't here, the first socket will never activate unless another one is already active
                start.TransitionsTo(states[Tuple.Create(0, -1)])
                    .When(firstSocket.Item2.IsFalse().And(firstSocket.Item3.IsGreaterThan(0)));
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
                    clipBuilder.Enable(firstFrameClip, obj.gameObject, false);
                }
                off.WithAnimation(firstFrameClip);
                
                var onClip = fx.NewClip("Load (On)");
                foreach (var obj in objectsToForceEnable) {
                    clipBuilder.Enable(onClip, obj.gameObject);
                }
                on.WithAnimation(onClip);
            }
        }
    }
}
