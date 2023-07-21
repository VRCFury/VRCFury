using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.UIElements;
using VF.Builder;
using VF.Feature.Base;
using VF.Inspector;
using VF.Model;
using VF.Model.StateAction;
using Toggle = VF.Model.Feature.Toggle;

namespace VF.Feature {

public class ToggleBuilder : FeatureBuilder<Toggle> {
    private VFAParam param;
    private AnimationClip restingClip;
    private string layerName;
    private VFACondition onCase;
    private bool onEqualsOut;
    private Dictionary<VRC.SDK3.Avatars.Components.VRCAvatarDescriptor.AnimLayerType, (VFAState, VFAState, VFAState)> inStates = new Dictionary<VRC.SDK3.Avatars.Components.VRCAvatarDescriptor.AnimLayerType, (VFAState, VFAState, VFAState)>();
    private Dictionary<VRC.SDK3.Avatars.Components.VRCAvatarDescriptor.AnimLayerType, (VFAState, VFAState, VFAState)> outStates = new Dictionary<VRC.SDK3.Avatars.Components.VRCAvatarDescriptor.AnimLayerType, (VFAState, VFAState, VFAState)>();

    private List<VFAState> exclusiveTagTriggeringStates = new List<VFAState>();
    private bool appliedToRest = false;
    
    private const string menuPathTooltip = "Menu Path is where you'd like the toggle to be located in the menu. This is unrelated"
        + " to the menu filenames -- simply enter the title you'd like to use. If you'd like the toggle to be in a submenu, use slashes. For example:\n\n"
        + "If you want the toggle to be called 'Shirt' in the root menu, you'd put:\nShirt\n\n"
        + "If you want the toggle to be called 'Pants' in a submenu called 'Clothing', you'd put:\nClothing/Pants";

    public ISet<string> GetExclusives(string objects) {

        return objects.Split(',')
            .Select(tag => tag.Trim())
            .Where(tag => !string.IsNullOrWhiteSpace(tag))
            .ToImmutableHashSet();
    }
	
    public ISet<string> GetExclusiveTags() {
        if(model.enableExclusiveTag)
            return GetExclusives(model.exclusiveTag);
        return new HashSet<string>(); 
    }

    public ISet<string> GetGlobalParams() {
        if(model.enableDriveGlobalParam)
            return GetExclusives(model.driveGlobalParam);
        return new HashSet<string>(); 
    }
    public VFAParam GetParam() {
        return param;
    }

    // this currently isn't used, but keeping it here for future proofing if hand layer starts getting included in toggles
    private bool IsHuanoid(State state) {
        var clips = state.actions.OfType<AnimationClipAction>();
        foreach (AnimationClipAction clip in clips) {
            AnimationClip c = clip.clip;
            if (c.humanMotion) return true;
        }
        return false;
    }

    private string getMaskName(AnimationClip clip) {
        
        var bones = AnimationUtility.GetCurveBindings(clip);
        var leftHand = false;
        var rightHand = false;
        var other = false;

        foreach (var b in bones) {

            if (!(HumanTrait.MuscleName.Contains(b.propertyName) || b.propertyName.EndsWith(" Stretched") || b.propertyName.EndsWith(".Spread"))) continue;
            if (b.propertyName.Contains("RightHand") || b.propertyName.Contains("Right Thumb") || b.propertyName.Contains("Right Index") ||
                b.propertyName.Contains("Right Middle") || b.propertyName.Contains("Right Ring") || b.propertyName.Contains("Right Little")) { rightHand = true; continue; }
            if (b.propertyName.Contains("LeftHand") || b.propertyName.Contains("Left Thumb") || b.propertyName.Contains("Left Index") || 
                b.propertyName.Contains("Left Middle") || b.propertyName.Contains("Left Ring") || b.propertyName.Contains("Left Little")) { leftHand = true; continue; }
            other = true;
        }
        if (other) return "emote"; 
        if (leftHand && rightHand) return "hands";
        if (leftHand) return "leftHand";
        if (rightHand) return "rightHand";
        
        return "";
    }

    private VFALayer getLayer(string layerName, ControllerManager controller, string maskName = "") {
        if (model.enableExclusiveTag) {
            var exclusiveTags = GetExclusiveTags();
            if (exclusiveTags.Count() == 0) return controller.NewLayer(layerName);
            var firstExclusiveTag = exclusiveTags.First();
            if (!exclusiveAnimationLayers.ContainsKey((firstExclusiveTag, controller.GetType(), maskName))) {
                 exclusiveAnimationLayers[(firstExclusiveTag, controller.GetType(), maskName)] = controller.NewLayer((firstExclusiveTag + " Animations " + maskName).Trim());
            }
            return exclusiveAnimationLayers[(firstExclusiveTag, controller.GetType(), maskName)];
        }
        return controller.NewLayer(layerName);
    }

    private VFALayer getLayerForParameters(string exclusiveTag) {
        if (!exclusiveParameterLayers.ContainsKey(exclusiveTag)) {
            exclusiveParameterLayers[exclusiveTag] = GetFx().NewLayer(exclusiveTag + " Parameters");
            exclusiveParameterLayers[exclusiveTag].NewState("Default");
        }
        return exclusiveParameterLayers[exclusiveTag];
    }

    private VFAState getStartState(string stateName, VFALayer layer) {
        if (layer.GetRawStateMachine().defaultState != null) {
            foreach (var s in layer.GetRawStateMachine().states) {
                if (s.state == layer.GetRawStateMachine().defaultState) return new VFAState(s, layer.GetRawStateMachine());
            }
        }
        return layer.NewState(stateName);
    }

    public string getPrimaryExclusive() {
        string targetTag = "";
        int targetMax = -1;
        int targetIndex = 0;

        foreach (var exclusiveTag in GetExclusiveTags()) {
            int tagCount = 1;
            int tagIndex = 0;
            foreach (var toggle in allBuildersInRun
                        .OfType<ToggleBuilder>()) {

                if (!model.exclusiveOffState && toggle == this) {
                    tagIndex = tagCount;
                }
                if (!toggle.model.exclusiveOffState && toggle.GetExclusiveTags().Contains(exclusiveTag)) {
                    tagCount++;
                }

            }

            if (tagCount > targetMax) {
                targetTag = exclusiveTag;
                targetMax = tagCount;
                targetIndex = tagIndex;
            }
        }

        return targetTag;
    }

    private void checkExclusives() {
        
        string targetTag = getPrimaryExclusive();
        int tagCount = 1;
        int tagIndex = 0;

       
        foreach (var toggle in allBuildersInRun
                    .OfType<ToggleBuilder>()) {

            if (!model.exclusiveOffState && toggle == this) {
                tagIndex = tagCount;
            }
            if (!toggle.model.exclusiveOffState && toggle.getPrimaryExclusive() == targetTag) {
                tagCount++;
            }
        }

        var temp = targetTag;

        foreach (var exclusiveTag in GetExclusiveTags()) {
            if (exclusiveTag != targetTag) {
                temp += (", " + exclusiveTag);
            }
        }

        if (tagCount > 256) {
            throw new Exception("Too many toggles for exclusive tag " + targetTag + ". Please reduce the number of toggles using this tag to below 255.");
        }

        if (tagCount > 8) {
            model.useInt = true;
            model.intTarget = tagIndex;
        }
    }
		
    private void CreateSlider() {
        var fx = GetFx();
        var layerName = model.name;
        var layer = fx.NewLayer(layerName);

        var off = layer.NewState("Off");
        var on = layer.NewState("On");
        var x = fx.NewFloat(
            model.name,
            synced: true,
            saved: model.saved,
            def: model.defaultOn ? model.defaultSliderValue : 0
        );
        manager.GetMenu().NewMenuSlider(
            model.name,
            x,
            icon: model.enableIcon ? model.icon : null
        );

        var clip = LoadState("On", model.state);
        if (ClipBuilder.IsStaticMotion(clip)) {
            var tree = fx.NewBlendTree("On Tree");
            tree.blendType = BlendTreeType.Simple1D;
            tree.useAutomaticThresholds = false;
            tree.blendParameter = x.Name();
            tree.AddChild(fx.GetNoopClip(), 0);
            tree.AddChild(clip, 1);
            on.WithAnimation(tree);
        } else {
            on.WithAnimation(clip).MotionTime(x);
        }

        var isOn = x.IsGreaterThan(0);
        off.TransitionsTo(on).When(isOn);
        on.TransitionsTo(off).When(isOn.Not());
    }


    [FeatureBuilderAction]
    public void Apply() {
        if (model.slider) {
            CreateSlider();
            return;
        }

        var physBoneResetter = CreatePhysBoneResetter(model.resetPhysbones, model.name);

        layerName = model.name;

        if (model.name == "" && model.useGlobalParam) {
            layerName = model.globalParam;
            model.addMenuItem = false;
        }

        var fx = GetFx();
        var layer = getLayer(layerName, fx);
        var off = getStartState("Off", layer);

        if (model.useGlobalParam && model.globalParam != null && model.paramOverride == null) {
            model.paramOverride = model.globalParam;
            model.usePrefixOnParam = false;
        }

        if (model.enableExclusiveTag) {
            checkExclusives();
        }

        string paramName;
        bool usePrefixOnParam;
        if (model.paramOverride != null) {
            paramName = model.paramOverride;
            usePrefixOnParam = model.usePrefixOnParam;
        } else if (model.useGlobalParam && model.globalParam != null) {
            paramName = model.globalParam;
            usePrefixOnParam = false;
        } else {
            paramName = model.name;
            usePrefixOnParam = model.usePrefixOnParam;
        }
        if (model.useInt) {
            if (model.intTarget == -1) {
                var numParam = fx.NewInt(paramName, synced: true, saved: model.saved, def: model.defaultOn ? 1 : 0, usePrefix: usePrefixOnParam);
                onCase = numParam.IsNotEqualTo(0);
            } else {
                var numParam = fx.NewInt("VF_" + GetExclusiveTags().First() + "_Exclusives", synced: true, def: model.defaultOn ? model.intTarget : 0, usePrefix: false);
                onCase = numParam.IsEqualTo(model.intTarget);
                param = numParam;
            }
        } else {
            var boolParam = fx.NewBool(paramName, synced: true, saved: model.saved, def: model.defaultOn, usePrefix: usePrefixOnParam);
            param = boolParam;
            onCase = boolParam.IsTrue();
        }

        if (!model.hasTransitionTime)  model.transitionTime = 0;
        if (!model.hasExitTime) model.exitTime = 0;
        
        if (model.separateLocal) {
            var isLocal = fx.IsLocal().IsTrue();
            Apply(fx, layer, off, onCase.And(isLocal.Not()), layerName + " On Remote", model.state, model.transitionStateIn, model.transitionStateOut, physBoneResetter);
            Apply(fx, layer, off, onCase.And(isLocal), layerName + " On Local", model.localState, model.localTransitionStateIn, model.localTransitionStateOut, physBoneResetter, true);
        } else {
            Apply(fx, layer, off, onCase, layerName + " On", model.state, model.transitionStateIn, model.transitionStateOut, physBoneResetter);
        }

        if (model.addMenuItem) {
            if (model.holdButton) {
                manager.GetMenu().NewMenuButton(
                    model.name,
                    param,
                    icon: model.enableIcon ? model.icon : null,
                    value: model.intTarget != -1 ? model.intTarget : 1
                );
            } else {
                manager.GetMenu().NewMenuToggle(
                    model.name,
                    param,
                    icon: model.enableIcon ? model.icon : null,
                    value: model.intTarget != -1 ? model.intTarget : 1
                );
            }
        }
    }
    
    private void Apply(
        ControllerManager controller,
        VFALayer layer,
        VFAState off,
        VFACondition onCase,
        string onName,
        State action,
        State inAction,
        State outAction,
        VFABool physBoneResetter,
        bool isLocal = false
    ) {
        var isHumanoidLayer = controller.GetType() != VRC.SDK3.Avatars.Components.VRCAvatarDescriptor.AnimLayerType.FX;

        var clip = LoadState(onName, action, isHumanoidLayer);

        if (controller.GetType() == VRC.SDK3.Avatars.Components.VRCAvatarDescriptor.AnimLayerType.FX && IsHuanoid(action)) {
            var actionLayer = GetAction();
            var layer2 = getLayer(layerName, actionLayer);
            var off2 = getStartState("Off", layer2);
            VFACondition onCase2;
            if (model.useInt) {
                var param2 = actionLayer.NewInt("VF_" + GetExclusiveTags().First() + "_Exclusives", synced: true, def: model.defaultOn ? model.intTarget : 0, usePrefix: false);
                onCase2 = param2.IsEqualTo(model.intTarget);
            } else {
                var param2 = actionLayer.NewBool(model.name, synced: !string.IsNullOrWhiteSpace(model.name), saved: model.saved, def: model.defaultOn, usePrefix: model.usePrefixOnParam);
                onCase2 = param2.IsTrue();
            }
            Apply(actionLayer, layer2, off2, onCase2, onName, action, inAction, outAction, physBoneResetter);
            if (clip == GetFx().GetNoopClip()) return; // if only a proxy animation don't worry about making toggle in FX layer
        } else if (controller.GetType() == VRC.SDK3.Avatars.Components.VRCAvatarDescriptor.AnimLayerType.Action) {
            var maskName = getMaskName(clip);
            if (maskName.ToLower().Contains("hand")) {
                var gestureLayer = GetGesture();
                var layer2 = getLayer(layerName, gestureLayer, maskName);
                var off2 = getStartState("Off", layer2);
                VFACondition onCase2;
            if (model.useInt) {
                var param2 = gestureLayer.NewInt("VF_" + GetExclusiveTags().First() + "_Exclusives", synced: true, def: model.defaultOn ? model.intTarget : 0, usePrefix: false);
                onCase2 = param2.IsEqualTo(model.intTarget);
            } else {
                var param2 = gestureLayer.NewBool(model.name, synced: !string.IsNullOrWhiteSpace(model.name), saved: model.saved, def: model.defaultOn, usePrefix: model.usePrefixOnParam);
                onCase2 = param2.IsTrue();
            }
                Apply(gestureLayer, layer2, off2, onCase2, onName, action, inAction, outAction, physBoneResetter);
                return;
            }
            
        }

        if (model.includeInRest && !appliedToRest) {
            appliedToRest = true;
            ApplyClipToRestingState(clip, true);
        }

        if (model.securityEnabled) {
            var securityLockUnlocked = allBuildersInRun
                .OfType<SecurityLockBuilder>()
                .Select(f => f.GetEnabled())
                .FirstOrDefault();
            if (securityLockUnlocked != null) {
                onCase = onCase.And(securityLockUnlocked);
            }
        }

        VFAState inState;
        VFAState onState;
        VFAState outState;

        AnimationClip transitionClipIn = null;

        if (model.hasTransition && inAction != null && !inAction.IsEmpty()) {
            transitionClipIn = LoadState(onName + " In", inAction, isHumanoidLayer);
            inState = layer.NewState(onName + " In").WithAnimation(transitionClipIn);
            onState = layer.NewState(onName).WithAnimation(clip);
            var transition = inState.TransitionsTo(onState).WithTransitionDurationSeconds(model.transitionTime);
            if (transitionClipIn.length <= 1f/transitionClipIn.frameRate) {
                transition.When(controller.Always());
            } else {
                transition.When().WithTransitionExitTime(1);
            }
        } else {
            inState = onState = layer.NewState(onName).WithAnimation(clip);
        }

        off.TransitionsTo(inState).When(onCase).WithTransitionDurationSeconds(model.transitionTime);

        if (model.simpleOutTransition) outAction = inAction;
        if (model.hasTransition && outAction != null && !outAction.IsEmpty()) {
            var transitionClipOut = LoadState(onName + " Out", outAction, isHumanoidLayer);
            outState = layer.NewState(onName + " Out").WithAnimation(transitionClipOut).Speed(model.simpleOutTransition ? -1 : 1);
            onState.TransitionsTo(outState).When(onCase.Not()).WithTransitionDurationSeconds(model.transitionTime).WithTransitionExitTime(model.exitTime);
        } else {
            outState = onState;
        }

        if (controller.GetType() == VRC.SDK3.Avatars.Components.VRCAvatarDescriptor.AnimLayerType.Action) {
            off.WithAnimation(inState.GetRaw().motion);
            off.TrackingController("emoteTracking").PlayableLayerController(VRC.SDKBase.VRC_PlayableLayerControl.BlendableLayer.Action, 0, 0);
            inState.TrackingController("emoteAnimation").PlayableLayerController(VRC.SDKBase.VRC_PlayableLayerControl.BlendableLayer.Action, 1, 0);

            if (inState != onState) {
                var blendOut = layer.NewState(onName + " Blendout").WithAnimation(inState.GetRaw().motion);
                var transition = outState.TransitionsTo(blendOut);
                if (outState == onState) {
                    transition.When(onCase.Not()).WithTransitionExitTime(model.exitTime).WithTransitionDurationSeconds(model.transitionTime);
                } else {
                    transition.When().WithTransitionExitTime(1);
                }
                outState = blendOut;
            }
        } else if (controller.GetType() == VRC.SDK3.Avatars.Components.VRCAvatarDescriptor.AnimLayerType.Gesture) {
            var maskName = getMaskName(clip);
            off.TrackingController(maskName + "Tracking");
            inState.TrackingController(maskName + "Animation");
            var maskGuid = "";
            switch (maskName) {
                case "hands":
                    maskGuid = "b2b8bad9583e56a46a3e21795e96ad92";
                    break;
                case "rightHand":
                    maskGuid = "903ce375d5f609d44b9f00b425d6eda9";
                    break;
                case "leftHand":
                    maskGuid = "7ff0199655202a04eb175de45a6e078a";
                    break;
            }

            if (maskGuid != "") {
                var maskPath = AssetDatabase.GUIDToAssetPath(maskGuid);
                var mask = AssetDatabase.LoadAssetAtPath<AvatarMask>(maskPath);
                controller.SetMask(controller.GetLayers().Count() - 1, mask);
            }
        }

         onEqualsOut = outState == onState;

        if (!model.enableExclusiveTag)
        {
            var exitTransition = outState.TransitionsToExit();

            if (onEqualsOut) {
                exitTransition.When(onCase.Not()).WithTransitionExitTime(model.exitTime).WithTransitionDurationSeconds(model.transitionTime);
            } else {
                exitTransition.When().WithTransitionExitTime(1);
            }
        }

        if (!model.separateLocal)
        {
            inStates[controller.GetType()] = (inState, null, null);
            outStates[controller.GetType()] = (outState, null, null);
        } else {
            if (!isLocal) {
                inStates[controller.GetType()] = (null, inState, null);
                outStates[controller.GetType()] = (null, outState, null);
            } else {
                inStates[controller.GetType()] = (null, inStates[controller.GetType()].Item2, inState);
                outStates[controller.GetType()] = (null, inStates[controller.GetType()].Item2, outState);
            }
        }
        


        if (physBoneResetter != null) {
            off.Drives(physBoneResetter, true);
            inState.Drives(physBoneResetter, true);
        }

        if (model.enableDriveGlobalParam && !string.IsNullOrWhiteSpace(model.driveGlobalParam)) {
            foreach(var p in GetGlobalParams()) {
                if (string.IsNullOrWhiteSpace(p)) continue;
                var driveGlobal = controller.NewBool(
                    p,
                    synced: false,
                    saved: false,
                    def: false,
                    usePrefix: false
                );
                off.Drives(driveGlobal, false);
                inState.Drives(driveGlobal, true);
            }
        }
    }


    [FeatureBuilderAction(FeatureOrder.CollectToggleExclusiveTags)]
    public void ApplyExclusiveTags() {
        
        if (!model.enableExclusiveTag) return;

        ControllerManager[] controllers = { GetFx(), GetAction(), GetGesture() };
        var paramsToTurnOff = new HashSet<VFABool>();
        var paramsToTurnToZero = new Dictionary<String, HashSet<(VFAInteger, int)>>();
        var allOthersOff = controllers[0].Always();
        var isLocal = controllers[0].IsLocal().IsTrue();

        foreach (var controller in controllers) {
            (VFAState outState, VFAState outStateR, VFAState outStateL) = outStates.ContainsKey(controller.GetType()) ? outStates[controller.GetType()] : (null, null, null);
            
            foreach (var exclusiveTag in GetExclusiveTags()) {
                foreach (var other in allBuildersInRun
                            .OfType<ToggleBuilder>()
                            .Where(b => b != this)) {
                    if (other.GetExclusiveTags().Contains(exclusiveTag)) {
                        var otherParam = other.GetParam();
                        if (otherParam != null) {
                            var otherOnCondition = other.model.useInt ? (otherParam as VFAInteger).IsEqualTo(other.model.intTarget) : (otherParam as VFABool).IsTrue();
                            if (other.model.useInt) {
                                if (param.Name() != otherParam.Name()) {
                                    if (!paramsToTurnToZero.ContainsKey(otherParam.Name())) {
                                        paramsToTurnToZero[otherParam.Name()] = new HashSet<(VFAInteger, int)>();
                                    }
                                    paramsToTurnToZero[otherParam.Name()].Add((otherParam as VFAInteger, other.model.intTarget));
                                }
                            } else {
                                paramsToTurnOff.Add(otherParam as VFABool);
                                if (controller == controllers[0]) {
                                    allOthersOff = allOthersOff.And(otherOnCondition.Not());
                                }
                            }
                            (VFAState inState, VFAState inStateR, VFAState inStateL) = other.inStates.ContainsKey(controller.GetType()) ? other.inStates[controller.GetType()] : (null, null, null);

                            if (inState != null) {
                                if (outState != null && inState.GetRawStateMachine() == outState.GetRawStateMachine()) {
                                    outState.TransitionsTo(inState).When(otherOnCondition).WithTransitionExitTime(1).WithTransitionDurationSeconds(model.transitionTime);
                                }
                                if (outStateR != null && inState.GetRawStateMachine() == outStateR.GetRawStateMachine()) {
                                    outStateR.TransitionsTo(inState).When(otherOnCondition).WithTransitionExitTime(1).WithTransitionDurationSeconds(model.transitionTime);
                                }
                                if (outStateL != null && inState.GetRawStateMachine() == outStateL.GetRawStateMachine()) {
                                    outStateL.TransitionsTo(inState).When(otherOnCondition).WithTransitionExitTime(1).WithTransitionDurationSeconds(model.transitionTime);
                                }
                            }

                            if (inStateR != null) {
                                if (outState != null && inStateR.GetRawStateMachine() == outState.GetRawStateMachine()) {
                                    outState.TransitionsTo(inStateR).When(otherOnCondition.And(isLocal.Not())).WithTransitionExitTime(1).WithTransitionDurationSeconds(model.transitionTime);
                                }
                                if (outStateR != null && inStateR.GetRawStateMachine() == outStateR.GetRawStateMachine()) {
                                    outStateR.TransitionsTo(inStateR).When(otherOnCondition.And(isLocal).Not()).WithTransitionExitTime(1).WithTransitionDurationSeconds(model.transitionTime);
                                }
                            }

                            if (inStateL != null) {
                                if (outState != null && inStateL.GetRawStateMachine() == outState.GetRawStateMachine()) {
                                    outState.TransitionsTo(inStateL).When(otherOnCondition.And(isLocal)).WithTransitionExitTime(1).WithTransitionDurationSeconds(model.transitionTime);
                                }
                                if (outStateL != null && inStateL.GetRawStateMachine() == outStateL.GetRawStateMachine()) {
                                    outStateL.TransitionsTo(inStateL).When(otherOnCondition.And(isLocal)).WithTransitionExitTime(1).WithTransitionDurationSeconds(model.transitionTime);
                                }
                            }
                        }
                    }
                }
            }

            if (outState != null) {
                var exitTransition = outState.TransitionsToExit();
                if (onEqualsOut) {
                    exitTransition.When(onCase.Not()).WithTransitionExitTime(model.exitTime).WithTransitionDurationSeconds(model.transitionTime);
                } else {
                    exitTransition.When().WithTransitionExitTime(1);
                }
            }

            if (outStateR != null) {
                var exitTransition = outStateR.TransitionsToExit();
                if (onEqualsOut) {
                    exitTransition.When(onCase.Not()).WithTransitionExitTime(model.exitTime).WithTransitionDurationSeconds(model.transitionTime);
                } else {
                    exitTransition.When().WithTransitionExitTime(1);
                }
            }

            if (outStateL != null) {
                var exitTransition = outStateL.TransitionsToExit();
                if (onEqualsOut) {
                    exitTransition.When(onCase.Not()).WithTransitionExitTime(model.exitTime).WithTransitionDurationSeconds(model.transitionTime);
                } else {
                    exitTransition.When().WithTransitionExitTime(1);
                }
            }
        }

        if (paramsToTurnOff.Count + paramsToTurnToZero.Count > 0) {

            var exclusiveLayer = getLayerForParameters(GetExclusiveTags().First());
            var startState = getStartState("Default", exclusiveLayer);
            var triggerState = exclusiveLayer.NewState(layerName);
            var onParam = model.useInt ? (param as VFAInteger).IsEqualTo(model.intTarget) : (param as VFABool).IsTrue();

            var intStates = new HashSet<(VFACondition, VFAState)>();

            triggerState.TransitionsToExit().When(onParam.Not());

            var allOrParam = controllers[0].Never();

            foreach (var tag in paramsToTurnToZero.Keys) {
                var orParam = controllers[0].Never();
                VFAInteger tagParam = paramsToTurnToZero[tag].First().Item1;
                foreach (var (p, i) in paramsToTurnToZero[tag]) {
                    orParam = orParam.Or(p.IsEqualTo(i));
                }
                var intTriggerState = exclusiveLayer.NewState(layerName + " + " + tagParam.Name());
                startState.TransitionsTo(intTriggerState).When(onParam.And(orParam));
                intTriggerState.Drives(tagParam, 0);

                intStates.Add((onParam.And(orParam), intTriggerState));
                allOrParam = allOrParam.Or(orParam);
            }

            foreach (var (condition, s1) in intStates) {
                foreach (var (dud, s2) in intStates) {
                    if (s1 != s2) {
                        s1.TransitionsTo(s2).When(condition);
                    }
                }
                s1.TransitionsTo(triggerState).When(allOrParam.Not());
            }

            triggerState.TransitionsFromAny().When(onParam.And(allOrParam.Not()));
            
            foreach (var p in paramsToTurnOff) {
                triggerState.Drives(p, false);
            }

            if (!model.useInt && model.exclusiveOffState) {
                startState.TransitionsTo(triggerState).When(allOthersOff);
                triggerState.Drives((param as VFABool), true);
            }
        }
    }

    public override string GetClipPrefix() {
        return "Toggle " + model.name.Replace('/', '_');
    }

    public override string GetEditorTitle() {
        return "Toggle";
    }

    public override VisualElement CreateEditor(SerializedProperty prop) {
        return CreateEditor(prop, content => content.Add(VRCFuryStateEditor.render(prop.FindPropertyRelative("state"))));
    }

    private static VisualElement CreateEditor(SerializedProperty prop, Action<VisualElement> renderBody) {
        var content = new VisualElement();

        var savedProp = prop.FindPropertyRelative("saved");
        var sliderProp = prop.FindPropertyRelative("slider");
        var securityEnabledProp = prop.FindPropertyRelative("securityEnabled");
        var defaultOnProp = prop.FindPropertyRelative("defaultOn");
        var includeInRestProp = prop.FindPropertyRelative("includeInRest");
        var exclusiveOffStateProp = prop.FindPropertyRelative("exclusiveOffState");
        var enableExclusiveTagProp = prop.FindPropertyRelative("enableExclusiveTag");
        var resetPhysboneProp = prop.FindPropertyRelative("resetPhysbones");
        var enableIconProp = prop.FindPropertyRelative("enableIcon");
        var enableDriveGlobalParamProp = prop.FindPropertyRelative("enableDriveGlobalParam");
        var separateLocalProp = prop.FindPropertyRelative("separateLocal");
        var hasTransitionProp = prop.FindPropertyRelative("hasTransition");
        var simpleOutTransitionProp = prop.FindPropertyRelative("simpleOutTransition");
        var defaultSliderProp = prop.FindPropertyRelative("defaultSliderValue");
        var hasTransitionTimeProp = prop.FindPropertyRelative("hasTransitionTime");
        var hasExitTimeProp = prop.FindPropertyRelative("hasExitTime");
        var useGlobalParamProp = prop.FindPropertyRelative("useGlobalParam");
        var globalParamProp = prop.FindPropertyRelative("globalParam");
        var holdButtonProp = prop.FindPropertyRelative("holdButton");

        var flex = new VisualElement {
            style = {
                flexDirection = FlexDirection.Row,
                alignItems = Align.FlexStart
            }
        };
        content.Add(flex);

        var name = VRCFuryEditorUtils.Prop(prop.FindPropertyRelative("name"), "Menu Path", tooltip: menuPathTooltip);
        name.style.flexGrow = 1;
        flex.Add(name);

        var button = VRCFuryEditorUtils.Button("Options", () => {
            var advMenu = new GenericMenu();
            if (savedProp != null) {
                advMenu.AddItem(new GUIContent("Saved Between Worlds"), savedProp.boolValue, () => {
                    savedProp.boolValue = !savedProp.boolValue;
                    prop.serializedObject.ApplyModifiedProperties();
                });
            }

            if (sliderProp != null) {
                advMenu.AddItem(new GUIContent("Use Slider Wheel"), sliderProp.boolValue, () => {
                    sliderProp.boolValue = !sliderProp.boolValue;
                    prop.serializedObject.ApplyModifiedProperties();
                });
            }

            if (securityEnabledProp != null) {
                advMenu.AddItem(new GUIContent("Protect with Security"), securityEnabledProp.boolValue, () => {
                    securityEnabledProp.boolValue = !securityEnabledProp.boolValue;
                    prop.serializedObject.ApplyModifiedProperties();
                });
            }

            if (defaultOnProp != null) {
                advMenu.AddItem(new GUIContent("Default On"), defaultOnProp.boolValue, () => {
                    defaultOnProp.boolValue = !defaultOnProp.boolValue;
                    prop.serializedObject.ApplyModifiedProperties();
                });
            }

            if (includeInRestProp != null) {
                advMenu.AddItem(new GUIContent("Show in Rest Pose"), includeInRestProp.boolValue, () => {
                    includeInRestProp.boolValue = !includeInRestProp.boolValue;
                    prop.serializedObject.ApplyModifiedProperties();
                });
            }

            if (resetPhysboneProp != null) {
                advMenu.AddItem(new GUIContent("Add PhysBone to Reset"), false, () => {
                    VRCFuryEditorUtils.AddToList(resetPhysboneProp);
                });
            }

            if (enableExclusiveTagProp != null) {
                advMenu.AddItem(new GUIContent("Enable Exclusive Tags"), enableExclusiveTagProp.boolValue, () => {
                    enableExclusiveTagProp.boolValue = !enableExclusiveTagProp.boolValue;
                    prop.serializedObject.ApplyModifiedProperties();
                });
            }
            
            if (exclusiveOffStateProp != null) {
                advMenu.AddItem(new GUIContent("This is Exclusive Off State"), exclusiveOffStateProp.boolValue, () => {
                    exclusiveOffStateProp.boolValue = !exclusiveOffStateProp.boolValue;
                    prop.serializedObject.ApplyModifiedProperties();
                });
            }

            if (enableIconProp != null) {
                advMenu.AddItem(new GUIContent("Set Custom Menu Icon"), enableIconProp.boolValue, () => {
                    enableIconProp.boolValue = !enableIconProp.boolValue;
                    prop.serializedObject.ApplyModifiedProperties();
                });
            }
            
            if (enableDriveGlobalParamProp != null) {
                advMenu.AddItem(new GUIContent("Drive a Global Parameter"), enableDriveGlobalParamProp.boolValue, () => {
                    enableDriveGlobalParamProp.boolValue = !enableDriveGlobalParamProp.boolValue;
                    prop.serializedObject.ApplyModifiedProperties();
                });
            }

            if (separateLocalProp != null)
            {
                advMenu.AddItem(new GUIContent("Separate Local State"), separateLocalProp.boolValue, () => {
                    separateLocalProp.boolValue = !separateLocalProp.boolValue;
                    prop.serializedObject.ApplyModifiedProperties();
                });
            }

            if (hasTransitionProp != null)
            {
                advMenu.AddItem(new GUIContent("Enable Transition State"), hasTransitionProp.boolValue, () => {
                    hasTransitionProp.boolValue = !hasTransitionProp.boolValue;
                    prop.serializedObject.ApplyModifiedProperties();
                });
            }

            advMenu.AddItem(new GUIContent("Has Transition Time"), hasTransitionTimeProp.boolValue, () => {
                    hasTransitionTimeProp.boolValue = !hasTransitionTimeProp.boolValue;
                    prop.serializedObject.ApplyModifiedProperties();
                });
            advMenu.AddItem(new GUIContent("Has Exit Time"), hasExitTimeProp.boolValue, () => {
                    hasExitTimeProp.boolValue = !hasExitTimeProp.boolValue;
                    prop.serializedObject.ApplyModifiedProperties();
                });

            if (useGlobalParamProp != null) {
                advMenu.AddItem(new GUIContent("Use a Global Parameter"), useGlobalParamProp.boolValue, () => {
                    useGlobalParamProp.boolValue = !useGlobalParamProp.boolValue;
                    prop.serializedObject.ApplyModifiedProperties();
                });
            }

            if (holdButtonProp != null) {
                advMenu.AddItem(new GUIContent("Hold Button"), holdButtonProp.boolValue, () => {
                    holdButtonProp.boolValue = !holdButtonProp.boolValue;
                    prop.serializedObject.ApplyModifiedProperties();
                });
            }

            advMenu.ShowAsContext();
        });
        button.style.flexGrow = 0;
        flex.Add(button);
        
        renderBody(content);

        if (resetPhysboneProp != null) {
            content.Add(VRCFuryEditorUtils.RefreshOnChange(() => {
                var c = new VisualElement();
                if (resetPhysboneProp.arraySize > 0) {
                    c.Add(VRCFuryEditorUtils.WrappedLabel("Reset PhysBones:"));
                    c.Add(VRCFuryEditorUtils.List(prop.FindPropertyRelative("resetPhysbones")));
                }
                return c;
            }, resetPhysboneProp));
        }

        if (enableExclusiveTagProp != null) {
            content.Add(VRCFuryEditorUtils.RefreshOnChange(() => {
                var c = new VisualElement();
                if (enableExclusiveTagProp.boolValue) {
                    c.Add(VRCFuryEditorUtils.Prop(prop.FindPropertyRelative("exclusiveTag"), "Exclusive Tags"));
                }
                return c;
            }, enableExclusiveTagProp));
        }

        if (useGlobalParamProp != null) {
            content.Add(VRCFuryEditorUtils.RefreshOnChange(() => {
                var c = new VisualElement();
                if (useGlobalParamProp.boolValue) {
                    c.Add(VRCFuryEditorUtils.Prop(prop.FindPropertyRelative("globalParam"), "Global Parameter"));
                }

                return c;
            }, useGlobalParamProp));
        }

        content.Add(VRCFuryEditorUtils.RefreshOnChange(() => {
            var c = new VisualElement();
            if (sliderProp.boolValue && defaultOnProp.boolValue) {
                c.Add(VRCFuryEditorUtils.Prop(prop.FindPropertyRelative("defaultSliderValue"), "Default Value"));
            }
            return c;
        }, sliderProp, defaultOnProp));
        
        if (enableIconProp != null) {
            content.Add(VRCFuryEditorUtils.RefreshOnChange(() => {
                var c = new VisualElement();
                if (enableIconProp.boolValue) {
                    c.Add(VRCFuryEditorUtils.Prop(prop.FindPropertyRelative("icon"), "Menu Icon"));
                }
                return c;
            }, enableIconProp));
        }

        if (enableDriveGlobalParamProp != null) {
            content.Add(VRCFuryEditorUtils.RefreshOnChange(() => {
                var c = new VisualElement();
                if (enableDriveGlobalParamProp.boolValue) {
                    c.Add(VRCFuryEditorUtils.Prop(prop.FindPropertyRelative("driveGlobalParam"), "Drive Global Param"));
                    c.Add(VRCFuryEditorUtils.Warn(
                        "Warning, Drive Global Param is an advanced feature. The driven parameter should not be placed in a menu " +
                        "or controlled by any other driver or shared with any other toggle. It should only be used as an input to " +
                        "manually-created state transitions in your avatar. This should NEVER be used on vrcfury props, as any merged " +
                        "full controllers will have their parameters rewritten."));
                }
                return c;
            }, enableDriveGlobalParamProp));
        }

        if (separateLocalProp != null)
        {
            content.Add(VRCFuryEditorUtils.RefreshOnChange(() => {
                var c = new VisualElement();
                if (separateLocalProp.boolValue)
                {
                    c.Add(VRCFuryEditorUtils.Prop(prop.FindPropertyRelative("localState"), "Local State"));
                }
                return c;
            }, separateLocalProp));
        }

        content.Add(VRCFuryEditorUtils.RefreshOnChange(() => {
            var c = new VisualElement();
            if (hasTransitionProp.boolValue)
            {
                c.Add(VRCFuryEditorUtils.Prop(prop.FindPropertyRelative("transitionStateIn"), "Transition In"));

                if (!simpleOutTransitionProp.boolValue)
                    c.Add(VRCFuryEditorUtils.Prop(prop.FindPropertyRelative("transitionStateOut"), "Transition Out"));
            }
            return c;
        }, hasTransitionProp, simpleOutTransitionProp));

        content.Add(VRCFuryEditorUtils.RefreshOnChange(() => {
            var c = new VisualElement();
            if (separateLocalProp.boolValue && hasTransitionProp.boolValue)
            {
                c.Add(VRCFuryEditorUtils.Prop(prop.FindPropertyRelative("localTransitionStateIn"), "Local Trans. In"));

                if (!simpleOutTransitionProp.boolValue)
                    c.Add(VRCFuryEditorUtils.Prop(prop.FindPropertyRelative("localTransitionStateOut"), "Local Trans. Out"));
                    
            }
            return c;
        }, separateLocalProp, hasTransitionProp, simpleOutTransitionProp));

        content.Add(VRCFuryEditorUtils.RefreshOnChange(() => {
            var c = new VisualElement();
            if (hasTransitionProp.boolValue)
            {
                c.Add(VRCFuryEditorUtils.Prop(prop.FindPropertyRelative("simpleOutTransition"), "Transition Out is reverse of Transition In"));
            }
            return c;
        }, hasTransitionProp));

        content.Add(VRCFuryEditorUtils.RefreshOnChange(() => {
            var c = new VisualElement();
            if (hasExitTimeProp.boolValue)
            {
                c.Add(VRCFuryEditorUtils.Prop(prop.FindPropertyRelative("exitTime"), "Exit Time"));
            }
            return c;
        }, hasExitTimeProp));

        content.Add(VRCFuryEditorUtils.RefreshOnChange(() => {
            var c = new VisualElement();
            if (hasTransitionTimeProp.boolValue)
            {
                c.Add(VRCFuryEditorUtils.Prop(prop.FindPropertyRelative("transitionTime"), "Transition Time"));
            }
            return c;
        }, hasTransitionTimeProp));

        // Tags
        content.Add(VRCFuryEditorUtils.RefreshOnChange(() => {
                var tags = new List<string>();
                if (savedProp != null && savedProp.boolValue)
                    tags.Add("Saved");
                if (sliderProp != null && sliderProp.boolValue)
                    tags.Add("Slider");
                if (securityEnabledProp != null && securityEnabledProp.boolValue)
                    tags.Add("Security");
                if (defaultOnProp != null && defaultOnProp.boolValue)
                    tags.Add("Default On");
                if (includeInRestProp != null && includeInRestProp.boolValue)
                    tags.Add("Shown in Rest Pose");
                if (exclusiveOffStateProp != null && exclusiveOffStateProp.boolValue)
                    tags.Add("This is the Exclusive Off State");
                if (holdButtonProp != null && holdButtonProp.boolValue)
                    tags.Add("Hold Button");

                var row = new VisualElement();
                row.style.flexWrap = Wrap.Wrap;
                row.style.flexDirection = FlexDirection.Row;
                foreach (var tag in tags) {
                    var flag = new Label(tag);
                    flag.style.width = StyleKeyword.Auto;
                    flag.style.backgroundColor = new Color(1f, 1f, 1f, 0.1f);
                    flag.style.borderTopRightRadius = 5;
                    flag.style.marginRight = 5;
                    VRCFuryEditorUtils.Padding(flag, 2, 4);
                    row.Add(flag);
                }

                return row;
            },
            savedProp,
            sliderProp,
            securityEnabledProp,
            defaultOnProp,
            includeInRestProp,
            exclusiveOffStateProp,
            holdButtonProp
        ));

        return content;
    }
}

}


