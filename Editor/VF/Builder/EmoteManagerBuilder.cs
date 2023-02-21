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

namespace VF.Builder {

/**
 * Manages emotes via the action layer and VRCEmote
 */
public class EmoteManagerBuilder : FeatureBuilder<EmoteManager> {

    [FeatureBuilderAction]
    public void Apply() {

        if (this != allBuildersInRun.OfType<EmoteManagerBuilder>().First()) return ; // only the first Emote Manager will be used

        var fxLayer = GetFx();
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

        List<int> reservedNumbers = new List<int>();

        foreach (var e in model.standingEmotes) {
            if (e.number != 0) {
                if (reservedNumbers.Contains(e.number)) {
                    throw new VRCFBuilderException("No 2 emotes can share a number");
                }
                reservedNumbers.Add(e.number);
            }
            e.emoteClip = LoadState(e.name, e.emoteAnimation);
            if (e.hasReset) 
                e.resetClip = LoadState(e.name + " Reset", e.resetAnimation);
        }

        foreach (var e in model.sittingEmotes) {
            if (e.number != 0) {
                if (reservedNumbers.Contains(e.number)) {
                    throw new VRCFBuilderException("No 2 emotes can share a number");
                }
                reservedNumbers.Add(e.number);
               
            } 
            e.emoteClip = LoadState(e.name, e.emoteAnimation);
            if (e.hasReset) 
                e.resetClip = LoadState(e.name + " Reset", e.resetAnimation);
        }

        var nextNumber = 1;

        foreach (var e in model.standingEmotes) {
            if (e.number == 0) {
                while (reservedNumbers.Contains(nextNumber)) {
                    nextNumber++;
                }
                e.number = nextNumber;
                nextNumber++;
            }
        }

        foreach (var e in model.sittingEmotes) {
            if (e.number == 0) {
                while (reservedNumbers.Contains(nextNumber)) {
                    nextNumber++;
                }
                e.number = nextNumber;
                nextNumber++;
            }
        }

        AddClipsToLayer(actionLayer, StripToAction);
        AddClipsToLayer(fxLayer, StripToFX, false);

        var vrcEmote = actionLayer.VRCEmote();

        foreach (var e in model.standingEmotes) {
            if (e.isToggle) {
                manager.GetMenu().NewMenuToggle(
                    "Standard Emotes/" + (model.sittingEmotes.Count > 0 ? "Standing/" : "") + e.name,
                    vrcEmote,
                    e.number,
                    icon: e.icon ? e.icon : null
                );
            } else {
                manager.GetMenu().NewMenuButton(
                    "Standard Emotes/" + (model.sittingEmotes.Count > 0 ? "Standing/" : "") + e.name,
                    vrcEmote,
                    e.number,
                    icon: e.icon ? e.icon : null
                );
            }
        }

        foreach (var e in model.sittingEmotes) {
            if (e.isToggle) {
                manager.GetMenu().NewMenuToggle(
                    "Standard Emotes/" + (model.standingEmotes.Count > 0 ? "Sitting/" : "") + e.name,
                    vrcEmote,
                    e.number,
                    icon: e.icon ? e.icon : null
                );
            } else {
                manager.GetMenu().NewMenuButton(
                    "Standard Emotes/" + (model.standingEmotes.Count > 0 ? "Sitting/" : "") + e.name,
                    vrcEmote,
                    e.number,
                    icon: e.icon ? e.icon : null
                );
            }
        }
    }

    public void AddClipsToLayer(ControllerManager VRClayer, Func<AnimationClip, AnimationClip> stripFunc, bool addWieghtsAndTracking = true) {

        var layerIndex = VRClayer.GetLayers().TakeWhile(l => l.name != "Action").Count();
        if (layerIndex < VRClayer.GetLayers().Count()) {
            VRClayer.RemoveLayer(layerIndex);
        }
        var layer = VRClayer.NewLayer("VRCF EmoteManaged Action");

        var topEmote = model.standingEmotes.Count() / -2;
        var bottomEmote = model.sittingEmotes.Count() / 2;

        var vrcEmote = VRClayer.VRCEmote();

        var standingAnimation = LoadState("standing", model.standingState);
        var sittingAnimation = LoadState("sitting", model.sittingState);
        var afkAnimation = LoadState("afk", model.afkState);

        var sittingCondition = VRClayer.Seated().IsTrue();
        var afkCondition = VRClayer.AFK().IsTrue();

        var start = layer.NewState("WaitForActionOrAFK").WithAnimation(standingAnimation);
        var sit = layer.NewState("Sit").WithAnimation(sittingAnimation);
        var prepareStanding = layer.NewState("Prepare Standing").WithAnimation(standingAnimation);
        var prepareSitting = layer.NewState("Prepare Sitting").WithAnimation(sittingAnimation);
        var afkInit = layer.NewState("Afk Init").WithAnimation(afkAnimation);
        var afk = layer.NewState("AFK").WithAnimation(afkAnimation);
        var afkBlendOut = layer.NewState("BlendOut").WithAnimation(afkAnimation);

        start.TransitionsTo(sit).When(sittingCondition);
        sit.TransitionsTo(start).When(sittingCondition.Not());

        start.TransitionsTo(afkInit).When(afkCondition);
        sit.TransitionsTo(afkInit).When(afkCondition);

        afkInit.TransitionsTo(afk).When().WithTransitionExitTime(0.01f).WithTransitionDurationSeconds(1);
        afk.TransitionsTo(afkBlendOut).When(afkCondition.Not());
        afkBlendOut.TransitionsToExit().When().WithTransitionExitTime(0.2f);

        var standingExit = layer.NewState("BlendOut Stand").WithAnimation(standingAnimation);
        var sittingExit = layer.NewState("BlendOut Sit").WithAnimation(sittingAnimation);

        var standingRestore = layer.NewState("Restore Tracking (stand)").WithAnimation(standingAnimation);
        var sittingRestore = layer.NewState("Restore Tracking (sit)").WithAnimation(sittingAnimation);

        if (addWieghtsAndTracking) {
            prepareStanding.TrackingController("emoteAnimation").PlayableLayerController(BlendableLayer.Action,1,.5f);
            prepareSitting.TrackingController("emoteAnimation").PlayableLayerController(BlendableLayer.Action,1,.25f);
            afkInit.TrackingController("allAnimation").PlayableLayerController(BlendableLayer.Action,1,1);
            afkBlendOut.TrackingController("allTracking").PlayableLayerController(BlendableLayer.Action,0,.5f);
            standingExit.PlayableLayerController(BlendableLayer.Action,0,.25f);
            sittingExit.PlayableLayerController(BlendableLayer.Action,0,.25f);
            standingRestore.TrackingController("emoteTracking");
            sittingRestore.TrackingController("emoteTracking");
        }

        standingExit.TransitionsTo(standingRestore).When().WithTransitionDurationSeconds(.25f).WithTransitionExitTime(1);
        sittingExit.TransitionsTo(sittingRestore).When().WithTransitionDurationSeconds(.25f).WithTransitionExitTime(1);

        standingRestore.TransitionsToExit().When().WithTransitionExitTime(1);
        sittingRestore.TransitionsToExit().When().WithTransitionExitTime(1);

        prepareStanding.Move(start, 2, topEmote);
        sit.Move(start, 1, bottomEmote);
        prepareSitting.Move(sit, 1, 0);
        afkInit.Move(start, 0, model.sittingEmotes.Count() + 2);
        afk.Move(afkInit, 3, 0);
        afkBlendOut.Move(afk, 3, 0);
        standingExit.Move(prepareStanding, 3, 0);
        sittingExit.Move(prepareSitting, 3, 0);
        standingRestore.Move(standingExit, 1, 0);
        sittingRestore.Move(sittingExit, 1, 0);

        layer.MoveExit(start, 7, 0);

        foreach (var e in model.standingEmotes) {
            addEmoteToTree(start, prepareStanding, standingExit, layer, vrcEmote, e, stripFunc, topEmote++);
        }

        foreach (var e in model.sittingEmotes) {
            addEmoteToTree(sit, prepareSitting, sittingExit, layer, vrcEmote, e, stripFunc, bottomEmote--);
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
        (string, string, int, bool, bool, string, bool, double, string)[] defaultStanding = {
            ("Wave", "Packages/com.vrchat.avatars/Samples/AV3 Demo Assets/Animation/ProxyAnim/proxy_stand_wave.anim", 0, false, false, "", true, .6, "Packages/com.vrchat.avatars/Samples/AV3 Demo Assets/Expressions Menu/Icons/person_wave.png"),
            ("Clap", "Packages/com.vrchat.avatars/Samples/AV3 Demo Assets/Animation/ProxyAnim/proxy_stand_clap.anim", 0, true, false, "", false, 0, ""),
            ("Point", "Packages/com.vrchat.avatars/Samples/AV3 Demo Assets/Animation/ProxyAnim/proxy_stand_point.anim", 0, false, false, "", true, .75, ""),
            ("Cheer", "Packages/com.vrchat.avatars/Samples/AV3 Demo Assets/Animation/ProxyAnim/proxy_stand_cheer.anim", 0, true, false, "", false, 0, "Packages/com.vrchat.avatars/Samples/AV3 Demo Assets/Expressions Menu/Icons/person_dance.png"),
            ("Dance", "Packages/com.vrchat.avatars/Samples/AV3 Demo Assets/Animation/ProxyAnim/proxy_dance.anim", 0, true, false, "", false, 0, "Packages/com.vrchat.avatars/Samples/AV3 Demo Assets/Expressions Menu/Icons/person_dance.png"),
            ("Backflip", "Packages/com.vrchat.avatars/Samples/AV3 Demo Assets/Animation/ProxyAnim/proxy_backflip.anim", 0, false, false, "", true, .8, ""),
            ("Sad Kick", "Packages/com.vrchat.avatars/Samples/AV3 Demo Assets/Animation/ProxyAnim/proxy_stand_sadkick.anim", 0, false, false, "", true, .75, ""),
            ("Die", "Packages/com.vrchat.avatars/Samples/AV3 Demo Assets/Animation/ProxyAnim/proxy_die.anim", 0, true, true, "Packages/com.vrchat.avatars/Samples/AV3 Demo Assets/Animation/ProxyAnim/proxy_supine_getup.anim", true, .75, "")
        };

        (string, string, int, bool, bool, string, bool, double, string)[] defaultSitting = {
            ("Seated Raise Hand", "Packages/com.vrchat.avatars/Samples/AV3 Demo Assets/Animation/ProxyAnim/proxy_seated_raise_hand.anim", 0, true, false, "", false, 0, ""),
            ("Seated Clap", "Packages/com.vrchat.avatars/Samples/AV3 Demo Assets/Animation/ProxyAnim/proxy_seated_clap.anim", 0, true, false, "", false, 0, ""),
            ("Seated Point", "Packages/com.vrchat.avatars/Samples/AV3 Demo Assets/Animation/ProxyAnim/proxy_seated_point.anim", 0, false, false, "", true, 1, ""),
            ("Seated Laugh", "Packages/com.vrchat.avatars/Samples/AV3 Demo Assets/Animation/ProxyAnim/proxy_seated_laugh.anim", 0, false, false, "", true, 1, ""),
            ("Seated Drum", "Packages/com.vrchat.avatars/Samples/AV3 Demo Assets/Animation/ProxyAnim/proxy_seated_drum.anim", 0, true, false, "", false, 0, ""),
            ("Seated Shake Fist", "Packages/com.vrchat.avatars/Samples/AV3 Demo Assets/Animation/ProxyAnim/proxy_seated_shake_fist.anim", 0, false, false, "", true, 1, ""),
            ("Seated Disaprove", "Packages/com.vrchat.avatars/Samples/AV3 Demo Assets/Animation/ProxyAnim/proxy_seated_disapprove.anim", 0, false, false, "", true, 1, ""),
            ("Seated Disbelief", "Packages/com.vrchat.avatars/Samples/AV3 Demo Assets/Animation/ProxyAnim/proxy_seated_disbelief.anim", 0, false, false, "", true , 1, "")
        };


        foreach (var e in defaultStanding) {
            var emote = new Emote(e.Item1, e.Item2, e.Item3, e.Item4, e.Item5, e.Item6, e.Item7, e.Item8, e.Item9);
            VRCFuryEditorUtils.AddToList(prop.FindPropertyRelative("standingEmotes"), entry => entry.managedReferenceValue = emote);
        }

        foreach (var e in defaultSitting) {
            var emote = new Emote(e.Item1, e.Item2, e.Item3, e.Item4, e.Item5, e.Item6, e.Item7, e.Item8, e.Item9);
            VRCFuryEditorUtils.AddToList(prop.FindPropertyRelative("sittingEmotes"), entry => entry.managedReferenceValue = emote);
        }
    }

    private void addEmoteToTree(VFAState start, VFAState nexus, VFAState exit, VFALayer layer, VFANumber vrcEmote, Emote emote, Func<AnimationClip, AnimationClip> stripFunc, int position = -1000) {

        var emoteClip = emote.emoteClip;
        var resetClip = emote.resetClip;
        emoteClip = stripFunc(emoteClip);
        resetClip = stripFunc(resetClip);

        if (emoteClip == manager.GetClipStorage().GetNoopClip() && resetClip == manager.GetClipStorage().GetNoopClip()) return;

        var condition = vrcEmote.IsEqualTo(emote.number);
        
        start.TransitionsTo(nexus).When(condition);

        var emoteState = layer.NewState(emote.name).WithAnimation(emoteClip);
        nexus.TransitionsTo(emoteState).When(condition).WithTransitionDurationSeconds(.25f);

        VFAState resetState = null;
        VFATransition exitTranistion = null;

        if (emote.hasReset){
            resetState = layer.NewState(emote.name + " Reset").WithAnimation(resetClip);
            exitTranistion = resetState.TransitionsTo(exit).When().WithTransitionDurationSeconds(.4f);
            if (emote.hasExitTime) {
                exitTranistion.WithTransitionExitTime(emote.exitTime);
            }
        }

        var nextState = emote.hasReset ? resetState : exit;

        if (emote.isToggle || !emote.hasExitTime) {
            exitTranistion = emoteState.TransitionsTo(nextState).When(condition.Not()).WithTransitionDurationSeconds(.25f);
        } else {
            exitTranistion = emoteState.TransitionsTo(nextState).When().WithTransitionDurationSeconds(.25f);
        }

        if (emote.hasExitTime && !emote.hasReset) {
            exitTranistion.WithTransitionExitTime(emote.exitTime);
        }

        if (position != -1000) {
            emoteState.Move(nexus, 1, position);
            if (emote.hasReset) {
                resetState.Move(emoteState, 1, 0);
            }
        }
    }
}

}
