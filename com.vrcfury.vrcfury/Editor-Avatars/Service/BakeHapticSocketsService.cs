using Party = VF.Builder.Haptics.SelfAndOthersReceiverService.Party;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
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
using VRC.SDK3.Dynamics.Contact.Components;
using VRC.SDK3.Avatars.ScriptableObjects;

namespace VF.Service {
    [VFService]
    internal class BakeHapticSocketsService {

        [VFAutowired] private readonly SelfAndOthersReceiverAnimatorService receiverAnimator;
        [VFAutowired] private readonly GlobalsService globals;
        [VFAutowired] private readonly VFGameObject avatarObject;
        [VFAutowired] private readonly ActionClipService actionClipService;
        [VFAutowired] private readonly SpsDepthAnimationService depthAnimations;
        [VFAutowired] private readonly SpsOptionsService spsOptions;
        [VFAutowired] private readonly HapticContactsService hapticContacts;
        [VFAutowired] private readonly SpsSocketMeasurementsService socketMeasurements;
        [VFAutowired] private readonly ExpressionParamsService expressionParams;
        [VFAutowired] private readonly DbtLayerService directTreeService;
        [VFAutowired] private readonly ClipFactoryService clipFactory;
        [VFAutowired] private readonly ObjectTreeService objectTrees;
        [VFAutowired] private readonly IsObjectEnabledService objectEnabled;
        [VFAutowired] private readonly VRCAvatarDescriptor avatar;
        [VFAutowired] private readonly ControllersService controllers;
        [VFAutowired] private readonly VRCFuryHapticSocketBaker socketBaker;
        [VFAutowired] private readonly SpsAutoChannelGenerator autoChannelGenerator;
        [VFAutowired] private readonly ParameterInjectService parameterInjectService;
        private VFAvatarController fx => controllers.GetFx();
        private VFAvatarController actionController => controllers.GetAction();
        [VFAutowired] private readonly MenuService menuService;
        private MenuManager menu => menuService.GetMenu();
        private readonly List<VFGameObject> forceEnable = new List<VFGameObject>();


        [FeatureBuilderAction(FeatureOrder.ForceStateInAnimator)]
        public void ForceSocketsOn() {
            if (forceEnable.Count == 0) return;

            var layer = fx.NewLayer("Force Sockets On");
            var clip = clipFactory.NewClip("Force Sockets On");
            foreach (var obj in forceEnable) {
                clip.SetEnabled(obj, true);
            }
            layer.NewState("On").WithAnimation(clip);
        }

        [FeatureBuilderAction]
        public void Apply() {
            var options = spsOptions.GetOptions();
            var saved = options.saveSockets;

            var enableAuto = options.autoModeAvailable
                && avatarObject.GetComponentsInSelfAndChildren<VRCFuryHapticSocket>().Any(socket => socket.addMenuItem);
            VFAParam autoOn = null;
            VFClip autoOnClip = null;
            if (enableAuto) {
                autoOn = spsOptions.GetAutoParameter();
                autoOnClip = clipFactory.NewClip("Enable SPS Auto Contacts");
                var directTree = directTreeService.Create($"Auto Mode Toggle");
                directTree.Add(
                    BlendtreeMath.GreaterThan(fx.IsLocal(), 0, name: "SPS: Auto Contacts").And(
                    BlendtreeMath.GreaterThan(autoOn, 0, name: "When Local")
                ).create(autoOnClip, null));
            }

            var hasSockets = avatarObject.GetComponentsInSelfAndChildren<VRCFuryHapticSocket>().Any();
            var legacyOn = hasSockets ? spsOptions.GetLegacyParameter() : null;
            var legacyOnClip = hasSockets ? spsOptions.GetLegacyOnClip() : null;

            var autoSockets = new List<Tuple<string, VFAParam, VFGameObject>>();
            var exclusiveTriggers = new List<(string,VFAParam)>();
            var usedMenuNames = new HashSet<string>();
            var usedOscIds = new HashSet<string>();
            foreach (var socket in avatarObject.GetComponentsInSelfAndChildren<VRCFuryHapticSocket>()) {
                try {
                    VFGameObject obj = socket.owner();
                    PhysboneUtils.RemoveFromPhysbones(socket.owner());

                    var preferredMenuName = VRCFuryHapticSocketEditor.GetMenuName(socket);
                    var menuName = HapticUtils.MakeUniqueId(usedMenuNames, preferredMenuName);
                    var oscId = HapticUtils.MakeUniqueOgbId(
                        usedOscIds,
                        HapticUtils.GetPreferredId(socket, s => s.oscId, _ => preferredMenuName)
                    );
                    Debug.Log("Baking haptic component in " + socket.owner().GetDebugPath() + " as " + oscId);

                    VFAParam toggleParam = null;
                    if (socket.addMenuItem) {
                        toggleParam = fx.CreateUniqueBool(oscId);
                        toggleParam.MakeExpressionBool(saved: saved);
                        var icon = socket.menuIcon?.Get();
                        menu.NewMenuToggle($"{spsOptions.GetMenuPath()}/{menuName}", toggleParam, icon: icon);
                    }

                    if (!BuildTargetUtils.IsDesktop()) {
                        continue;
                    }

                    var bakeResult = socketBaker.Bake(socket, legacyPlugCompatibility: options.legacyModeAvailable,
                        tpsDepthAnimationCompatibility: options.tpsDepthAnimationCompatibility, avatarRoot: avatarObject);
                    globals.addOtherFeature(new ShowInFirstPerson {
                        useObjOverride = true,
                        objOverride = bakeResult.bakeRoot,
                        onlyIfChildOfHead = true
                    });

                    var mainReceivers = socketMeasurements.GetReceivers(socket, bakeResult.worldSpace, oscId);

                    if (HapticsToggleMenuItem.Get()) {
                        var paramPrefix = "OGB/Socket/" + oscId;
                        expressionParams.AddOrMerge(ExpressionParam.Create(paramPrefix + "/DepthRadius")
                            .Float().Default(mainReceivers.request.worldRadius).Unsynced());

                        if (VRCFuryHapticSocketBaker.ShouldSupportOgbTouch(socket, autoChannelGenerator.GetClosestBone(socket.owner()))) {
                            var radius = (autoChannelGenerator.GetAvatarViewPosition(socket.owner())?.y ?? 0) * 0.03f;
                            if (radius > 0) {
                                foreach (var self in new[] { true, false }) {
                                    hapticContacts.AddReceiver(new HapticContactsService.ReceiverRequest {
                                        obj = bakeResult.worldSpace,
                                        radius = radius,
                                        paramName = paramPrefix + (self ? "/TouchSelf" : "/TouchOthers"),
                                        objName = self ? "Receiver - Touch (Self)" : "Receiver - Touch (Others)",
                                        tags = HapticUtils.SelfContacts,
                                        party = self ? HapticUtils.ReceiverParty.Self : HapticUtils.ReceiverParty.Others,
                                        localOnly = true,
                                        usePrefix = false,
                                        enableWithOgb = true
                                    });
                                }
                            }
                        }

                        foreach (var self in new[] { true, false }) {
                            receiverAnimator.GetParameter(mainReceivers, self, paramPrefix + (self ? "/DepthSelf" : "/DepthOthers")
#if VRCSDK_HAS_CONTACT_PARAMETER_FLAGS
                                , ContactReceiver.AdvancedParameter.All
#endif
                            );
                        }
                    }

                    var math = new Lazy<BlendtreeMath>(() => new BlendtreeMath(fx, objectTrees.Get(bakeResult.worldSpace)));
                    var measurements = new Lazy<Func<Party, Func<string, VFAParam>>>(() =>
                        socketMeasurements.GetMeasurements(socket, mainReceivers, oscId));

                    if (socket.depthActions2.Count > 0) {
                        depthAnimations.CreateAnims(
                            socket.depthActions2,
                            socket.owner(),
                            oscId,
                            party => measurements.Value(party),
                            bakeResult.worldSpace
                        );
                    }

                    var injectRequests = parameterInjectService.GetRequests()
                        .Where(r => r.sourceObject == socket.owner())
                        .ToList();
                    if (socket.IsValidPlugLength) {
                        injectRequests.Add(new ParameterInjectService.Request() {
                            sourceObject = socket.owner(),
                            resolvedParam = socket.plugLengthParameterName,
                            sourceParam = VRCFuryHapticPlugEditor.SpsPlugLengthMeters
                        });
                    }
                    if (socket.IsValidPlugWidth) {
                        injectRequests.Add(new ParameterInjectService.Request() {
                            sourceObject = socket.owner(),
                            resolvedParam = socket.plugWidthParameterName,
                            sourceParam = VRCFuryHapticPlugEditor.SpsPlugRadiusMeters
                        });
                    }

                    foreach (var inject in injectRequests) {
                        var value = measurements.Value(Party.Merged)(inject.sourceParam);
                        if (value != null) {
                            math.Value.CopyInPlace(value, inject.resolvedParam);
                        }
                    }

                    if (legacyOnClip != null) {
                        foreach (var child in new[] { bakeResult.legacyLights, bakeResult.legacyFrontSender }.NotNull()) {
                            spsOptions.RegisterLegacyObject(child);
                        }
                        var legacyGizmo = obj.GetComponent<VRCFurySocketGizmo>();
                        if (legacyGizmo != null) {
                            legacyGizmo.data.useLegacyLights = false;
                            legacyOnClip.SetCurve(legacyGizmo, "data.useLegacyLights", bakeResult.legacyLights != null ? 1 : 0);
                        }
                    }

                    if (toggleParam != null && bakeResult.legacyLights != null) {
                        exclusiveTriggers.Add((oscId, toggleParam));
                    }

                    // Do the toggle last so all the objects have been generated and can be toggled on/off
                    var relocated = bakeResult.bakeRoot.parent != obj;
                    VFClip onClip = null;
                    if (toggleParam != null || relocated) {
                        bakeResult.bakeRoot.active = false;
                        onClip = clipFactory.NewClip($"{oscId}");
                        onClip.SetEnabled(bakeResult.bakeRoot, true);
                        var toggleTree = directTreeService.Create($"{oscId} - Toggle");
                        if (relocated) {
                            var condition = BlendtreeMath.GreaterThan(objectEnabled.Get(obj), 0);
                            if (toggleParam != null) condition = condition.And(BlendtreeMath.GreaterThan(toggleParam, 0));
                            var offClip = clipFactory.NewClip($"{oscId} - Off");
                            offClip.SetEnabled(bakeResult.bakeRoot, false);
                            toggleTree.Add(condition.create(onClip, offClip));
                        } else {
                            toggleTree.Add(toggleParam, onClip);
                        }
                    }
                    if (toggleParam != null) {
                        obj.active = true;
                        forceEnable.Add(obj);

                        var activeClip = actionClipService.LoadState($"SPS - Active Animation for {oscId}", socket.activeActions);
                        if (new AnimatorIterator.Clips().From(activeClip).SelectMany(clip => clip.GetAllBindings()).Any()) {
                            var activeAnimParam = fx.CreateUniqueFloat($"SPS - Active Animation for {oscId}");
                            var activeAnimLayer = fx.NewLayer($"SPS - Active Animation for {oscId}");
                            var off = activeAnimLayer.NewState("Off");
                            var on = activeAnimLayer.NewState("On").WithAnimation(activeClip);

                            off.TransitionsTo(on).When(activeAnimParam.IsGreaterThan(0));
                            on.TransitionsTo(off).When(activeAnimParam.IsLessThan(1));

                            objectTrees.Get(bakeResult.worldSpace).Add(activeAnimParam.MakeSetter(1));
                        }

                        var gizmo = obj.GetComponent<VRCFurySocketGizmo>();
                        if (gizmo != null) {
                            gizmo.show = false;
                            onClip.SetCurve(gizmo, "show", 1);
                        }

                        if (autoOnClip != null) {
                            autoSockets.Add(Tuple.Create(oscId, toggleParam, bakeResult.bakeRoot));
                        }
                    }
                } catch (Exception e) {
                    throw new ExceptionWithCause($"Failed to build SPS Socket: {socket.owner().GetDebugPath()}", e);
                }
            }

            if (exclusiveTriggers.Count >= 2) {
                var exclusiveLayer = actionController.NewLayer("SPS - Socket Exclusivity");
                exclusiveLayer.NewState("Start");
                legacyOn?.CopyTo(actionController);
                foreach (var i in Enumerable.Range(0, exclusiveTriggers.Count)) {
                    var (name, on) = exclusiveTriggers[i];
                    on?.CopyTo(actionController);
                    var state = exclusiveLayer.NewState(name);
                    var when = on.IsTrue();
                    when = when.And(actionController.IsLocal().IsTrue());
                    if (legacyOn != null) when = when.And(legacyOn.IsTrue());
                    state.TransitionsFromAny().When(when);
                    foreach (var j in Enumerable.Range(0, exclusiveTriggers.Count)) {
                        if (i == j) continue;
                        var (_, otherOn) = exclusiveTriggers[j];
                        state.Drives(otherOn, false);
                    }
                }
            }

            if (autoOn != null && autoSockets.Count > 0) {
#if !VRCSDK_HAS_CONTACT_PARAMETER_FLAGS
                throw new Exception("SPS Auto Mode requires VRCSDK 3.10.6 or newer.");
#else
                var autoActiveNum = fx.CreateUniqueInt("AutoSocketNum", def: -1);
                var autoActiveDist = fx.CreateUniqueFloat("AutoActiveDist", def: 10000);
                var autoProximity = fx.CreateUniqueFloat("AutoContact");
                var autoCurrentDist = fx.GetOrCreateFloat(autoProximity.name + "_PointSeparation", def: -1);
                var dbt = directTreeService.Create("SPS - Auto Mode");
                var closer = fx.CreateUniqueFloat("AutoCloser");
                // PointSeparation remains valid inside a plug's length sphere, where
                // proximity saturates. Its -1 no-contact sentinel must never win.
                dbt.Add(BlendtreeMath.GreaterThan(autoCurrentDist, 0, true)
                    .And(BlendtreeMath.GreaterThan(autoActiveDist, autoCurrentDist))
                    .create(closer.MakeSetter(1), closer.MakeSetter(0)));

                var autoReceiverObj = GameObjects.Create("SpsAutoDistance", avatarObject);
                ConstraintUtils.MakeWorldSpace(autoReceiverObj);

                var receiver = autoReceiverObj.AddComponent<VRCContactReceiver>();
                receiver.parameter = autoProximity;
                receiver.advancedParameters = ContactReceiver.AdvancedParameter.PointSeparation;
                receiver.radius = 1f;
                receiver.receiverType = ContactReceiver.ReceiverType.Proximity;
                receiver.collisionTags.Add(SpsTags.LegacyPlugSphere);
                receiver.allowOthers = true;
                receiver.allowSelf = false;
                receiver.localOnly = true;
                receiver.shapeType = ContactBase.ShapeType.Sphere;

                var constraint = VFConstraint.CreateParent(autoReceiverObj);

                var layer = fx.NewLayer("SPS - Auto Socket Comparison");
                var remoteTrap = layer.NewState("Remote trap");
                var idle = layer.NewState("Idle");
                remoteTrap.TransitionsTo(idle).When(fx.IsLocal().IsTrue());

                var active = layer.NewState("Active");
                idle.TransitionsTo(active).When(autoOn.IsTrue());

                var turnOffState = layer.NewState("Turn Off");
                active.TransitionsTo(turnOffState).When(autoOn.IsFalse());
                turnOffState.Drives(autoActiveNum, -1);
                turnOffState.Drives(autoActiveDist, 10000);
                foreach (var o in autoSockets) turnOffState.Drives(o.Item2, false);
                turnOffState.TransitionsTo(active).When(autoOn.IsTrue());

                var lastState = idle;
                Action<VFState> addNext = (next) => active.TransitionsTo(next).When(fx.Always());

                var settleTime = 0.05f;

                for (var i = 0; i < autoSockets.Count && i < 16; i++) {
                    var (name, enabled, obj) = autoSockets[i];
                    constraint.AddSource(obj);

                    // We need to settle for a frame for the constraint to move
                    var evalClip = clipFactory.NewClip($"Settle1 {name}");
                    evalClip.SetCurve(constraint.GetComponent(), constraint.GetWeightProperty(i), 1);
                    var settleState = layer.NewState($"Settle {name}").WithAnimation(evalClip).Move(lastState, 1, 0);
                    lastState = settleState;
                    if (addNext != null) addNext(settleState);

                    // We need to settle for a moment because contacts don't update every frame
                    var settleState2 = layer.NewState($"Settle2 {name}").WithAnimation(evalClip);
                    settleState.TransitionsTo(settleState2).When().WithTransitionExitTime(settleTime);

                    // We need to settle for another frame for the DBT comparison to apply
                    var settleState3 = layer.NewState($"Settle3 {name}").WithAnimation(evalClip);
                    settleState2.TransitionsTo(settleState3).When(fx.Always());

                    // Active socket went out of range
                    settleState3.TransitionsTo(turnOffState).When(autoActiveNum.IsEqualTo(i).And(autoCurrentDist.IsLessThan(0)));

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
                    settleState3.TransitionsTo(switchToState).When(closer.IsGreaterThan(0));

                    addNext = (next) => {
                        settleState3.TransitionsTo(next).When(fx.Always());
                        updateState.TransitionsTo(next).When(fx.Always());
                        switchToState.TransitionsTo(next).When(fx.Always());
                    };
                }

                if (addNext != null) addNext(active);
#endif
            }
        }
    }
}
