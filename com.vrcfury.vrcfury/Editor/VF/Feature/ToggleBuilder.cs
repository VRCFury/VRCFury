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
using VF.Utils;
using Toggle = VF.Model.Feature.Toggle;

namespace VF.Feature {

public class ToggleBuilder : FeatureBuilder<Toggle> {
    private VFAParam param;
    private string layerName;
    private VFACondition onCase;
    private bool onEqualsOut;

    private bool addMenuItem;
    private bool useInt;
    private int intTarget = -1;
    private bool enableExclusiveTag;

    private string humanoidMask = "";

    private AnimationClip restingClip;

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
        if (humanoidMask == "") CheckHumanoidMask();
        var tags = model.enableExclusiveTag ? model.exclusiveTag : "";
        if (humanoidMask == "emote") {
            tags += ",VF_EMOTE";
        }
        if (humanoidMask == "leftHand" || humanoidMask == "hands") {
            tags += ",VF_LEFT_HAND";
        }
        if (humanoidMask == "rightHand" || humanoidMask == "hands") {
            tags += ",VF_RIGHT_HAND";
        }
        return GetExclusives(tags);
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
        if (state == null) return false;
        var clips = state.actions.OfType<AnimationClipAction>();
        foreach (AnimationClipAction clip in clips) {
            AnimationClip c = clip.clip;
            if (c.humanMotion) return true;
        }
        return false;
    }

    private string GetHumanoidMaskName(params State[] states) {
        
        var leftHand = false;
        var rightHand = false;

        foreach(var state in states) {
            if (state == null) continue;
            foreach(var action in state.actions) {
                if (action is AnimationClipAction clip) {
                    var bones = AnimationUtility.GetCurveBindings(clip.clip);
                    foreach (var b in bones) {
                        if (!(HumanTrait.MuscleName.Contains(b.propertyName) || b.propertyName.EndsWith(" Stretched") || b.propertyName.EndsWith(".Spread"))) continue;
                        if (b.propertyName.Contains("RightHand") || b.propertyName.Contains("Right Thumb") || b.propertyName.Contains("Right Index") ||
                            b.propertyName.Contains("Right Middle") || b.propertyName.Contains("Right Ring") || b.propertyName.Contains("Right Little")) { rightHand = true; continue; }
                        if (b.propertyName.Contains("LeftHand") || b.propertyName.Contains("Left Thumb") || b.propertyName.Contains("Left Index") || 
                            b.propertyName.Contains("Left Middle") || b.propertyName.Contains("Left Ring") || b.propertyName.Contains("Left Little")) { leftHand = true; continue; }
                        return "emote";
                    }
                }
            }
        }

        if (leftHand && rightHand) return "hands";
        if (leftHand) return "leftHand";
        if (rightHand) return "rightHand";
        
        return "none";
    }

    private VFALayer GetLayer(string layerName, ControllerManager controller) {
        if (enableExclusiveTag) {
            var primaryExclusiveTag = GetPrimaryExclusive();
            if (primaryExclusiveTag == "") return controller.NewLayer(layerName);
            if (!exclusiveAnimationLayers.ContainsKey(primaryExclusiveTag)) {
                 exclusiveAnimationLayers[primaryExclusiveTag] = controller.NewLayer((primaryExclusiveTag + " Animations").Trim());
            }
            return exclusiveAnimationLayers[primaryExclusiveTag];
        }
        return controller.NewLayer(layerName);
    }

    private VFALayer GetLayerForParameters(string exclusiveTag) {
        if (!exclusiveParameterLayers.ContainsKey(exclusiveTag)) {
            exclusiveParameterLayers[exclusiveTag] = GetFx().NewLayer(exclusiveTag + " Parameters");
            exclusiveParameterLayers[exclusiveTag].NewState("Default");
        }
        return exclusiveParameterLayers[exclusiveTag];
    }

    private VFAState GetOffState(string stateName, VFALayer layer) {
        if (layer.GetRawStateMachine().states.Count() > 0) {
            return new VFAState(layer.GetRawStateMachine().states.First(), layer.GetRawStateMachine());
        }
        return layer.NewState(stateName);
    }

    private void SetStartState(VFALayer layer, AnimatorState state) {
        layer.GetRawStateMachine().defaultState = state;
    }

    public string GetPrimaryExclusive() {
        string targetTag = "";
        int targetMax = -1;

        foreach (var exclusiveTag in GetExclusiveTags()) {
            int tagCount = 1;
            foreach (var toggle in allBuildersInRun
                        .OfType<ToggleBuilder>()) {

                if (!toggle.model.exclusiveOffState && toggle.GetExclusiveTags().Contains(exclusiveTag)) {
                    tagCount++;
                }

            }

            if (tagCount > targetMax) {
                targetTag = exclusiveTag;
                targetMax = tagCount;
            }
        }

        return targetTag;
    }

    private void CheckHumanoidMask() {
        humanoidMask = GetHumanoidMaskName(model.state, model.localState, model.transitionStateIn, model.localTransitionStateIn, model.transitionStateOut, model.localTransitionStateOut);
        if (humanoidMask != "none") enableExclusiveTag = true;
    }

    private void CheckExclusives() {
        string targetTag = GetPrimaryExclusive();
        int tagCount = 1;
        int tagIndex = 0;

       
        foreach (var toggle in allBuildersInRun
                    .OfType<ToggleBuilder>()) {

            if (!model.exclusiveOffState && toggle == this) {
                tagIndex = tagCount;
            }
            if (!toggle.model.exclusiveOffState && toggle.GetPrimaryExclusive() == targetTag) {
                tagCount++;
            }
        }

        if (tagCount > 256) {
            throw new Exception("Too many toggles for exclusive tag " + targetTag + ". Please reduce the number of toggles using this tag to below 255.");
        }

        if (tagCount > 8) {
            useInt = true;
            intTarget = tagIndex;
        }
    }
		
    private (string,bool) GetParamName() {
        if (model.paramOverride != null) {
            return (model.paramOverride, false);
        }
        if (model.useGlobalParam && !string.IsNullOrWhiteSpace(model.globalParam)) {
            return (model.globalParam, false);
        }
        return (model.name, model.usePrefixOnParam);
    }

    private void CreateSlider() {
        var fx = GetFx();
        var layerName = model.name;
        var layer = fx.NewLayer(layerName);

        var (paramName, usePrefixOnParam) = GetParamName();

        var off = layer.NewState("Off");
        var on = layer.NewState("On");
        var x = fx.NewFloat(
            paramName,
            synced: addMenuItem,
            saved: model.saved,
            def: model.defaultOn ? model.defaultSliderValue : 0,
            usePrefix: usePrefixOnParam
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
            tree.AddChild(fx.GetEmptyClip(), 0);
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
        
        useInt = model.useInt;
        addMenuItem = model.addMenuItem;
        enableExclusiveTag = model.enableExclusiveTag || enableExclusiveTag;

        if (model.slider) {
            CreateSlider();
            return;
        }

        var physBoneResetter = CreatePhysBoneResetter(model.resetPhysbones, model.name);

        layerName = model.name;

        if (model.name == "" && model.useGlobalParam) {
            layerName = model.globalParam;
            addMenuItem = false;
        }

        if (humanoidMask == "") CheckHumanoidMask();
        if (enableExclusiveTag) CheckExclusives();

        var fx = GetFx();
        var layer = GetLayer(layerName, fx);
        var off = GetOffState("Off", layer);

        var (paramName, usePrefixOnParam) = GetParamName();
        if (useInt) {
            if (intTarget == -1) {
                var numParam = fx.NewInt(paramName, synced: addMenuItem, saved: model.saved, def: model.defaultOn ? 1 : 0, usePrefix: usePrefixOnParam);
                onCase = numParam.IsNotEqualTo(0);
            } else {
                var numParam = fx.NewInt("VF_" + GetPrimaryExclusive() + "_Exclusives", synced: addMenuItem, def: model.defaultOn ? intTarget : 0, usePrefix: false);
                onCase = numParam.IsEqualTo(intTarget);
                param = numParam;
            }
        } else {
            var boolParam = fx.NewBool(paramName, synced: addMenuItem, saved: model.saved, def: model.defaultOn, usePrefix: usePrefixOnParam);
            param = boolParam;
            onCase = boolParam.IsTrue();
        }
        
        if (model.separateLocal) {
            var isLocal = fx.IsLocal().IsTrue();
            Apply(fx, layer, off, onCase.And(isLocal.Not()), layerName + " On Remote", model.state, model.transitionStateIn, model.transitionStateOut, physBoneResetter);
            Apply(fx, layer, off, onCase.And(isLocal), layerName + " On Local", model.localState, model.localTransitionStateIn, model.localTransitionStateOut, physBoneResetter);
        } else {
            Apply(fx, layer, off, onCase, layerName + " On", model.state, model.transitionStateIn, model.transitionStateOut, physBoneResetter);
        }

        if (addMenuItem) {
            if (model.holdButton) {
                manager.GetMenu().NewMenuButton(
                    model.name,
                    param,
                    icon: model.enableIcon ? model.icon : null,
                    value: intTarget != -1 ? intTarget : 1
                );
            } else {
                manager.GetMenu().NewMenuToggle(
                    model.name,
                    param,
                    icon: model.enableIcon ? model.icon : null,
                    value: intTarget != -1 ? intTarget : 1
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
        VFABool physBoneResetter
    ) {
        var transitionTime = model.transitionTime;
        if (!model.hasTransitionTime)  transitionTime = 0;

        var clip = LoadState(onName, action);

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

        if (model.hasTransition && inAction != null && inAction.actions.Count() != 0) {
            
            var transitionClipIn = LoadState(onName + " In", inAction);

            // if clip is empty, copy last frame of transition
            if (clip == controller.GetEmptyClip()) {
                clip = controller.NewClip(onName);
                clip.CopyFromLast(transitionClipIn);
            }
            
            inState = layer.NewState(onName + " In").WithAnimation(transitionClipIn);
            onState = layer.NewState(onName).WithAnimation(clip);
            var transition = inState.TransitionsTo(onState).WithTransitionDurationSeconds(transitionTime);
            if (transitionClipIn.length <= 1f/transitionClipIn.frameRate) {
                transition.When(controller.Always());
            } else {
                transition.When().WithTransitionExitTime(1);
            }


        } else {
            inState = onState = layer.NewState(onName).WithAnimation(clip);
        }

        off.TransitionsTo(inState).When(onCase).WithTransitionDurationSeconds(transitionTime);
        inState.TransitionsFromEntry().When(onCase);

        if (model.simpleOutTransition) outAction = inAction;

       
        if (model.hasTransition && outAction != null && outAction.actions.Count() != 0) {
            var transitionClipOut = LoadState(onName + " Out", outAction);
            outState = layer.NewState(onName + " Out").WithAnimation(transitionClipOut).Speed(model.simpleOutTransition ? -1 : 1);
            onState.TransitionsTo(outState).When(onCase.Not()).WithTransitionDurationSeconds(transitionTime).WithTransitionExitTime(model.hasExitTime ? 1 : 0);
        } else {
            outState = onState;
        }

        onEqualsOut = outState == onState;

        var exitTransition = outState.TransitionsToExit();

        if (onEqualsOut) {
            exitTransition.When(onCase.Not()).WithTransitionExitTime(model.hasExitTime ? 1 : 0).WithTransitionDurationSeconds(transitionTime);
        } else {
            exitTransition.When().WithTransitionExitTime(1);
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

        if (restingClip == null && model.includeInRest) {
            restingClip = clip;

            if (model.defaultOn) {
                SetStartState(layer, onState.GetRaw());
                if (!enableExclusiveTag) {
                    off.TransitionsFromEntry().When(controller.Always());
                }
            }
        }
    }

    [FeatureBuilderAction(FeatureOrder.CollectToggleExclusiveTags)]
    public void ApplyExclusiveTags() {
        
        if (!enableExclusiveTag) return;

        var controller = GetFx();
        var paramsToTurnOff = new HashSet<VFABool>();
        var paramsToTurnToZero = new Dictionary<string, HashSet<(VFAInteger, int)>>();
        var allOthersOff = controller.Always();
        var isLocal = controller.IsLocal().IsTrue();
            
        foreach (var exclusiveTag in GetExclusiveTags()) {
            foreach (var other in allBuildersInRun
                        .OfType<ToggleBuilder>()
                        .Where(b => b != this)) {
                if (other.GetExclusiveTags().Contains(exclusiveTag)) {
                    var otherParam = other.GetParam();
                    if (otherParam != null) {
                        var otherOnCondition = other.useInt ? (otherParam as VFAInteger).IsEqualTo(other.intTarget) : (otherParam as VFABool).IsTrue();
                        if (other.useInt) {
                            if (param.Name() != otherParam.Name()) {
                                if (!paramsToTurnToZero.ContainsKey(otherParam.Name())) {
                                    paramsToTurnToZero[otherParam.Name()] = new HashSet<(VFAInteger, int)>();
                                }
                                paramsToTurnToZero[otherParam.Name()].Add((otherParam as VFAInteger, other.intTarget));
                            }
                        } else {
                            paramsToTurnOff.Add(otherParam as VFABool);
                            allOthersOff = allOthersOff.And(otherOnCondition.Not());
                        }
                    }
                }
            }
        }

        if (model.includeInRest && model.defaultOn) {
            var layer = GetLayer(layerName, controller);
            var off = GetOffState("Off", layer);
            off.TransitionsFromEntry().When(allOthersOff);
        }

        if (paramsToTurnOff.Count + paramsToTurnToZero.Count > 0) {

            var exclusiveLayer = GetLayerForParameters(GetPrimaryExclusive());
            var startState = GetOffState("Default", exclusiveLayer);
            var triggerState = exclusiveLayer.NewState(layerName);
            var onParam = useInt ? (param as VFAInteger).IsEqualTo(intTarget) : (param as VFABool).IsTrue();

            var intStates = new HashSet<(VFACondition, VFAState)>();

            triggerState.TransitionsToExit().When(onParam.Not());

            var allOrParam = controller.Never();

            foreach (var tag in paramsToTurnToZero.Keys) {
                var orParam = controller.Never();
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

            if (!useInt && model.exclusiveOffState) {
                startState.TransitionsTo(triggerState).When(allOthersOff);
                triggerState.Drives((param as VFABool), true);
            }
        }
    }

    public override string GetClipPrefix() {
        return "Toggle " + model.name.Replace('/', '_');
    }

    [FeatureBuilderAction(FeatureOrder.ApplyToggleRestingState)]
    public void ApplyRestingState() {
        if (restingClip != null) {
            var restingStateBuilder = allBuildersInRun
                .OfType<RestingStateBuilder>()
                .First();
            restingStateBuilder.ApplyClipToRestingState(restingClip, true);
        }
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
            advMenu.AddItem(new GUIContent("Run Animation to Completion"), hasExitTimeProp.boolValue, () => {
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
                if (hasExitTimeProp != null && hasExitTimeProp.boolValue)
                    tags.Add("Run to Completion");

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
            holdButtonProp,
            hasExitTimeProp
        ));

        return content;
    }
}

}


