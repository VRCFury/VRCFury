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
using EmoteManager = VF.Model.Feature.EmoteManager;
using VRC.SDK3.Avatars.Components;
using static VF.Model.Feature.EmoteManager;
using VF.Builder.Exceptions;
using static VRC.SDKBase.VRC_PlayableLayerControl;
using UnityEditor.Animations;

namespace VF.Builder {

/**
 * Manages emotes via the action layer and VRCEmote
 */
public class EmoteManagerBuilder : FeatureBuilder<EmoteManager> {

    [FeatureBuilderAction]
    public void Apply() {

        var actionLayer = GetAction();

        if (model.standingState.actions.Count() == 0){
            var clip = (AnimationClip) AssetDatabase.LoadAssetAtPath("Packages/com.vrchat.avatars/Samples/AV3 Demo Assets/Animation/ProxyAnim/proxy_stand_still.anim", typeof(AnimationClip));
                var action = new AnimationClipAction {
                    clip = clip
                };
                model.standingState.actions.Add(action);
        }

        if (model.sittingState.actions.Count() == 0){
            var clip = (AnimationClip) AssetDatabase.LoadAssetAtPath("Packages/com.vrchat.avatars/Samples/AV3 Demo Assets/Animation/ProxyAnim/proxy_sit.anim", typeof(AnimationClip));
                var action = new AnimationClipAction {
                    clip = clip
                };
                model.sittingState.actions.Add(action);
        }

        if (model.afkState.actions.Count() == 0){
            var clip = (AnimationClip) AssetDatabase.LoadAssetAtPath("Packages/com.vrchat.avatars/Samples/AV3 Demo Assets/Animation/ProxyAnim/proxy_afk.anim", typeof(AnimationClip));
                var action = new AnimationClipAction {
                    clip = clip
                };
                model.afkState.actions.Add(action);
        }
        AnimatorStateMachine baseStateMachine = null;

        var standingAnimation = LoadState("standing", model.standingState, true);
        var sittingAnimation = LoadState("sitting", model.sittingState, true);
        var afkAnimation = LoadState("afk", model.afkState, true);

        if (this == allBuildersInRun.OfType<EmoteManagerBuilder>().First()){

            var baseLayer = actionLayer.NewLayer("Emote Base");

            baseStateMachine = baseLayer.GetRawStateMachine();

            var standingState = baseLayer.NewState("Standing").WithAnimation(standingAnimation);
            var sittingState = baseLayer.NewState("Sitting").WithAnimation(standingAnimation);

            var sittingParam = actionLayer.Seated();

            standingState.TransitionsFromAny().When(sittingParam.IsFalse());
            sittingState.TransitionsFromAny().When(sittingParam.IsTrue());

            var t = new Model.Feature.Toggle {
                paramOverride = "AFK",
                usePrefixOnParam = false,
                isEmote = true,
                sittingEmote = false,
                passiveAction = afkAnimation,
                name = "AFK",
                addMenuItem = false,
                state = model.afkState,
                hasTransition = true,
                transitionStateIn = null,
                transitionStateOut = null,
                simpleOutTransition = false,
                transitionTime = .5f,
                emoteBaseStateMachine = baseStateMachine
            };

            addOtherFeature(t);
        } else {
            foreach (var layer in actionLayer.GetLayers()) {
                if (layer.name == "Emote Base") {
                    baseStateMachine = layer;
                    break;
                }
            }
        }

        var submenu = model.sittingEmotes.Count() > 0 ? "Standing/" : "";

        foreach (var e in model.standingEmotes) {
            var t = new Model.Feature.Toggle {
                isEmote = true,
                sittingEmote = false,
                passiveAction = standingAnimation,
                name = "Standard Emotes/" + submenu + e.name,
                state = e.emoteAnimation,
                hasTransition = true,
                transitionStateIn = null,
                transitionStateOut = e.resetAnimation,
                enableExclusiveTag = true,
                exclusiveTag = "VF-Managed-Emote",
                isButton = !e.isToggle,
                simpleOutTransition = false,
                transitionTime = .25f,
                exitTime = e.hasExitTime ? e.exitTime : 0,
                enableIcon = e.icon != null,
                icon = e.icon,
                emoteBaseStateMachine = baseStateMachine
            };

            addOtherFeature(t);
        }

        submenu = model.standingEmotes.Count() > 0 ? "Sitting/" : "";

        foreach (var e in model.sittingEmotes) {
            var t = new Model.Feature.Toggle {
                isEmote = true,
                sittingEmote = true,
                passiveAction = sittingAnimation,
                name = "Standard Emotes/" + submenu + e.name,
                state = e.emoteAnimation,
                hasTransition = true,
                transitionStateIn = null,
                transitionStateOut = e.resetAnimation,
                enableExclusiveTag = true,
                exclusiveTag = "VF-Managed-Emote",
                isButton = !e.isToggle,
                simpleOutTransition = false,
                transitionTime = .25f,
                exitTime = e.hasExitTime ? e.exitTime : 0,
                enableIcon = e.icon != null,
                icon = e.icon,
                emoteBaseStateMachine = baseStateMachine
            };
            addOtherFeature(t);
        }
        
    }

    public override string GetEditorTitle() {
        return "Emote Manager";
    }

    public override VisualElement CreateEditor(SerializedProperty prop) {
        var standingStateProp = prop.FindPropertyRelative("standingState");
        var sittingStateProp = prop.FindPropertyRelative("sittingState");
        var afkEmotesProp = prop.FindPropertyRelative("afkState");
        var standingEmotesProp = prop.FindPropertyRelative("standingEmotes");
        var sittingEmotesProp = prop.FindPropertyRelative("sittingEmotes");

        var content = new VisualElement();

        content.Add(VRCFuryStateEditor.render(standingStateProp, "Standing State"));

        content.Add(VRCFuryStateEditor.render(sittingStateProp, "Sitting State:"));

        content.Add(VRCFuryStateEditor.render(afkEmotesProp, "AFK State:"));

        content.Add(VRCFuryEmoteEditor.render(standingEmotesProp, "Standing Emotes"));

        content.Add(VRCFuryEmoteEditor.render(sittingEmotesProp, "Sitting Emotes"));

        content.Add(VRCFuryEditorUtils.RefreshOnChange(() => {
            var row = new VisualElement();
            if (standingEmotesProp.arraySize == 0 && sittingEmotesProp.arraySize == 0) {
                row.Add(VRCFuryEditorUtils.Button("Reset to VRC Defaults", () => {ResetToVRCDefault(prop); }));
            }
            
            return row;
        }, standingEmotesProp, sittingEmotesProp));
        
        return content;
    }

    private static void ResetToVRCDefault (SerializedProperty prop)
    {
        (string, string, bool, bool, string, bool, double, string)[] defaultStanding = {
            ("Wave", "Packages/com.vrchat.avatars/Samples/AV3 Demo Assets/Animation/ProxyAnim/proxy_stand_wave.anim", false, false, "", true, .6, "Packages/com.vrchat.avatars/Samples/AV3 Demo Assets/Expressions Menu/Icons/person_wave.png"),
            ("Clap", "Packages/com.vrchat.avatars/Samples/AV3 Demo Assets/Animation/ProxyAnim/proxy_stand_clap.anim", true, false, "", false, 0, ""),
            ("Point", "Packages/com.vrchat.avatars/Samples/AV3 Demo Assets/Animation/ProxyAnim/proxy_stand_point.anim", false, false, "", true, .75, ""),
            ("Cheer", "Packages/com.vrchat.avatars/Samples/AV3 Demo Assets/Animation/ProxyAnim/proxy_stand_cheer.anim", true, false, "", false, 0, "Packages/com.vrchat.avatars/Samples/AV3 Demo Assets/Expressions Menu/Icons/person_dance.png"),
            ("Dance", "Packages/com.vrchat.avatars/Samples/AV3 Demo Assets/Animation/ProxyAnim/proxy_dance.anim", true, false, "", false, 0, "Packages/com.vrchat.avatars/Samples/AV3 Demo Assets/Expressions Menu/Icons/person_dance.png"),
            ("Backflip", "Packages/com.vrchat.avatars/Samples/AV3 Demo Assets/Animation/ProxyAnim/proxy_backflip.anim", false, false, "", true, .8, ""),
            ("Sad Kick", "Packages/com.vrchat.avatars/Samples/AV3 Demo Assets/Animation/ProxyAnim/proxy_stand_sadkick.anim", false, false, "", true, .75, ""),
            ("Die", "Packages/com.vrchat.avatars/Samples/AV3 Demo Assets/Animation/ProxyAnim/proxy_die.anim", true, true, "Packages/com.vrchat.avatars/Samples/AV3 Demo Assets/Animation/ProxyAnim/proxy_supine_getup.anim", true, .75, "")
        };

        (string, string, bool, bool, string, bool, double, string)[] defaultSitting = {
            ("Seated Raise Hand", "Packages/com.vrchat.avatars/Samples/AV3 Demo Assets/Animation/ProxyAnim/proxy_seated_raise_hand.anim", true, false, "", false, 0, ""),
            ("Seated Clap", "Packages/com.vrchat.avatars/Samples/AV3 Demo Assets/Animation/ProxyAnim/proxy_seated_clap.anim", true, false, "", false, 0, ""),
            ("Seated Point", "Packages/com.vrchat.avatars/Samples/AV3 Demo Assets/Animation/ProxyAnim/proxy_seated_point.anim", false, false, "", true, 1, ""),
            ("Seated Laugh", "Packages/com.vrchat.avatars/Samples/AV3 Demo Assets/Animation/ProxyAnim/proxy_seated_laugh.anim", false, false, "", true, 1, ""),
            ("Seated Drum", "Packages/com.vrchat.avatars/Samples/AV3 Demo Assets/Animation/ProxyAnim/proxy_seated_drum.anim", true, false, "", false, 0, ""),
            ("Seated Shake Fist", "Packages/com.vrchat.avatars/Samples/AV3 Demo Assets/Animation/ProxyAnim/proxy_seated_shake_fist.anim", false, false, "", true, 1, ""),
            ("Seated Disaprove", "Packages/com.vrchat.avatars/Samples/AV3 Demo Assets/Animation/ProxyAnim/proxy_seated_disapprove.anim", false, false, "", true, 1, ""),
            ("Seated Disbelief", "Packages/com.vrchat.avatars/Samples/AV3 Demo Assets/Animation/ProxyAnim/proxy_seated_disbelief.anim", false, false, "", true , 1, "")
        };


        foreach (var e in defaultStanding) {
            var emote = new Emote(e.Item1, e.Item2, e.Item3, e.Item4, e.Item5, e.Item6, e.Item7, e.Item8);
            VRCFuryEditorUtils.AddToList(prop.FindPropertyRelative("standingEmotes"), entry => entry.managedReferenceValue = emote);
        }

        foreach (var e in defaultSitting) {
            var emote = new Emote(e.Item1, e.Item2, e.Item3, e.Item4, e.Item5, e.Item6, e.Item7, e.Item8);
            VRCFuryEditorUtils.AddToList(prop.FindPropertyRelative("sittingEmotes"), entry => entry.managedReferenceValue = emote);
        }
    }
}

}
