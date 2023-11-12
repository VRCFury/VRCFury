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
using VF.Injector;
using VF.Inspector;
using VF.Model;
using VF.Service;
using VF.Utils;
using VF.Utils.Controller;
using Toggle = VF.Model.Feature.Toggle;

namespace VF.Feature {

public class ToggleBuilder : FeatureBuilder<Toggle> {
    [VFAutowired] private readonly ActionClipService actionClipService;
    [VFAutowired] private readonly PhysboneResetService physboneResetService;

    private List<VFState> exclusiveTagTriggeringStates = new List<VFState>();
    private VFABool param;
    private AnimationClip restingClip;

    private bool addMenuItem;
    private string primaryExclusive = null;
    private bool useInt;
    private int intTarget = -1;

    private const string menuPathTooltip = "Menu Path is where you'd like the toggle to be located in the menu. This is unrelated"
        + " to the menu filenames -- simply enter the title you'd like to use. If you'd like the toggle to be in a submenu, use slashes. For example:\n\n"
        + "If you want the toggle to be called 'Shirt' in the root menu, you'd put:\nShirt\n\n"
        + "If you want the toggle to be called 'Pants' in a submenu called 'Clothing', you'd put:\nClothing/Pants";

   public ISet<string> SeparateList(string objects) {
        return objects.Split(',')
            .Select(tag => tag.Trim())
            .Where(tag => !string.IsNullOrWhiteSpace(tag))
            .ToImmutableHashSet();
    }

    public ISet<string> GetExclusiveTags() {
        if(model.enableExclusiveTag)
            return SeparateList(model.exclusiveTag);
        return new HashSet<string>(); 
    }

    public string GetPrimaryExclusive() {
        if (primaryExclusive == null) {
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

            primaryExclusive = targetTag;
        }
        return primaryExclusive;
    }

    private void CheckExclusives() {
        if (!model.enableExclusiveTag) return;
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

    public ISet<string> GetGlobalParams() {
        if(model.enableDriveGlobalParam)
            return SeparateList(model.driveGlobalParam);
        return new HashSet<string>(); 
    }

    private void SetStartState(VFLayer layer, AnimatorState state) {
        layer.GetRawStateMachine().defaultState = state;
    }

    public VFABool GetParam() {
        return param;
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
            synced: true,
            saved: model.saved,
            def: model.defaultOn ? model.defaultSliderValue : 0,
            usePrefix: usePrefixOnParam
        );

        var hasTitle = !string.IsNullOrEmpty(model.name);
        var hasIcon = model.enableIcon && model.icon?.Get() != null;
        if (model.addMenuItem && (hasTitle || hasIcon)) {
            manager.GetMenu().NewMenuSlider(
                model.name,
                x,
                icon: model.enableIcon ? model.icon?.Get() : null
            );
        }

        var clip = actionClipService.LoadState("On", model.state);
        if (ClipBuilderService.IsStaticMotion(clip)) {
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
        addMenuItem = model.addMenuItem;
        useInt = model.useInt;
        var hasTitle = !string.IsNullOrEmpty(model.name);
        var hasIcon = model.enableIcon && model.icon?.Get() != null;
        if (!hasTitle && !hasIcon) addMenuItem = false;

        if (model.slider) {
            CreateSlider();
            return;
        }

        CheckExclusives();

        var physBoneResetter = physboneResetService.CreatePhysBoneResetter(model.resetPhysbones, model.name);

        var layerName = model.name;
        if (!hasTitle && model.useGlobalParam) layerName = model.globalParam;

        var fx = GetFx();
        var layer = fx.NewLayer(layerName);
        var off = layer.NewState("Off");

        var (paramName, usePrefixOnParam) = GetParamName();
        VFCondition onCase;
        if (useInt) {
            if (intTarget == -1) {
                var numParam = fx.NewInt(paramName, synced: true, saved: model.saved, def: model.defaultOn ? 1 : 0, usePrefix: usePrefixOnParam);
                onCase = numParam.IsNotEqualTo(0);
            } else {
                var boolParam = fx.NewBool(paramName, synced: false, saved: model.saved, def: model.defaultOn, usePrefix: usePrefixOnParam);
                param = boolParam;
                onCase = boolParam.IsTrue();
            }
        } else {
            var boolParam = fx.NewBool(paramName, synced: true, saved: model.saved, def: model.defaultOn, usePrefix: usePrefixOnParam);
            param = boolParam;
            onCase = boolParam.IsTrue();
        }

        if (model.separateLocal) {
            var isLocal = fx.IsLocal().IsTrue();
            Apply(fx, layer, off, onCase.And(isLocal.Not()), "On Remote", model.state, model.transitionStateIn, model.transitionStateOut, physBoneResetter);
            Apply(fx, layer, off, onCase.And(isLocal), "On Local", model.localState, model.localTransitionStateIn, model.localTransitionStateOut, physBoneResetter);
        } else {
            Apply(fx, layer, off, onCase, "On", model.state, model.transitionStateIn, model.transitionStateOut, physBoneResetter);
        }

        
        if (addMenuItem) {
            if (!useInt) {
                if (model.holdButton) {
                    manager.GetMenu().NewMenuButton(
                        model.name,
                        param,
                        icon: model.icon?.Get()
                    );
                } else {
                    manager.GetMenu().NewMenuToggle(
                        model.name,
                        param,
                        icon: model.icon?.Get()
                    );
                }
            } else {
                var intParam = fx.NewInt("VF_" + GetPrimaryExclusive() + "_Exclusives", synced: true, saved: model.saved, def: model.defaultOn ? intTarget : 0, usePrefix: false);
                var aliasLayer = fx.NewLayer(layerName + "_Alias");
                var startState = aliasLayer.NewState("Start").Drives(param, false);
                var aliasState = aliasLayer.NewState("Alias").Drives(param, true);
                startState.TransitionsTo(aliasState).When(intParam.IsEqualTo(intTarget));
                aliasState.TransitionsTo(startState).When(intParam.IsEqualTo(intTarget).Not());
                intAliases[param] = (intParam, intTarget);
                if (model.holdButton) {
                    manager.GetMenu().NewMenuButton(
                        model.name,
                        intParam,
                        icon: model.icon?.Get(),
                        value: intTarget
                    );
                } else {
                    manager.GetMenu().NewMenuToggle(
                        model.name,
                        intParam,
                        icon: model.icon?.Get(),
                        value: intTarget
                    );
                }
            }
        }
    }

    private void Apply(
        ControllerManager fx,
        VFLayer layer,
        VFState off,
        VFCondition onCase,
        string onName,
        State action,
        State inAction,
        State outAction,
        VFABool physBoneResetter
    ) {
        var clip = actionClipService.LoadState(onName, action);

        if (restingClip == null && model.includeInRest) {
            restingClip = clip;
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

        VFState inState;
        VFState onState;
        VFState outState;

        if (model.hasTransition) {
            var transitionClipIn = actionClipService.LoadState(onName + " In", inAction);

            // if clip is empty, copy last frame of transition
            if (clip == fx.GetEmptyClip()) {
                clip = fx.NewClip(onName);
                clip.CopyFromLast(transitionClipIn);
            }

            inState = layer.NewState(onName + " In").WithAnimation(transitionClipIn);
            onState = layer.NewState(onName).WithAnimation(clip);
            inState.TransitionsTo(onState).When().WithTransitionExitTime(1);
        } else {
            inState = onState = layer.NewState(onName).WithAnimation(clip);
        }
        exclusiveTagTriggeringStates.Add(inState);
        off.TransitionsTo(inState).When(onCase);

        if (model.simpleOutTransition) outAction = inAction;
        if (model.hasTransition) {
            var transitionClipOut = actionClipService.LoadState(onName + " Out", outAction);
            outState = layer.NewState(onName + " Out").WithAnimation(transitionClipOut).Speed(model.simpleOutTransition ? -1 : 1);
            onState.TransitionsTo(outState).When(onCase.Not()).WithTransitionExitTime(model.hasExitTime ? 1 : -1);
        } else {
            outState = onState;
        }

        var exitTransition = outState.TransitionsToExit();

        if (outState == onState) {
            exitTransition.When(onCase.Not()).WithTransitionExitTime(model.hasExitTime ? 1 : -1);
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
                var driveGlobal = fx.NewBool(
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

        if (model.includeInRest && model.defaultOn) {
            SetStartState(layer, onState.GetRaw());
        }
    }

     [FeatureBuilderAction(FeatureOrder.CollectToggleExclusiveTags)]
     public void ApplyExclusiveTags() {
        if (exclusiveTagTriggeringStates.Count == 0) return;

        var fx = GetFx();
        var allOthersOffCondition = fx.Always();

        var myTags = GetExclusiveTags();
        foreach (var other in allBuildersInRun
                     .OfType<ToggleBuilder>()
                     .Where(b => b != this)) {
            var otherTags = other.GetExclusiveTags();
            var conflictsWithOther = myTags.Any(myTag => otherTags.Contains(myTag));
            if (conflictsWithOther) {
                var otherParam = other.GetParam();
                if (otherParam != null) {
                    foreach (var state in exclusiveTagTriggeringStates) {
                        state.Drives(otherParam, false);
                        if (intAliases.ContainsKey(otherParam) && !(intAliases.ContainsKey(param) && intAliases[otherParam].Item1.Name() == intAliases[param].Item1.Name())) {
                            state.Drives(intAliases[otherParam].Item1, 0);;
                        }
                    }
                    allOthersOffCondition = allOthersOffCondition.And(otherParam.IsFalse());
                }
            }
        }

        if (model.exclusiveOffState && param != null) {
            var layer = fx.NewLayer(model.name + " - Off Trigger");
            var off = layer.NewState("Idle");
            var on = layer.NewState("Trigger");
            off.TransitionsTo(on).When(allOthersOffCondition);
            on.TransitionsTo(off).When(allOthersOffCondition.Not().Or(param.IsFalse()));
            on.Drives(param, true);
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

        content.Add(VRCFuryEditorUtils.RefreshOnChange(() => {

            VisualElement MakeSingle(string tin, string state, string tout) {
                var single = new VisualElement();
                if (hasTransitionProp.boolValue) {
                    single.Add(MakeTabbed(
                        "Transition In:",
                        VRCFuryEditorUtils.Prop(prop.FindPropertyRelative(tin))
                    ));
                    single.Add(MakeTabbed(
                        "Then do this until turned off:",
                        VRCFuryStateEditor.render(prop.FindPropertyRelative(state))
                    ));
                    var cout = new VisualElement();
                    cout.Add(VRCFuryEditorUtils.Prop(prop.FindPropertyRelative("simpleOutTransition"), "Transition Out is reverse of Transition In"));
                    if (!simpleOutTransitionProp.boolValue)
                        cout.Add(VRCFuryEditorUtils.Prop(prop.FindPropertyRelative(tout)));
                    single.Add(MakeTabbed("Then transition out:", cout));
                } else {
                    single.Add(VRCFuryStateEditor.render(prop.FindPropertyRelative(state)));
                }
                return single;
            }

            var remoteSingle = MakeSingle("transitionStateIn", "state", "transitionStateOut");
            var c = new VisualElement();
            if (separateLocalProp.boolValue) {
                var localSingle = MakeSingle("localTransitionStateIn", "localState", "localTransitionStateOut");
                c.Add(MakeTabbed("In local:", localSingle));
                c.Add(MakeTabbed("In remote:", remoteSingle));
            } else {
                c = remoteSingle;
            }

            return MakeTabbed("When toggle is enabled:", c);
        }, separateLocalProp, hasTransitionProp, simpleOutTransitionProp));

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

    private VisualElement MakeTabbed(string label, VisualElement child) {
        var output = new VisualElement();
        output.Add(VRCFuryEditorUtils.WrappedLabel(label, style: s => s.unityFontStyleAndWeight = FontStyle.Bold));
        var tabbed = new VisualElement { style = { paddingLeft = 5 } };
        tabbed.Add(child);
        output.Add(tabbed);
        return output;
    }
}

}


