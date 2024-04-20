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
using VF.Model.StateAction;
using VF.Service;
using VF.Utils;
using VF.Utils.Controller;
using Toggle = VF.Model.Feature.Toggle;

namespace VF.Feature {

public class ToggleBuilder : FeatureBuilder<Toggle> {
    [VFAutowired] private readonly ObjectMoveService mover;
    [VFAutowired] private readonly ActionClipService actionClipService;
    [VFAutowired] private readonly RestingStateService restingState;
    [VFAutowired] private readonly FixWriteDefaultsBuilder writeDefaultsManager;

    private readonly List<VFState> exclusiveTagTriggeringStates = new List<VFState>();
    private VFAParam exclusiveParam;
    private AnimationClip savedRestingClip;

    public const string menuPathTooltip = "This is where you'd like the toggle to be located in the menu. This is unrelated"
        + " to the menu filenames -- simply enter the title you'd like to use. If you'd like the toggle to be in a submenu, use slashes. For example:\n\n"
        + "If you want the toggle to be called 'Shirt' in the root menu, you'd put:\nShirt\n\n"
        + "If you want the toggle to be called 'Pants' in a submenu called 'Clothing', you'd put:\nClothing/Pants";

    private static ISet<string> SeparateList(string str) {
        return str.Split(',')
            .Select(tag => tag.Trim())
            .Where(tag => !string.IsNullOrWhiteSpace(tag))
            .ToImmutableHashSet();
    }

    private ISet<string> GetExclusiveTags() {
        if (model.enableExclusiveTag) {
            return SeparateList(model.exclusiveTag);
        }
        return new HashSet<string>(); 
    }

    private ISet<string> GetDriveGlobalParams() {
        if (model.enableDriveGlobalParam) {
            return SeparateList(model.driveGlobalParam);
        }
        return new HashSet<string>(); 
    }

    public VFAParam GetExclusiveParam() {
        return exclusiveParam;
    }

    private (string,bool,bool) GetParamName() {
        if (model.paramOverride != null) {
            return (model.paramOverride, false, false);
        }
        if (model.useGlobalParam && !string.IsNullOrWhiteSpace(model.globalParam)) {
            return (model.globalParam, false, false);
        }
        var existingParam = manager.GetMenu().GetMenuParam(model.name, model.slider);
        if (existingParam != null) {
            return (existingParam, false, true);
        }
        return (model.name, model.usePrefixOnParam, false);
    }

    [FeatureBuilderAction]
    public void Apply() {
        var fx = GetFx();
        var hasTitle = !string.IsNullOrEmpty(model.name);
        var hasIcon = model.enableIcon && model.icon?.Get() != null;
        var addMenuItem = model.addMenuItem && (hasTitle || hasIcon);

        var synced = true;
        if (model.useGlobalParam && FullControllerBuilder.VRChatGlobalParams.Contains(model.globalParam)) {
            synced = false;
        }

        var (paramName, usePrefixOnParam, menuItemAlreadyMade) = GetParamName();
        VFCondition onCase;
        VFAFloat weight = null;
        bool defaultOn;
        if (model.slider) {
            var param = fx.NewFloat(
                paramName,
                synced: synced,
                saved: model.saved,
                def: model.defaultSliderValue,
                usePrefix: usePrefixOnParam
            );
            exclusiveParam = param;
            onCase = model.sliderInactiveAtZero ? param.IsGreaterThan(0) : fx.Always();
            defaultOn = model.sliderInactiveAtZero ? model.defaultSliderValue > 0 : true;
            weight = param;
            if (addMenuItem && !menuItemAlreadyMade) {
                manager.GetMenu().NewMenuSlider(
                    model.name,
                    param,
                    icon: model.enableIcon ? model.icon?.Get() : null
                );
            }
        } else if (model.useInt) {
            var param = fx.NewInt(paramName, synced: true, saved: model.saved, def: model.defaultOn ? 1 : 0, usePrefix: usePrefixOnParam);
            exclusiveParam = param;
            onCase = param.IsNotEqualTo(0);
            defaultOn = model.defaultOn;
        } else {
            var param = fx.NewBool(paramName, synced: synced, saved: model.saved, def: model.defaultOn, usePrefix: usePrefixOnParam);
            exclusiveParam = param;
            onCase = param.IsTrue();
            defaultOn = model.defaultOn;
            if (addMenuItem && !menuItemAlreadyMade) {
                if (model.holdButton) {
                    manager.GetMenu().NewMenuButton(
                        model.name,
                        param,
                        icon: model.enableIcon ? model.icon?.Get() : null
                    );
                } else {
                    manager.GetMenu().NewMenuToggle(
                        model.name,
                        param,
                        icon: model.enableIcon ? model.icon?.Get() : null
                    );
                }
            }
        }

        var layerName = model.name;
        if (string.IsNullOrEmpty(layerName) && model.useGlobalParam) layerName = model.globalParam;
        if (string.IsNullOrEmpty(layerName)) layerName = "Toggle";

        var layer = fx.NewLayer(layerName);
        var off = layer.NewState("Off");

        if (model.separateLocal) {
            var isLocal = fx.IsLocal().IsTrue();
            Apply(fx, layer, off, onCase.And(isLocal.Not()), weight, defaultOn, "On Remote", model.state, model.transitionStateIn, model.transitionStateOut, model.transitionTimeIn, model.transitionTimeOut);
            Apply(fx, layer, off, onCase.And(isLocal), weight, defaultOn, "On Local", model.localState, model.localTransitionStateIn, model.localTransitionStateOut, model.localTransitionTimeIn, model.localTransitionTimeOut);
        } else {
            Apply(fx, layer, off, onCase, weight, defaultOn, "On", model.state, model.transitionStateIn, model.transitionStateOut, model.transitionTimeIn, model.transitionTimeOut);
        }
    }

    private void Apply(
        ControllerManager fx,
        VFLayer layer,
        VFState off,
        VFCondition onCase,
        VFAFloat weight,
        bool defaultOn,
        string onName,
        State action,
        State inAction,
        State outAction,
        float inTime,
        float outTime
    ) {
        var clip = actionClipService.LoadState(onName, action);

        if (model.securityEnabled) {
            var securityLockUnlocked = allBuildersInRun
                .OfType<SecurityLockBuilder>()
                .Select(f => f.GetEnabled())
                .FirstOrDefault();
            if (securityLockUnlocked != null) {
                onCase = onCase.And(securityLockUnlocked.IsTrue());
            } else {
                Debug.LogWarning("Security pin not set, restriction disabled");
            }
        }

        VFState inState;
        VFState onState;

        AnimationClip restingClip;
        if (weight != null) {
            inState = onState = layer.NewState(onName);
            if (clip.IsStatic()) {
                clip = clipBuilder.MergeSingleFrameClips(
                    (0, new AnimationClip()),
                    (1, clip)
                );
                clip.UseLinearTangents();
            }
            clip.SetLooping(false);
            onState.WithAnimation(clip).MotionTime(weight);
            onState.TransitionsToExit().When(onCase.Not());
            restingClip = clip.Evaluate(model.defaultSliderValue * clip.length);
        } else if (model.hasTransition) {
            var inClip = actionClipService.LoadState(onName + " In", inAction);
            // if clip is empty, copy last frame of transition
            if (clip.GetAllBindings().Length == 0) {
                clip = fx.NewClip(onName);
                clip.CopyFromLast(inClip);
            }
            var outClip = model.simpleOutTransition ? inClip : actionClipService.LoadState(onName + " Out", outAction);
            var outSpeed = model.simpleOutTransition ? -1 : 1;
            
            // Copy "object enabled" and "material" states to in and out clips if they don't already have them
            // This is a convenience feature, so that people don't need to turn on objects in their transitions
            // if it's already on in the main clip.
            foreach (var (binding,curve) in clip.GetAllCurves()) {
                if (!curve.IsFloat) {
                    if (inClip.GetObjectCurve(binding) == null) inClip.SetCurve(binding, curve.GetFirst());
                    if (outClip.GetObjectCurve(binding) == null) outClip.SetCurve(binding, curve.GetLast());
                } else if (binding.type == typeof(GameObject)) {
                    // Only expand gameobject "enabled" into intro and outro if we're turning something "on"
                    // If we're turning it off, it probably shouldn't be off during the transition.
                    var first = curve.GetFirst().GetFloat();
                    var last = curve.GetLast().GetFloat();
                    if (first > 0.99 && last > 0.99) {
                        if (inClip.GetFloatCurve(binding) == null) inClip.SetCurve(binding, curve.GetFirst());
                        if (outClip.GetFloatCurve(binding) == null) outClip.SetCurve(binding, curve.GetLast());
                    }
                }
            }

            inState = layer.NewState(onName + " In").WithAnimation(inClip);
            onState = layer.NewState(onName).WithAnimation(clip);
            inState.TransitionsTo(onState).When(fx.Always()).WithTransitionExitTime(inClip.IsEmptyOrZeroLength() ? -1 : 1).WithTransitionDurationSeconds(inTime);

            var outState = layer.NewState(onName + " Out").WithAnimation(outClip).Speed(outSpeed);
            onState.TransitionsTo(outState).When(onCase.Not()).WithTransitionExitTime(model.hasExitTime ? 1 : -1).WithTransitionDurationSeconds(outTime);
            outState.TransitionsToExit().When(fx.Always()).WithTransitionExitTime(outClip.IsEmptyOrZeroLength() ? -1 : 1);
            restingClip = clip;
        } else {
            inState = onState = layer.NewState(onName).WithAnimation(clip);
            onState.TransitionsToExit().When(onCase.Not()).WithTransitionExitTime(model.hasExitTime ? 1 : -1);
            restingClip = clip;
        }

        exclusiveTagTriggeringStates.Add(inState);
        off.TransitionsTo(inState).When(onCase);

        if (model.enableDriveGlobalParam) {
            foreach(var p in GetDriveGlobalParams()) {
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

        if (defaultOn && !model.separateLocal && !model.securityEnabled) {
            layer.GetRawStateMachine().defaultState = onState.GetRaw();
            off.TransitionsFromEntry().When();
        }

        if (savedRestingClip == null) {
            var copy = new AnimationClip();
            copy.CopyFrom(restingClip);
            savedRestingClip = copy;
            mover.AddAdditionalManagedClip(savedRestingClip);
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
                var otherParam = other.GetExclusiveParam();
                if (otherParam != null) {
                    foreach (var state in exclusiveTagTriggeringStates) {
                        state.Drives(otherParam, 0);
                    }
                    allOthersOffCondition = allOthersOffCondition.And(otherParam.IsFalse());
                }
            }
        }

        if (model.exclusiveOffState && exclusiveParam != null) {
            var layer = fx.NewLayer(model.name + " - Off Trigger");
            var off = layer.NewState("Idle");
            var on = layer.NewState("Trigger");
            off.TransitionsTo(on).When(allOthersOffCondition);
            on.TransitionsTo(off).When(allOthersOffCondition.Not().Or(exclusiveParam.IsFalse()));
            on.Drives(exclusiveParam, 1);
        }
    }

    public override string GetClipPrefix() {
        return "Toggle " + model.name.Replace('/', '_');
    }

    [FeatureBuilderAction(FeatureOrder.ApplyToggleRestingState)]
    public void ApplyRestingState() {
        if (savedRestingClip == null) return;

        bool includeInRest;
        if (model.slider) {
            includeInRest = model.sliderInactiveAtZero ? model.defaultSliderValue > 0 : true;
        } else {
            includeInRest = model.defaultOn;
        }
        if (model.invertRestLogic) includeInRest = !includeInRest;
        if (!includeInRest) return;

        if (!savedRestingClip.IsStatic()) return;

        foreach (var b in savedRestingClip.GetFloatBindings())
            writeDefaultsManager.RecordDefaultNow(b, true, true);
        foreach (var b in savedRestingClip.GetObjectBindings())
            writeDefaultsManager.RecordDefaultNow(b, false, true);
        restingState.ApplyClipToRestingState(savedRestingClip);
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
        var invertRestLogicProp = prop.FindPropertyRelative("invertRestLogic");
        var exclusiveOffStateProp = prop.FindPropertyRelative("exclusiveOffState");
        var enableExclusiveTagProp = prop.FindPropertyRelative("enableExclusiveTag");
        var enableIconProp = prop.FindPropertyRelative("enableIcon");
        var enableDriveGlobalParamProp = prop.FindPropertyRelative("enableDriveGlobalParam");
        var separateLocalProp = prop.FindPropertyRelative("separateLocal");
        var hasTransitionProp = prop.FindPropertyRelative("hasTransition");
        var simpleOutTransitionProp = prop.FindPropertyRelative("simpleOutTransition");
        var defaultSliderProp = prop.FindPropertyRelative("defaultSliderValue");
        var hasExitTimeProp = prop.FindPropertyRelative("hasExitTime");
        var useGlobalParamProp = prop.FindPropertyRelative("useGlobalParam");
        var globalParamProp = prop.FindPropertyRelative("globalParam");
        var holdButtonProp = prop.FindPropertyRelative("holdButton");

        var flex = new VisualElement().Row();
        content.Add(flex);

        flex.Add(VRCFuryEditorUtils.Prop(prop.FindPropertyRelative("name"), "Menu Path", tooltip: menuPathTooltip).FlexGrow(1));

        var button = new Button()
            .Text("Options")
            .OnClick(() => {
                var advMenu = new GenericMenu();

                advMenu.AddItem(new GUIContent("Saved Between Worlds"), savedProp.boolValue, () => {
                    savedProp.boolValue = !savedProp.boolValue;
                    prop.serializedObject.ApplyModifiedProperties();
                });

                advMenu.AddItem(new GUIContent("Use Slider Wheel"), sliderProp.boolValue, () => {
                    sliderProp.boolValue = !sliderProp.boolValue;
                    invertRestLogicProp.boolValue = false;
                    prop.serializedObject.ApplyModifiedProperties();
                });

                advMenu.AddItem(new GUIContent("Protect with Security"), securityEnabledProp.boolValue, () => {
                    securityEnabledProp.boolValue = !securityEnabledProp.boolValue;
                    prop.serializedObject.ApplyModifiedProperties();
                });

                if (!sliderProp.boolValue) {
                    advMenu.AddItem(new GUIContent("Default On"), defaultOnProp.boolValue, () => {
                        defaultOnProp.boolValue = !defaultOnProp.boolValue;
                        invertRestLogicProp.boolValue = false;
                        prop.serializedObject.ApplyModifiedProperties();
                    });
                    
                    advMenu.AddItem(new GUIContent("Run Animation to Completion"), hasExitTimeProp.boolValue, () => {
                        hasExitTimeProp.boolValue = !hasExitTimeProp.boolValue;
                        prop.serializedObject.ApplyModifiedProperties();
                    });
                    
                    advMenu.AddItem(new GUIContent("Hold Button"), holdButtonProp.boolValue, () => {
                        holdButtonProp.boolValue = !holdButtonProp.boolValue;
                        prop.serializedObject.ApplyModifiedProperties();
                    });
                }

                advMenu.AddItem(new GUIContent((defaultOnProp.boolValue || sliderProp.boolValue) ? "Hide when animator disabled" : "Show when animator disabled"), invertRestLogicProp.boolValue, () => {
                    invertRestLogicProp.boolValue = !invertRestLogicProp.boolValue;
                    prop.serializedObject.ApplyModifiedProperties();
                });

                advMenu.AddItem(new GUIContent("Enable Exclusive Tags"), enableExclusiveTagProp.boolValue, () => {
                    enableExclusiveTagProp.boolValue = !enableExclusiveTagProp.boolValue;
                    prop.serializedObject.ApplyModifiedProperties();
                });

                advMenu.AddItem(new GUIContent("This is Exclusive Off State"), exclusiveOffStateProp.boolValue, () => {
                    exclusiveOffStateProp.boolValue = !exclusiveOffStateProp.boolValue;
                    prop.serializedObject.ApplyModifiedProperties();
                });

                advMenu.AddItem(new GUIContent("Set Custom Menu Icon"), enableIconProp.boolValue, () => {
                    enableIconProp.boolValue = !enableIconProp.boolValue;
                    prop.serializedObject.ApplyModifiedProperties();
                });

                advMenu.AddItem(new GUIContent("Drive a Global Parameter"), enableDriveGlobalParamProp.boolValue, () => {
                    enableDriveGlobalParamProp.boolValue = !enableDriveGlobalParamProp.boolValue;
                    prop.serializedObject.ApplyModifiedProperties();
                });

                advMenu.AddItem(new GUIContent("Separate Local State"), separateLocalProp.boolValue, () => {
                    separateLocalProp.boolValue = !separateLocalProp.boolValue;
                    prop.serializedObject.ApplyModifiedProperties();
                });

                advMenu.AddItem(new GUIContent("Enable Transition State"), hasTransitionProp.boolValue, () => {
                    hasTransitionProp.boolValue = !hasTransitionProp.boolValue;
                    prop.serializedObject.ApplyModifiedProperties();
                });

                advMenu.AddItem(new GUIContent("Use a Global Parameter"), useGlobalParamProp.boolValue, () => {
                    useGlobalParamProp.boolValue = !useGlobalParamProp.boolValue;
                    prop.serializedObject.ApplyModifiedProperties();
                });

                advMenu.ShowAsContext();
            });
        flex.Add(button);

        content.Add(VRCFuryEditorUtils.RefreshOnChange(() => {
            var c = new VisualElement();
            if (enableExclusiveTagProp.boolValue) {
                c.Add(VRCFuryEditorUtils.Prop(prop.FindPropertyRelative("exclusiveTag"), "Exclusive Tags"));
            }
            return c;
        }, enableExclusiveTagProp));

        content.Add(VRCFuryEditorUtils.RefreshOnChange(() => {
            var c = new VisualElement();
            if (useGlobalParamProp.boolValue) {
                c.Add(VRCFuryEditorUtils.Prop(prop.FindPropertyRelative("globalParam"), "Global Parameter"));
            }

            return c;
        }, useGlobalParamProp));

        content.Add(VRCFuryEditorUtils.RefreshOnChange(() => {
            var c = new VisualElement();
            if (enableIconProp.boolValue) {
                c.Add(VRCFuryEditorUtils.Prop(prop.FindPropertyRelative("icon"), "Menu Icon"));
            }
            return c;
        }, enableIconProp));

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

        content.Add(VRCFuryEditorUtils.RefreshOnChange(() => {

            VisualElement MakeSingle(string tin, string state, string tout, string timeIn, string timeOut) {
                var single = new VisualElement();
                if (hasTransitionProp.boolValue) {
                    single.Add(MakeTabbed(
                        "Transition in:",
                        VRCFuryEditorUtils.Prop(prop.FindPropertyRelative(tin))
                    ));
                    single.Add(MakeTabbed(
                        "Then blend for this much time:",
                        VRCFuryEditorUtils.Prop(prop.FindPropertyRelative(timeIn))
                    ));
                    single.Add(MakeTabbed(
                        "Then do this until turned off:",
                        VRCFuryStateEditor.render(prop.FindPropertyRelative(state))
                    ));
                    single.Add(MakeTabbed(
                        "Then blend for this much time:",
                        VRCFuryEditorUtils.Prop(prop.FindPropertyRelative(timeOut))
                    ));
                    var cout = new VisualElement();
                    cout.Add(VRCFuryEditorUtils.Prop(prop.FindPropertyRelative("simpleOutTransition"), "Transition Out is reverse of Transition In"));
                    if (!simpleOutTransitionProp.boolValue)
                        cout.Add(VRCFuryEditorUtils.Prop(prop.FindPropertyRelative(tout)));
                    single.Add(MakeTabbed("Then play this transition out:", cout));
                } else {
                    single.Add(VRCFuryStateEditor.render(prop.FindPropertyRelative(state)));
                }
                return single;
            }

            var remoteSingle = MakeSingle("transitionStateIn", "state", "transitionStateOut", "transitionTimeIn", "transitionTimeOut");
            var c = new VisualElement();
            if (separateLocalProp.boolValue) {
                var localSingle = MakeSingle("localTransitionStateIn", "localState", "localTransitionStateOut", "localTransitionTimeIn", "localTransitionTimeOut");
                c.Add(MakeTabbed("In local:", localSingle));
                c.Add(MakeTabbed("In remote:", remoteSingle));
            } else {
                c = remoteSingle;
            }

            return MakeTabbed("When toggle is enabled:", c);
        }, separateLocalProp, hasTransitionProp, simpleOutTransitionProp));

        content.Add(VRCFuryEditorUtils.RefreshOnChange(() => {
            if (sliderProp.boolValue) {
                var sliderOptions = new VisualElement();
                sliderOptions.Add(VRCFuryEditorUtils.Prop(prop.FindPropertyRelative("defaultSliderValue"), "Default value"));
                sliderOptions.Add(VRCFuryEditorUtils.Prop(prop.FindPropertyRelative("sliderInactiveAtZero"), "Zero is 'Off' (Advanced)", tooltip: "" +
                    "When checked, the toggle will be considered to be entirely 'off' when the slider is at 0, meaning that NOTHING will be animated at 0." +
                    " It is unusual to check this, but is required if you want this slider to interact with Exclusive Tags or transitions."));
                return MakeTabbed("This toggle is a slider", sliderOptions);
            }
            return new VisualElement();
        }, sliderProp));

        // Tags
        content.Add(VRCFuryEditorUtils.RefreshOnChange(() => {
                var tags = new List<string>();
                if (savedProp.boolValue)
                    tags.Add("Saved");
                if (securityEnabledProp.boolValue)
                    tags.Add("Security");
                if (!sliderProp.boolValue) {
                    if (defaultOnProp.boolValue)
                        tags.Add("Default On");
                    if (holdButtonProp.boolValue)
                        tags.Add("Hold Button");
                    if (hasExitTimeProp.boolValue)
                        tags.Add("Run to Completion");
                }
                if (invertRestLogicProp.boolValue)
                    tags.Add((defaultOnProp.boolValue || sliderProp.boolValue) ? "Hide when animator disabled" : "Show when animator disabled");
                if (exclusiveOffStateProp.boolValue)
                    tags.Add("This is the Exclusive Off State");

                var row = new VisualElement().Row().FlexWrap();
                foreach (var tag in tags) {
                    var flag = new Label(tag).Padding(2,4);
                    flag.style.width = StyleKeyword.Auto;
                    flag.style.backgroundColor = new Color(1f, 1f, 1f, 0.1f);
                    flag.style.borderTopRightRadius = 5;
                    flag.style.marginRight = 5;
                    row.Add(flag);
                }

                return row;
            },
            savedProp,
            securityEnabledProp,
            defaultOnProp,
            invertRestLogicProp,
            exclusiveOffStateProp,
            holdButtonProp,
            hasExitTimeProp,
            sliderProp
        ));

        var toggleOffWarning = VRCFuryEditorUtils.Error(
            "You cannot use Turn Off for an object that another Toggle Turns On! Turn Off should only be used for objects which are not controlled by their own toggle.\n\n" +
            "1. You do not need a dedicated 'Turn Off' toggle. Turning off the other toggle will turn off the object.\n\n" +
            "2. If you want this toggle to turn off the other toggle when activated, use Exclusive Tags instead (in the options on the top right).");
        toggleOffWarning.SetVisible(false);
        content.Add(toggleOffWarning);
        VRCFuryEditorUtils.RefreshOnInterval(toggleOffWarning, () => {
            var baseObject = avatarObject != null ? avatarObject : featureBaseObject.root;

            var turnsOff = model.state.actions
                .OfType<ObjectToggleAction>()
                .Where(a => a.mode == ObjectToggleAction.Mode.TurnOff)
                .Select(a => a.obj)
                .Where(o => o != null)
                .ToImmutableHashSet();
            var othersTurnOn = baseObject.GetComponentsInSelfAndChildren<VRCFury>()
                .SelectMany(vf => vf.GetAllFeatures())
                .OfType<Toggle>()
                .SelectMany(toggle => toggle.state.actions)
                .OfType<ObjectToggleAction>()
                .Where(a => a.mode == ObjectToggleAction.Mode.TurnOn)
                .Select(a => a.obj)
                .Where(o => o != null)
                .ToImmutableHashSet();
            var overlap = turnsOff.Intersect(othersTurnOn);
            toggleOffWarning.SetVisible(overlap.Count > 0);
        });

        return content;
    }

    private VisualElement MakeTabbed(string label, VisualElement child) {
        var output = new VisualElement();
        output.Add(VRCFuryEditorUtils.WrappedLabel(label).Bold());
        var tabbed = new VisualElement { style = { paddingLeft = 10 } };
        tabbed.Add(child);
        output.Add(tabbed);
        return output;
    }
}

}


