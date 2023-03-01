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
using VF.Builder.Exceptions;
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
            var clipPath = AssetDatabase.GUIDToAssetPath("91e5518865a04934b82b8aba11398609");
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);
                var action = new AnimationClipAction {
                    clip = clip
                };
                model.standingState.actions.Add(action);
        }

        if (model.sittingState.actions.Count() == 0){
            var clipPath = AssetDatabase.GUIDToAssetPath("970f39cfa8501c741b71ad9eefeeb83d");
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);
                var action = new AnimationClipAction {
                    clip = clip
                };
                model.sittingState.actions.Add(action);
        }

        if (model.afkState.actions.Count() == 0){
            var clipPath = AssetDatabase.GUIDToAssetPath("806c242c97b686d4bac4ad50defd1fdb");
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);
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
            ("Wave", "60873c431a64a744d87a5ad1e20bf886", false, false, "", true, .6, "011229dd2f6f5f64f8965a08d3434654"),
            ("Clap", "44ce16481749f4c4baf0549d1bf3b3f3", true, false, "", false, 0, ""),
            ("Point", "498e9dfd6d870064184180c5e4a3fc59", false, false, "", true, .75, ""),
            ("Cheer", "7359fa5b13647ba4986416b105f0d6dd", true, false, "", false, 0, "9a20b3a6641e1af4e95e058f361790cbg"),
            ("Dance", "0d2e5f9cc00d88a48b7bbe6e2898a4b4", true, false, "", false, 0, "9a20b3a6641e1af4e95e058f361790cb"),
            ("Backflip", "2af7e07b1514ac14bafe50d6b79cd07e", false, false, "", true, .8, ""),
            ("Sad Kick", "762c2cb22a9e6cc45803bd200a00c634", false, false, "", true, .75, ""),
            ("Die", "4cf06429686164a45adaedb6a6e520a5", true, true, "ef56f98d2522d6b4387a112b015c6478", true, .75, "")
        };

        (string, string, bool, bool, string, bool, double, string)[] defaultSitting = {
            ("Seated Raise Hand", "1791a673b68e05943baa8b96f0d44bd7", true, false, "", false, 0, ""),
            ("Seated Clap", "390816a8c9a0e634c8eb94e9907a8a81", true, false, "", false, 0, ""),
            ("Seated Point", "f7da25fc68cda2748bf78e7ed01e28a4", false, false, "", true, 1, ""),
            ("Seated Laugh", "b405e069574439846861d02dc0b5ee62", false, false, "", true, 1, ""),
            ("Seated Drum", "3aa84c817614d9a4e83d0250b9ac214e", true, false, "", false, 0, ""),
            ("Seated Shake Fist", "fda92038a2576ec43ad296fc2b6528f6", false, false, "", true, 1, ""),
            ("Seated Disaprove", "593e00f8a0060b14ea6b289eb12f0db1", false, false, "", true, 1, ""),
            ("Seated Disbelief", "385699e4f9531f8468264ffc7c48d9ed", false, false, "", true , 1, "")
        };


        foreach (var e in defaultStanding) {
            var emote = new EmoteManager.Emote(e.Item1, e.Item2, e.Item3, e.Item4, e.Item5, e.Item6, e.Item7, e.Item8);
            VRCFuryEditorUtils.AddToList(prop.FindPropertyRelative("standingEmotes"), entry => entry.managedReferenceValue = emote);
        }

        foreach (var e in defaultSitting) {
            var emote = new EmoteManager.Emote(e.Item1, e.Item2, e.Item3, e.Item4, e.Item5, e.Item6, e.Item7, e.Item8);
            VRCFuryEditorUtils.AddToList(prop.FindPropertyRelative("sittingEmotes"), entry => entry.managedReferenceValue = emote);
        }
    }
}

}
