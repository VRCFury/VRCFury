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
        param = fx.NewBool(model.name, synced: true, saved: model.saved, def: model.defaultOn, usePrefix: model.usePrefixOnParam);
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

        var onCase = param.IsTrue();

        if (model.securityEnabled && securityLockUnlocked != null) {
            onCase = onCase.And(securityLockUnlocked);
        }

        off.TransitionsTo(on).When(onCase);
        on.TransitionsTo(off).When(onCase.Not());

        if (physBoneResetter != null) {
            off.Drives(physBoneResetter, true);
            on.Drives(physBoneResetter, true);
        }

        if (model.addMenuItem) {
            manager.GetMenu().NewMenuToggle(model.name, param);
        }
    }

    [FeatureBuilderAction(FeatureOrder.CollectToggleExclusiveTags)]
    public void ApplyExclusiveTags() {
        if (onState == null) return;

        var myTags = GetExclusiveTags();
        foreach (var other in allBuildersInRun
                     .OfType<ToggleBuilder>()
                     .Where(b => b != this)) {
            var otherTags = other.GetExclusiveTags();
            var conflictsWithOther = myTags.Any(myTag => otherTags.Contains(myTag));
            if (conflictsWithOther) {
                var otherParam = other.GetParam();
                if (otherParam != null) {
                    onState.Drives(other.GetParam(), false);
                }
            }
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
        var enableExclusiveTagProp = prop.FindPropertyRelative("enableExclusiveTag");
        var resetPhysboneProp = prop.FindPropertyRelative("resetPhysbones");

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

        var button = VRCFuryEditorUtils.Button("*", () => {
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

            var row = new VisualElement();
            row.style.flexWrap = Wrap.Wrap;
            row.style.flexDirection = FlexDirection.Row;
            foreach (var tag in tags) {
                var flag = new Label(tag);
                flag.style.width = StyleKeyword.Auto;
                flag.style.backgroundColor = new Color(1f, 1f, 1f, 0.1f);
                flag.style.borderTopRightRadius = 5;
                VRCFuryEditorUtils.Padding(flag, 2, 4);
                row.Add(flag);
            }

            return row;
        },
            savedProp,
            sliderProp,
            securityEnabledProp,
            defaultOnProp,
            includeInRestProp
        ));

        return content;
    }
}

}


