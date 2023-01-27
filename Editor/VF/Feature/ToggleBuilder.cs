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
using VF.Model.Feature;
using VF.Model.StateAction;
using Object = UnityEngine.Object;
using Toggle = VF.Model.Feature.Toggle;

namespace VF.Feature {

public class ToggleBuilder : FeatureBuilder<Toggle> {
    private VFAState onState;
    private VFAState onStateLocal;
    private VFAState transitionState;
    private VFAState localTransitionState;
    private VFABool param;
    private AnimationClip clip;

    public ISet<string> GetExclusiveTags() {
        if (model.enableExclusiveTag) {
            return model.exclusiveTag.Split(',')
                .Select(tag => tag.Trim())
                .Where(tag => !string.IsNullOrWhiteSpace(tag))
                .ToImmutableHashSet();
        }
        return new HashSet<string>();
    }
    public VFABool GetParam() {
        return param;
    }

    [FeatureBuilderAction]
    public void Apply() {
        // If the toggle is setup to /actually/ toggle something (and it's not an off state for just an exclusive tag or something)
        // Then don't even bother adding it. The user probably removed the object, so the toggle shouldn't be present.
        if (model.state.IsEmpty() && model.state.actions.Count > 0) {
            return;
        }
        
        if (model.slider) {
            var stops = new List<Puppet.Stop> {
                new Puppet.Stop(1,0,model.state)
            };
            var puppet = new Puppet {
                name = model.name,
                saved = model.saved,
                slider = true,
                stops = stops,
                defaultX = model.slider && model.defaultOn ? model.defaultSliderValue : 0
            };
            addOtherFeature(puppet);
            return;
        }

        var physBoneResetter = CreatePhysBoneResetter(model.resetPhysbones, model.name);

        var layerName = model.name;
        var fx = GetFx();
        var layer = fx.NewLayer(layerName);
        clip = LoadState(model.name, model.state);
        var off = layer.NewState("Off");
        var on = layer.NewState("On").WithAnimation(clip);
        onState = on;
        VFACondition onCase;
        VFACondition isLocal = fx.IsLocal().IsTrue();

        if (model.useInt) {
            var numParam = fx.NewInt(model.name, synced: true, saved: model.saved, def: model.defaultOn ? 1 : 0, usePrefix: model.usePrefixOnParam);
            onCase = numParam.IsNotEqualTo(0);
        } else {
            var boolParam = fx.NewBool(model.name, synced: true, saved: model.saved, def: model.defaultOn, usePrefix: model.usePrefixOnParam);
            param = boolParam;
            onCase = boolParam.IsTrue();
        }

        var securityLockUnlocked = allBuildersInRun
            .Select(f => f as SecurityLockBuilder)
            .Where(f => f != null)
            .Select(f => f.GetEnabled())
            .FirstOrDefault();

        if (model.includeInRest) {
            var defaultsManager = allBuildersInRun
                .OfType<FixWriteDefaultsBuilder>()
                .First();
            defaultsManager.forceRecordBindings.UnionWith(AnimationUtility.GetCurveBindings(clip));
            defaultsManager.forceRecordBindings.UnionWith(AnimationUtility.GetObjectReferenceCurveBindings(clip));
        }

        if (model.securityEnabled && securityLockUnlocked != null) {
            onCase = onCase.And(securityLockUnlocked);
        }

        var transitionToOn = off.TransitionsTo(on).When(onCase);
        var transitionToOff = on.TransitionsTo(off).When(onCase.Not());
         
        if (model.separateLocal) {
            AnimationClip localClip = LoadState(model.name + " Local", model.localState);
            var onLocal = layer.NewState("On Local").WithAnimation(localClip).Move(off,0,-1);
            onStateLocal = onLocal;
            transitionToOn.AddCondition(isLocal.Not());
            off.TransitionsTo(onLocal).When(isLocal.And(onCase));
            
            if (!model.hasTransition) 
                onLocal.TransitionsTo(off).When(onCase.Not());
        }

        if (model.hasTransition) {
            AnimationClip transitionClipIn = LoadState(model.name + " In", model.transitionStateIn);
            AnimationClip transitionClipOut = LoadState(model.name + " Out", model.transitionStateOut);
            var simple = model.simpleOutTransition;
            var transitionIn = layer.NewState("Transition In").WithAnimation(transitionClipIn);
            var transitionOut = layer.NewState("Transition Out").WithAnimation(simple ? transitionClipIn : transitionClipOut).Speed(simple ? -1 : 1);

            transitionState = transitionIn;

            on.RemoveTransitions();
            off.RemoveTransitions();

            var transitionToTransition = off.TransitionsTo(transitionIn).When(onCase);
            transitionIn.TransitionsTo(on).When().WithTransitionExitTime(1);
            on.TransitionsTo(transitionOut).When(onCase.Not());
            transitionOut.TransitionsToExit().When().WithTransitionExitTime(1);

            transitionIn.Move(off,0,1);
            on.Move(transitionIn,1,0);
            transitionOut.Move(on,1,0);

            if (model.separateLocal)
                transitionToTransition.AddCondition(isLocal.Not());
                
        }

        if (model.separateLocal && model.hasTransition) {
            AnimationClip localTransitionClipIn = LoadState(model.name + " Local In", model.localTransitionStateIn);
            AnimationClip localTransitionClipOut = LoadState(model.name + " Local Out", model.localTransitionStateOut);
            var simple = model.simpleOutTransition;
            var localTransitionIn = layer.NewState("Local Transition In").WithAnimation(localTransitionClipIn);
            var localTransitionOut = layer.NewState("Local Transition Out").WithAnimation(simple ? localTransitionClipIn : localTransitionClipOut).Speed(simple ? -1 : 1);

            localTransitionState = localTransitionIn;

            off.TransitionsTo(localTransitionIn).When(isLocal.And(onCase));
            localTransitionIn.TransitionsTo(onStateLocal).When().WithTransitionExitTime(1);
            onStateLocal.TransitionsTo(localTransitionOut).When(onCase.Not());
            localTransitionOut.TransitionsToExit().When().WithTransitionExitTime(1);

            localTransitionIn.Move(off,0,-1);
            onStateLocal.Move(localTransitionIn,1,0);
            localTransitionOut.Move(onStateLocal,1,0);
        }
        
        if (physBoneResetter != null) {
            off.Drives(physBoneResetter, true);
            on.Drives(physBoneResetter, true);
            if (model.separateLocal)
                onStateLocal.Drives(physBoneResetter, true);
            if (model.hasTransition)
                transitionState.Drives(physBoneResetter, true);
            if (model.separateLocal && model.hasTransition)
                localTransitionState.Drives(physBoneResetter, true);
        }

        if (model.enableDriveGlobalParam != null && !string.IsNullOrWhiteSpace(model.driveGlobalParam)) {
            var driveGlobal = fx.NewBool(
                model.driveGlobalParam,
                synced: false,
                saved: false,
                def: false,
                usePrefix: false
            );
            off.Drives(driveGlobal, false);
            on.Drives(driveGlobal, true);
            if (model.separateLocal)
                onStateLocal.Drives(driveGlobal, true);
            if (model.hasTransition)
                transitionState.Drives(driveGlobal, true);
            if (model.separateLocal && model.hasTransition)
                localTransitionState.Drives(driveGlobal, true);
        
        }

        if (model.addMenuItem) {
            manager.GetMenu().NewMenuToggle(
                model.name,
                param,
                icon: model.enableIcon ? model.icon : null
            );
        }
    }

     [FeatureBuilderAction(FeatureOrder.CollectToggleExclusiveTags)]
    public void ApplyExclusiveTags () {
        ApplyExclusiveTagsInternal(onState);
        ApplyExclusiveTagsInternal(onStateLocal);
    }
    public void ApplyExclusiveTagsInternal(VFAState state) {
        if (state == null) return;
        
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
                    state.Drives(otherParam, false);
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
    private static void WithoutAnimator(GameObject obj, System.Action func) {
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
        if (onState == null) return;
        if (!model.includeInRest) return;
        WithoutAnimator(avatarObject, () => {
            clip.SampleAnimation(avatarObject, 0);
        });
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
                defaultSliderProp.floatValue = 1;
            } else {
                defaultSliderProp.floatValue = 0;
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

        if (separateLocalProp != null)
        {
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
        }

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
        }, simpleOutTransitionProp));

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
            separateLocalProp
            ));

        return content;
    }
}

}


