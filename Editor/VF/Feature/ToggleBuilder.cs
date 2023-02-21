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
    private List<VFAState> exclusiveTagTriggeringStates = new List<VFAState>();
    private VFABool param;
    private AnimationClip restingClip;

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

    [FeatureBuilderAction]
    public void Apply() {
        // If the toggle is setup to /actually/ toggle something (and it's not an off state for just an exclusive tag or something)
        // Then don't even bother adding it. The user probably removed the object, so the toggle shouldn't be present.
        // Toggle should still be added if it drives a global variable
        if (model.state.IsEmpty() && model.state.actions.Count > 0 && !model.enableDriveGlobalParam) {
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

        var layerName = string.IsNullOrWhiteSpace(model.name) ? model.drivingParam : model.name;
        var fx = GetFx();
        var layer = fx.NewLayer(layerName);
        var off = layer.NewState("Off");

        VFACondition onCase;
        var paramName = model.paramOverride ?? model.name;
        if (model.useInt) {
            var numParam = fx.NewInt(paramName, synced: true, saved: model.saved, def: model.defaultOn ? 1 : 0, usePrefix: model.usePrefixOnParam);
            onCase = numParam.IsNotEqualTo(0);
        } else {
            if (model.isParamDriven) {
                model.usePrefixOnParam = false;
            }
            var boolParam = fx.NewBool(paramName, synced: !string.IsNullOrWhiteSpace(model.name), saved: model.saved, def: model.defaultOn, usePrefix: model.usePrefixOnParam);
            param = boolParam;
            onCase = param.IsTrue();
        }
        
        if (model.separateLocal) {
            var isLocal = fx.IsLocal().IsTrue();
            Apply(fx, layer, off, onCase.And(isLocal.Not()), "On Remote", model.state, model.transitionStateIn, model.transitionStateOut, physBoneResetter);
            Apply(fx, layer, off, onCase.And(isLocal), "On Local", model.localState, model.localTransitionStateIn, model.localTransitionStateOut, physBoneResetter);
        } else {
            Apply(fx, layer, off, onCase, "On", model.state, model.transitionStateIn, model.transitionStateOut, physBoneResetter);
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
        ControllerManager fx,
        VFALayer layer,
        VFAState off,
        VFACondition onCase,
        string onName,
        State action,
        State inAction,
        State outAction,
        VFABool physBoneResetter
    ) {
        var clip = LoadState(model.name + " " + onName, action);

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
        if (model.hasTransition && inAction != null && !inAction.IsEmpty()) {
            var transitionClipIn = LoadState(model.name + onName + " In", inAction);
            inState = layer.NewState(onName + " In").WithAnimation(transitionClipIn);
            onState = layer.NewState(onName).WithAnimation(clip);
            inState.TransitionsTo(onState).When().WithTransitionExitTime(1);
        } else {
            inState = onState = layer.NewState(onName).WithAnimation(clip);
        }
        exclusiveTagTriggeringStates.Add(inState);
        off.TransitionsTo(inState).When(onCase);

        if (model.simpleOutTransition) outAction = inAction;
        if (model.hasTransition && outAction != null && !outAction.IsEmpty()) {
            var transitionClipOut = LoadState(model.name + onName + " Out", outAction);
            var outState = layer.NewState(onName + " Out").WithAnimation(transitionClipOut).Speed(model.simpleOutTransition ? -1 : 1);
            onState.TransitionsTo(outState).When(onCase.Not());
            outState.TransitionsToExit().When().WithTransitionExitTime(1);
        } else {
            onState.TransitionsToExit().When(onCase.Not());
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
                if (!model.keepGlobalParam)
                    off.Drives(driveGlobal, false);
                inState.Drives(driveGlobal, true);
            }
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

    /**
     * This method is needed, because:
     * 1. If you clip.SampleAnimation on the avatar while it has a humanoid Avatar set on its Animator, it'll
     *    bake into motorcycle pose.
     * 2. If you change the avatar or controller on the Animator, the Animator will reset all transforms of all
     *    children objects back to the way they were at the start of the frame.
     * Only destroying the animator then recreating it seems to "reset" this "start of frame" state.
     */
    public static void WithoutAnimator(GameObject obj, System.Action func) {
        var animator = obj.GetComponent<Animator>();
        if (!animator) {
            func();
            return;
        }

        var controller = animator.runtimeAnimatorController;
        var avatar = animator.avatar;
        var applyRootMotion = animator.applyRootMotion;
        var updateMode = animator.updateMode;
        var cullingMode = animator.cullingMode;
        Object.DestroyImmediate(animator);
        animator = obj.AddComponent<Animator>();
        animator.applyRootMotion = applyRootMotion;
        animator.updateMode = updateMode;
        animator.cullingMode = cullingMode;
        func();
        animator.runtimeAnimatorController = controller;
        animator.avatar = avatar;
    }

    [FeatureBuilderAction(FeatureOrder.ApplyToggleRestingState)]
    public void ApplyRestingState() {
        if (restingClip != null) {
            WithoutAnimator(avatarObject, () => { restingClip.SampleAnimation(avatarObject, 0); });
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
        var isParamDrivenProp = prop.FindPropertyRelative("isParamDriven");

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

            advMenu.AddItem(new GUIContent("Parameter Driven"), isParamDrivenProp.boolValue, () => {
                    isParamDrivenProp.boolValue = !isParamDrivenProp.boolValue;
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

        content.Add(VRCFuryEditorUtils.RefreshOnChange(() => {
            var c = new VisualElement();
            if (isParamDrivenProp.boolValue)
            {
                c.Add(VRCFuryEditorUtils.Prop(prop.FindPropertyRelative("drivingParam"), "Driving Param"));
            }
            return c;
        }, isParamDrivenProp));

        if (enableDriveGlobalParamProp != null) {
            content.Add(VRCFuryEditorUtils.RefreshOnChange(() => {
                var c = new VisualElement();
                if (enableDriveGlobalParamProp.boolValue) {
                    c.Add(VRCFuryEditorUtils.Prop(prop.FindPropertyRelative("driveGlobalParam"), "Drive Global Param"));
                    c.Add(VRCFuryEditorUtils.Prop(prop.FindPropertyRelative("keepGlobalParam"), "Keep Global Param"));
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


