using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using VF.Builder;
using VF.Feature.Base;
using VF.Inspector;
using VF.Model;
using VF.Model.Feature;
using VF.Model.StateAction;
using Object = UnityEngine.Object;
using Toggle = VF.Model.Feature.Toggle;

namespace VF.Feature {

public class ToggleBuilder : FeatureBuilder<Toggle> {
    private VFABool param;
    private AnimationClip restingClip;
    private string layerName;
    private Dictionary<VRC.SDK3.Avatars.Components.VRCAvatarDescriptor.AnimLayerType, VFAState> inStates = new Dictionary<VRC.SDK3.Avatars.Components.VRCAvatarDescriptor.AnimLayerType, VFAState>();
    private Dictionary<VRC.SDK3.Avatars.Components.VRCAvatarDescriptor.AnimLayerType, VFAState> outStates = new Dictionary<VRC.SDK3.Avatars.Components.VRCAvatarDescriptor.AnimLayerType, VFAState>();

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
    public VFABool GetParam() {
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

    [FeatureBuilderAction]
    public void Apply() {
        // If the toggle is setup to /actually/ toggle something (and it's not an off state for just an exclusive tag or something)
        // Then don't even bother adding it. The user probably removed the object, so the toggle shouldn't be present.
        // Toggle should still be added if it drives a global variable
        if (model.state.IsEmpty() && model.state.actions.Count > 0  && !model.enableDriveGlobalParam) {
            return;
        }

        if (model.slider) {
            var stops = new List<Puppet.Stop> {
                new Puppet.Stop(1, 0, model.state)
            };
            var puppet = new Puppet {
                name = model.name,
                saved = model.saved,
                slider = true,
                stops = stops,
                defaultX = model.slider && model.defaultOn ? model.defaultSliderValue : 0,
                enableIcon = model.enableIcon,
                icon = model.icon,
            };
            addOtherFeature(puppet);
            return;
        }

        var physBoneResetter = CreatePhysBoneResetter(model.resetPhysbones, model.name);

        layerName = model.name;
        var fx = GetFx();
        var layer = getLayer(layerName, fx);
        var off = getStartState("Off", layer);

        VFACondition onCase;

        var paramName = model.paramOverride ?? model.name;
        if (model.useInt) {
            var numParam = fx.NewInt(paramName, synced: true, saved: model.saved, def: model.defaultOn ? 1 : 0, usePrefix: model.usePrefixOnParam);
            onCase = numParam.IsNotEqualTo(0);
        } else {
            var boolParam = fx.NewBool(paramName, synced: true, saved: model.saved, def: model.defaultOn, usePrefix: model.usePrefixOnParam);
            param = boolParam;
            onCase = boolParam.IsTrue();
        }

        if (!model.hasTransitionTime)  model.transitionTime = 0;
        if (!model.hasExitTime) model.exitTime = 0;
        
        if (model.separateLocal) {
            var isLocal = fx.IsLocal().IsTrue();
            Apply(fx, layer, off, onCase.And(isLocal.Not()), layerName + " On Remote", model.state, model.transitionStateIn, model.transitionStateOut, physBoneResetter);
            Apply(fx, layer, off, onCase.And(isLocal), layerName + " On Local", model.localState, model.localTransitionStateIn, model.localTransitionStateOut, physBoneResetter);
        } else {
            Apply(fx, layer, off, onCase, layerName + " On", model.state, model.transitionStateIn, model.transitionStateOut, physBoneResetter);
        }

        if (model.addMenuItem && !string.IsNullOrWhiteSpace(model.name)) {
            if (model.isButton) {
                manager.GetMenu().NewMenuButton(
                    model.name,
                    param,
                    icon: model.enableIcon ? model.icon : null
                );
            } else {
                manager.GetMenu().NewMenuToggle(
                    model.name,
                    param,
                    icon: model.enableIcon ? model.icon : null
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
        var isHumanoidLayer = controller.GetType() != VRC.SDK3.Avatars.Components.VRCAvatarDescriptor.AnimLayerType.FX;

        var clip = LoadState(onName, action, isHumanoidLayer);

        if (controller.GetType() == VRC.SDK3.Avatars.Components.VRCAvatarDescriptor.AnimLayerType.FX && IsHuanoid(action)) {
            var actionLayer = GetAction();
            var layer2 = getLayer(layerName, actionLayer);
            var off2 = getStartState("Off", layer2);
            var boolParam2 = actionLayer.NewBool(model.name, synced: !string.IsNullOrWhiteSpace(model.name), saved: model.saved, def: model.defaultOn, usePrefix: model.usePrefixOnParam);
            var onCase2 = boolParam2.IsTrue();
            Apply(actionLayer, layer2, off2, onCase2, onName, action, inAction, outAction, physBoneResetter);
            if (clip == GetFx().GetNoopClip()) return; // if only a proxy animation don't worry about making toggle in FX layer
        } else if (controller.GetType() == VRC.SDK3.Avatars.Components.VRCAvatarDescriptor.AnimLayerType.Action) {
            var maskName = getMaskName(clip);
            if (maskName.ToLower().Contains("hand")) {
                var gestureLayer = GetGesture();
                var layer2 = getLayer(layerName, gestureLayer, maskName);
                var off2 = getStartState("Off", layer2);
                var boolParam2 = gestureLayer.NewBool(model.name, synced: !string.IsNullOrWhiteSpace(model.name), saved: model.saved, def: model.defaultOn, usePrefix: model.usePrefixOnParam);
                var onCase2 = boolParam2.IsTrue();
                Apply(gestureLayer, layer2, off2, onCase2, onName, action, inAction, outAction, physBoneResetter);
                return;
            }
            
        }

        if (restingClip == null && model.includeInRest) {
            restingClip = clip;
            var defaultsManager = allBuildersInRun
                .OfType<FixWriteDefaultsBuilder>()
                .First();
            defaultsManager.forceRecordBindings.UnionWith(AnimationUtility.GetCurveBindings(clip));
            defaultsManager.forceRecordBindings.UnionWith(AnimationUtility.GetObjectReferenceCurveBindings(clip));
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

        var exitTransition = outState.TransitionsToExit();

        if (outState == onState) {
            exitTransition.When(onCase.Not()).WithTransitionExitTime(model.exitTime).WithTransitionDurationSeconds(model.transitionTime);
        } else {
            exitTransition.When().WithTransitionExitTime(1);
        }

        inStates[controller.GetType()] = inState;
        outStates[controller.GetType()] = outState;


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
        
        ControllerManager[] controllers = { GetFx(), GetAction(), GetGesture() };

        foreach (var controller in controllers) {
            foreach (var exclusiveTag in GetExclusiveTags()) {
                var paramsToTurnOff = new List<VFABool>();
                var allOthersOff = controller.Always();
                foreach (var other in allBuildersInRun
                            .OfType<ToggleBuilder>()
                            .Where(b => b != this)) {
                    if (other.GetExclusiveTags().Contains(exclusiveTag)) {
                        var otherParam = other.GetParam();
                        allOthersOff = allOthersOff.And(otherParam.IsFalse());
                        if (otherParam != null) {
                            paramsToTurnOff.Add(otherParam);
                            VFAState outState = outStates.ContainsKey(controller.GetType()) ? outStates[controller.GetType()] : null;
                            VFAState inState = other.inStates.ContainsKey(controller.GetType()) ? other.inStates[controller.GetType()] : null;
                            if (inState != null && outState != null && inState.GetRawStateMachine() == outState.GetRawStateMachine()) {
                                outState.TransitionsTo(inState).When(otherParam.IsTrue()).WithTransitionExitTime(1).WithTransitionDurationSeconds(model.transitionTime);
                            }
                        }
                    }
                }

                if (controller.GetType() == VRC.SDK3.Avatars.Components.VRCAvatarDescriptor.AnimLayerType.FX) {
                    var layer = getLayerForParameters(exclusiveTag);
                    var triggerState = layer.NewState(layerName);

                    triggerState.TransitionsFromAny().When(param.IsTrue());
                    foreach (var p in paramsToTurnOff) {
                        triggerState.Drives(p, false);
                    }

                    if (model.exclusiveOffState) {
                        triggerState.TransitionsFromAny().When(allOthersOff);
                        triggerState.Drives(param, true);
                    }
                }
            }
        }
    }

    public override string GetClipPrefix() {
        return "Toggle " + model.name.Replace('/', '_');
    }

    [FeatureBuilderAction(FeatureOrder.ApplyToggleRestingState)]
    public void ApplyRestingState() {
        if (restingClip != null) {
            ResetAnimatorBuilder.WithoutAnimator(avatarObject, () => { restingClip.SampleAnimation(avatarObject, 0); });
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
        var isButtonProp = prop.FindPropertyRelative("isButton");
        var hasTransitionTimeProp = prop.FindPropertyRelative("hasTransitionTime");
        var hasExitTimeProp = prop.FindPropertyRelative("hasExitTime");

        var flex = new VisualElement {
            style = {
                flexDirection = FlexDirection.Row,
                alignItems = Align.FlexStart
            }
        };
        content.Add(flex);

        var name = VRCFuryEditorUtils.Prop(prop.FindPropertyRelative("name"), "Menu Path");
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

            advMenu.AddItem(new GUIContent("Is Button"), isButtonProp.boolValue, () => {
                    isButtonProp.boolValue = !isButtonProp.boolValue;
                    prop.serializedObject.ApplyModifiedProperties();
                });


            advMenu.AddItem(new GUIContent("Has Transition Time"), hasTransitionTimeProp.boolValue, () => {
                    hasTransitionTimeProp.boolValue = !hasTransitionTimeProp.boolValue;
                    prop.serializedObject.ApplyModifiedProperties();
                });
            advMenu.AddItem(new GUIContent("Has Exit Time"), hasExitTimeProp.boolValue, () => {
                    hasExitTimeProp.boolValue = !hasExitTimeProp.boolValue;
                    prop.serializedObject.ApplyModifiedProperties();
                });

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
                if (isButtonProp != null && isButtonProp.boolValue)
                    tags.Add("Button");

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
            isButtonProp
        ));

        return content;
    }
}

}


