using System;
using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;
using System.Collections.Generic;
using VRC.SDK3.Avatars.Components;
using VRCF.Model;
using System.IO;
using VRC.SDK3.Avatars.ScriptableObjects;

namespace VRCF.Builder {

public class VRCFuryBuilder {
    public bool SafeRun(VRCFury config, GameObject avatarObject) {
        EditorUtility.DisplayProgressBar("VRCFury is building ...", "", 0.5f);
        bool result;
        try {
            result = Run(config, avatarObject);
        } catch(Exception e) {
            result = false;
            Debug.LogException(e);
            EditorUtility.DisplayDialog("VRCFury Error", "An exception was thrown by VRCFury. Check the unity console.", "Ok");
        }

        AssetDatabase.SaveAssets();
        EditorUtility.ClearProgressBar();
        return result;
    }

    private bool Run(VRCFury config, GameObject avatarObject) {
        this.avatarObject = avatarObject;

        Debug.Log("VRCFury is running for " + avatarObject.name + "...");
        var avatar = avatarObject.GetComponent(typeof(VRCAvatarDescriptor)) as VRCAvatarDescriptor;
        var fxLayer = avatar.baseAnimationLayers[4];
        Action saveFxLayer = () => { avatar.baseAnimationLayers[4] = fxLayer; };

        // Unhook everything from our assets before we delete them
        var animator = avatarObject.GetComponent<Animator>();
        if (animator != null) {
            if (IsVrcfAsset(animator.runtimeAnimatorController)) {
                animator.runtimeAnimatorController = null;
            }
        }
        AnimatorController fxController = null;
        if (IsVrcfAsset(fxLayer.animatorController)) {
            fxLayer.animatorController = null;
            saveFxLayer();
        } else if (avatar.customizeAnimationLayers && !fxLayer.isDefault && fxLayer.animatorController != null) {
            fxController = (AnimatorController)fxLayer.animatorController;
            VRCFuryNameManager.PurgeFromAnimator(fxController);
        }
        VRCExpressionsMenu menu = null;
        if (IsVrcfAsset(avatar.expressionsMenu)) {
            avatar.expressionsMenu = null;
        } else if (avatar.customExpressions && avatar.expressionsMenu != null) {
            menu = avatar.expressionsMenu;
            VRCFuryNameManager.PurgeFromMenu(menu);
        }
        VRCExpressionParameters syncedParams = null;
        if (IsVrcfAsset(avatar.expressionParameters)) {
            avatar.expressionParameters = null;
        } else if (avatar.customExpressions && avatar.expressionParameters != null) {
            syncedParams = avatar.expressionParameters;
            VRCFuryNameManager.PurgeFromParams(syncedParams);
        }

        // Nuke all our old generated assets
        var avatarPath = avatarObject.scene.path;
        if (string.IsNullOrEmpty(avatarPath)) {
            EditorUtility.DisplayDialog("VRCFury Error", "Failed to find file path to avatar scene", "Ok");
            return false;
        }
        var tmpDir = Path.GetDirectoryName(avatarPath) + "/_VRCFury/" + avatarObject.name;
        if (Directory.Exists(tmpDir)) {
            foreach (var asset in AssetDatabase.FindAssets("", new string[] { tmpDir })) {
                var path = AssetDatabase.GUIDToAssetPath(asset);
                AssetDatabase.DeleteAsset(path);
            }
        }
        Directory.CreateDirectory(tmpDir);

        var thirdPartyIntegrations = false;
        if (fxController == null) {
            fxController = AnimatorController.CreateAnimatorControllerAtPath(tmpDir + "/VRCFury for " + avatarObject.name + ".controller");
            avatar.customizeAnimationLayers = true;
            fxLayer.isDefault = false;
            fxLayer.type = VRCAvatarDescriptor.AnimLayerType.FX;
            fxLayer.animatorController = fxController;
            saveFxLayer();
            if (animator != null) animator.runtimeAnimatorController = fxController;
            thirdPartyIntegrations = true;
        }
        var useMenuRoot = false;
        if (menu == null) {
            useMenuRoot = true;
            avatar.customExpressions = true;
            menu = avatar.expressionsMenu = ScriptableObject.CreateInstance<VRCExpressionsMenu>();
            menu.controls = new List<VRCExpressionsMenu.Control>();
            AssetDatabase.CreateAsset(menu, tmpDir + "/VRCFury Menu for " + avatarObject.name + ".asset");
        }
        if (syncedParams == null) {
            avatar.customExpressions = true;
            syncedParams = avatar.expressionParameters = ScriptableObject.CreateInstance<VRCExpressionParameters>();
            syncedParams.parameters = new VRCExpressionParameters.Parameter[]{};
            AssetDatabase.CreateAsset(syncedParams, tmpDir + "/VRCFury Params for " + avatarObject.name + ".asset");
        }

        if (thirdPartyIntegrations) {
            VRCFuryTPSIntegration.Run(avatarObject, fxController, tmpDir);
            VRCFuryLensIntegration.Run(avatarObject);
        }

        manager = new VRCFuryNameManager(menu, syncedParams, fxController, tmpDir, useMenuRoot);
        baseFile = AssetDatabase.GetAssetPath(fxController);
        motions = new VRCFuryClipUtils(avatarObject);

        // REMOVE ANIMATORS FROM PREFAB INSTANCES (often used for prop testing)
        foreach (var otherAnimator in avatarObject.GetComponentsInChildren<Animator>(true)) {
            if (otherAnimator.gameObject != avatarObject && PrefabUtility.IsPartOfPrefabInstance(otherAnimator.gameObject)) {
                UnityEngine.Object.DestroyImmediate(otherAnimator);
            }
        }

        // DEFAULTS
        noopClip = manager.GetNoopClip();
        defaultClip = manager.NewClip("Defaults");
        var defaultLayer = manager.NewLayer("Defaults");
        defaultLayer.NewState("Defaults").WithAnimation(defaultClip);

        if (config != null) {
            handleBaseConfig(config);
            handleProps(getAllBaseProps(config), null);
        }
        foreach (var otherConfig in avatarObject.GetComponentsInChildren<VRCFury>(true)) {
            if (otherConfig == config) continue;
            Debug.Log("Importing config from " + otherConfig.gameObject.name);
            handleProps(otherConfig.props.props, otherConfig.gameObject);
        }

        Debug.Log("VRCFury Finished!");

        return true;
    }

    private void handleBaseConfig(VRCFury config) {
        //var paramOrifaceMouthRing = manager.NewBool("OrifaceMouthRing", synced: true);
        //var paramOrifaceMouthHole = manager.NewBool("OrifaceMouthHole", synced: true);

        // VISEMES
        if (config.viseme != null) {
            var visemeFolder = Path.GetDirectoryName(AssetDatabase.GetAssetPath(config.viseme));
            var visemes = manager.NewLayer("Visemes");
            var VisemeParam = manager.NewInt("Viseme", usePrefix: false);
            Action<int, string> addViseme = (index, text) => {
                var animFileName = "Viseme-" + text;
                var clip = AssetDatabase.LoadMainAssetAtPath(visemeFolder + "/" + animFileName + ".anim") as AnimationClip;
                if (clip == null) throw new Exception("Missing animation for viseme " + animFileName);
                var state = visemes.NewState(text).WithAnimation(clip);
                if (text == "sil") state.Move(0, -8);
                state.TransitionsFromEntry().When(VisemeParam.IsEqualTo(index));
                state.TransitionsToExit().When(VisemeParam.IsNotEqualTo(index));
            };
            addViseme(0, "sil");
            addViseme(1, "PP");
            addViseme(2, "FF");
            addViseme(3, "TH");
            addViseme(4, "DD");
            addViseme(5, "kk");
            addViseme(6, "CH");
            addViseme(7, "SS");
            addViseme(8, "nn");
            addViseme(9, "RR");
            addViseme(10, "aa");
            addViseme(11, "E");
            addViseme(12, "I");
            addViseme(13, "O");
            addViseme(14, "U");
        }

        // BLINKING
        VFABool blinkActive = null;
        if (StateExists(config.stateBlink)) {
            var blinkTriggerSynced = manager.NewBool("BlinkTriggerSynced", synced: true);
            var blinkTrigger = manager.NewTrigger("BlinkTrigger");
            blinkActive = manager.NewBool("BlinkActive", def: true);

            {
                var blinkCounter = manager.NewInt("BlinkCounter");
                var layer = manager.NewLayer("Blink - Generator");
                var entry = layer.NewState("Entry");
                var remote = layer.NewState("Remote").Move(entry, 0, -1);
                var idle = layer.NewState("Idle").Move(entry, 0, 1);
                var subtract = layer.NewState("Subtract");
                var trigger0 = layer.NewState("Trigger 0").Move(subtract, 1, 0);
                var trigger1 = layer.NewState("Trigger 1").Move(trigger0, 1, 0);
                var randomize = layer.NewState("Randomize").Move(idle, 1, 0);

                entry.TransitionsTo(remote).When(IsLocal().IsFalse());
                entry.TransitionsTo(idle).When(Always());

                idle.TransitionsTo(trigger0).When(blinkCounter.IsLessThan(1).And(blinkTriggerSynced.IsTrue()));
                trigger0.Drives(blinkTriggerSynced, false);
                trigger0.TransitionsTo(randomize).When(Always());

                idle.TransitionsTo(trigger1).When(blinkCounter.IsLessThan(1).And(blinkTriggerSynced.IsFalse()));
                trigger1.Drives(blinkTriggerSynced, true);
                trigger1.TransitionsTo(randomize).When(Always());

                randomize.DrivesRandom(blinkCounter, 2, 10);
                randomize.TransitionsTo(idle).When(Always());

                idle.TransitionsTo(subtract).WithTransitionDurationSeconds(1f).When(Always());
                subtract.DrivesDelta(blinkCounter, -1);
                subtract.TransitionsTo(idle).When(Always());
            }

            {
                var layer = manager.NewLayer("Blink - Receiver");
                var blink0 = layer.NewState("Trigger == false");
                var blink1 = layer.NewState("Trigger == true");

                blink0.TransitionsTo(blink1).When(blinkTriggerSynced.IsTrue());
                blink0.Drives(blinkTrigger, true);
                blink1.TransitionsTo(blink0).When(blinkTriggerSynced.IsFalse());
                blink1.Drives(blinkTrigger, true);
            }

            {
                var blinkClip = loadClip("blink", config.stateBlink);
                var blinkDuration = 0.07f;
                var layer = manager.NewLayer("Blink - Animate");
                var idle = layer.NewState("Idle");
                var checkActive = layer.NewState("Check Active");
                var blink = layer.NewState("Blink").WithAnimation(blinkClip).Move(checkActive, 1, 0);

                idle.TransitionsTo(checkActive).When(blinkTrigger.IsTrue());
                checkActive.TransitionsTo(blink).WithTransitionDurationSeconds(blinkDuration).When(blinkActive.IsTrue());
                checkActive.TransitionsTo(idle).When(Always());
                blink.TransitionsTo(idle).WithTransitionDurationSeconds(blinkDuration).When(Always());
            }
        }

        var enableGestures = StateExists(config.stateEyesClosed)
            || StateExists(config.stateEyesHappy)
            || StateExists(config.stateEyesSad)
            || StateExists(config.stateEyesAngry)
            || StateExists(config.stateMouthBlep)
            || StateExists(config.stateMouthSuck)
            || StateExists(config.stateMouthSad)
            || StateExists(config.stateMouthAngry)
            || StateExists(config.stateMouthHappy)
            || StateExists(config.stateEarsBack);

        if (enableGestures) {
            var paramEmoteHappy = manager.NewBool("EmoteHappy", synced: true);
            var paramEmoteSad = manager.NewBool("EmoteSad", synced: true);
            var paramEmoteAngry = manager.NewBool("EmoteAngry", synced: true);
            var paramEmoteTongue = manager.NewBool("EmoteTongue", synced: true);
            // These don't actually need synced, but vrc gets annoyed that the menu is using an unsynced param
            var paramEmoteHappyLock = manager.NewBool("EmoteHappyLock", synced: true);
            manager.NewMenuToggle("Lock Happy", paramEmoteHappyLock);
            var paramEmoteSadLock = manager.NewBool("EmoteSadLock", synced: true);
            manager.NewMenuToggle("Lock Sad", paramEmoteSadLock);
            var paramEmoteAngryLock = manager.NewBool("EmoteAngryLock", synced: true);
            manager.NewMenuToggle("Lock Angry", paramEmoteAngryLock);
            var paramEmoteTongueLock = manager.NewBool("EmoteTongueLock", synced: true);
            manager.NewMenuToggle("Lock Tongue", paramEmoteTongueLock);

            {
                var layer = manager.NewLayer("Eyes");
                var idle = layer.NewState("Idle");
                var closed = layer.NewState("Closed").WithAnimation(loadClip("eyesClosed", config.stateEyesClosed));
                var happy = layer.NewState("Happy").WithAnimation(loadClip("eyesHappy", config.stateEyesHappy));
                //var bedroom = layer.NewState("Bedroom").WithAnimation(loadClip("eyesBedroom", inputs.stateEyesBedroom));
                var sad = layer.NewState("Sad").WithAnimation(loadClip("eyesSad", config.stateEyesSad));
                var angry = layer.NewState("Angry").WithAnimation(loadClip("eyesAngry", config.stateEyesAngry));

                if (blinkActive != null) {
                    idle.Drives(blinkActive, true);
                    closed.Drives(blinkActive, false);
                    happy.Drives(blinkActive, false);
                    //bedroom.Drives(blinkActive, false)
                    sad.Drives(blinkActive, false);
                    angry.Drives(blinkActive, false);
                }

                //closed.TransitionsFromAny().WithTransitionToSelf().WithTransitionDurationSeconds(0.1f).When(paramOrifaceMouthRing.IsTrue());
                //closed.TransitionsFromAny().WithTransitionToSelf().WithTransitionDurationSeconds(0.1f).When(paramOrifaceMouthHole.IsTrue());
                happy.TransitionsFromAny().WithTransitionToSelf().WithTransitionDurationSeconds(0.1f).When(paramEmoteHappy.IsTrue());
                //bedroom.TransitionsFromAny().WithTransitionToSelf().WithTransitionDurationSeconds(0.1f).When(bedroom.IsTrue());
                sad.TransitionsFromAny().WithTransitionToSelf().WithTransitionDurationSeconds(0.1f).When(paramEmoteSad.IsTrue());
                angry.TransitionsFromAny().WithTransitionToSelf().WithTransitionDurationSeconds(0.1f).When(paramEmoteAngry.IsTrue());
                idle.TransitionsFromAny().WithTransitionToSelf().WithTransitionDurationSeconds(0.1f).When(Always());
            }

            {
                var layer = manager.NewLayer("Mouth");
                var idle = layer.NewState("Idle");
                var blep = layer.NewState("Blep").WithAnimation(loadClip("mouthBlep", config.stateMouthBlep));
                var suck = layer.NewState("Suck").WithAnimation(loadClip("mouthSuck", config.stateMouthSuck));
                var sad = layer.NewState("Sad").WithAnimation(loadClip("mouthSad", config.stateMouthSad));
                var angry = layer.NewState("Angry").WithAnimation(loadClip("mouthAngry", config.stateMouthAngry));
                var happy = layer.NewState("Happy").WithAnimation(loadClip("mouthHappy", config.stateMouthHappy));

                //suck.TransitionsFromAny().WithTransitionToSelf().WithTransitionDurationSeconds(0.1f).When(paramOrifaceMouthRing.IsTrue());
                //suck.TransitionsFromAny().WithTransitionToSelf().WithTransitionDurationSeconds(0.1f).When(paramOrifaceMouthHole.IsTrue());
                blep.TransitionsFromAny().WithTransitionToSelf().WithTransitionDurationSeconds(0.1f).When(paramEmoteTongue.IsTrue());
                happy.TransitionsFromAny().WithTransitionToSelf().WithTransitionDurationSeconds(0.1f).When(paramEmoteHappy.IsTrue());
                sad.TransitionsFromAny().WithTransitionToSelf().WithTransitionDurationSeconds(0.1f).When(paramEmoteSad.IsTrue());
                angry.TransitionsFromAny().WithTransitionToSelf().WithTransitionDurationSeconds(0.1f).When(paramEmoteAngry.IsTrue());
                idle.TransitionsFromAny().WithTransitionToSelf().WithTransitionDurationSeconds(0.1f).When(Always());
            }

            {
                var layer = manager.NewLayer("Ears");
                var idle = layer.NewState("Idle");
                var back = layer.NewState("Back").WithAnimation(loadClip("earsBack", config.stateEarsBack));

                back.TransitionsFromAny().WithTransitionToSelf().WithTransitionDurationSeconds(0.1f).When(paramEmoteSad.IsTrue());
                back.TransitionsFromAny().WithTransitionToSelf().WithTransitionDurationSeconds(0.1f).When(paramEmoteAngry.IsTrue());
                idle.TransitionsFromAny().WithTransitionToSelf().WithTransitionDurationSeconds(0.1f).When(Always());
            }

            createGestureTriggerLayer("Tongue", paramEmoteTongueLock, paramEmoteTongue, 4);
            createGestureTriggerLayer("Happy", paramEmoteHappyLock, paramEmoteHappy, 7);
            createGestureTriggerLayer("Sad", paramEmoteSadLock, paramEmoteSad, 6);
            createGestureTriggerLayer("Angry", paramEmoteAngryLock, paramEmoteAngry, 5);
        }

        // SCALE
        if (config.scaleEnabled) {
            var paramScale = manager.NewFloat("Scale", synced: true, def: 0.5f);
            manager.NewMenuSlider("Scale", paramScale);
            var scaleClip = manager.NewClip("Scale");
            var baseScale = avatarObject.transform.localScale.x;
            motions.Scale(scaleClip, avatarObject, motions.FromFrames(
                new Keyframe(0, baseScale * 0.1f),
                new Keyframe(2, baseScale * 1),
                new Keyframe(3, baseScale * 2),
                new Keyframe(4, baseScale * 10)
            ));

            var layer = manager.NewLayer("Scale");
            var main = layer.NewState("Scale").WithAnimation(scaleClip).MotionTime(paramScale);
        }

        // SECURITY LOCK
        VFABool paramSecuritySync = null;
        if (config.securityCodeLeft > 0 && config.securityCodeRight > 0) {
            paramSecuritySync = manager.NewBool("SecurityLockSync", synced: true, defTrueInEditor: true);
            // This doesn't actually need synced, but vrc gets annoyed that the menu is using an unsynced param
            var paramSecurityMenu = manager.NewBool("SecurityLockMenu", synced: true);
            manager.NewMenuToggle("Security", paramSecurityMenu);
            var layer = manager.NewLayer("Security Lock");
            var entry = layer.NewState("Entry");
            var remote = layer.NewState("Remote").Move(entry, 0, -1);
            var locked = layer.NewState("Locked").Move(entry, 0, 1);
            var check = layer.NewState("Check");
            var unlocked = layer.NewState("Unlocked").Move(check, 1, 0);

            entry.TransitionsTo(remote).When(IsLocal().IsFalse());
            entry.TransitionsTo(locked).When(Always());

            locked.Drives(paramSecurityMenu, false);
            locked.Drives(paramSecuritySync, false);
            locked.TransitionsTo(check).When(paramSecurityMenu.IsTrue());

            check.TransitionsTo(unlocked).When(GestureLeft().IsEqualTo(config.securityCodeLeft).And(GestureRight().IsEqualTo(config.securityCodeRight)));
            check.TransitionsTo(locked).When(Always());

            unlocked.Drives(paramSecuritySync, true);
            unlocked.TransitionsTo(locked).When(paramSecurityMenu.IsFalse());
        }

        // TALK GLOW
        if (StateExists(config.stateTalking)) {
            var layer = manager.NewLayer("Talk Glow");
            var clip = loadClip("TalkGlow", config.stateTalking);
            var off = layer.NewState("Off");
            var on = layer.NewState("On").WithAnimation(clip);

            off.TransitionsTo(on).When(Viseme().IsGreaterThan(9));
            on.TransitionsTo(off).When(Viseme().IsLessThan(10));
        }
    }

    private void handleProps(List<VRCFuryProp> props, GameObject propBaseObject) {
        foreach (var prop in props) {
            handleProp(prop, propBaseObject);
        }
    }

    private VFACondition Always() {
        var paramTrue = manager.NewBool("True", def: true);
        return paramTrue.IsTrue();
    }
    private VFANumber GestureLeft() {
        return manager.NewInt("GestureLeft", usePrefix: false);
    }
    private VFANumber GestureRight() {
        return manager.NewInt("GestureRight", usePrefix: false);
    }
    private VFANumber Viseme() {
        return manager.NewInt("Viseme", usePrefix: false);
    }
    private VFABool IsLocal() {
        return manager.NewBool("IsLocal", usePrefix: false);
    }

    private void handleProp(VRCFuryProp prop, GameObject propBaseObject) {
        var layerName = "Prop - " + prop.name;

        VFABool physBoneResetter = null;
        if (prop.resetPhysbones.Count > 0) {
            physBoneResetter = createPhysboneResetter(layerName, prop.resetPhysbones);
        }

        if (prop.type == VRCFuryProp.PUPPET || (prop.type == VRCFuryProp.TOGGLE && prop.slider)) {
            var layer = manager.NewLayer(layerName);
            var tree = manager.NewBlendTree("prop_" + prop.name);
            tree.blendType = BlendTreeType.FreeformDirectional2D;
            tree.AddChild(noopClip, new Vector2(0,0));
            int i = 0;
            var puppetStops = new List<VRCFuryPropPuppetStop>();
            if (prop.type == VRCFuryProp.PUPPET) {
                puppetStops = prop.puppetStops;
            } else {
                puppetStops.Add(new VRCFuryPropPuppetStop(1,0,prop.state));
            }
            var usesX = false;
            var usesY = false;
            foreach (var stop in puppetStops) {
                if (stop.x != 0) usesX = true;
                if (stop.y != 0) usesY = true;
                tree.AddChild(loadClip("prop_" + prop.name + "_" + i++, stop.state, propBaseObject), new Vector2(stop.x,stop.y));
            }
            var on = layer.NewState("Blend").WithAnimation(tree);

            var x = manager.NewFloat("Prop_" + prop.name + "_x", synced: usesX);
            tree.blendParameter = x.Name();
            var y = manager.NewFloat("Prop_" + prop.name + "_y", synced: usesY);
            tree.blendParameterY = y.Name();
            if (prop.type == VRCFuryProp.TOGGLE) {
                if (usesX) manager.NewMenuSlider(prop.name, x);
            } else {
                manager.NewMenuPuppet(prop.name, usesX ? x : null, usesY ? y : null);
            }
        } else if (prop.type == VRCFuryProp.MODES) {
            var layer = manager.NewLayer(layerName);
            var off = layer.NewState("Off");
            if (physBoneResetter != null) off.Drives(physBoneResetter, true);
            var param = manager.NewInt("Prop_" + prop.name, synced: true, saved: prop.saved);
            manager.NewMenuToggle(prop.name + " - Off", param, 0);
            var i = 1;
            foreach (var mode in prop.modes) {
                var num = i++;
                var clip = loadClip("prop_" + prop.name+"_"+num, mode.state, propBaseObject);
                var state = layer.NewState(""+num).WithAnimation(clip);
                if (physBoneResetter != null) state.Drives(physBoneResetter, true);
                if (prop.securityEnabled) {
                    var paramSecuritySync = manager.NewBool("SecurityLockSync");
                    state.TransitionsFromAny().When(param.IsEqualTo(num).And(paramSecuritySync.IsTrue()));
                    state.TransitionsToExit().When(param.IsNotEqualTo(num));
                    state.TransitionsToExit().When(paramSecuritySync.IsFalse());
                } else {
                    state.TransitionsFromAny().When(param.IsEqualTo(num));
                    state.TransitionsToExit().When(param.IsNotEqualTo(num));
                }
                manager.NewMenuToggle(prop.name + " - " + num, param, num);
            }
        } else if (prop.type == VRCFuryProp.TOGGLE) {
            var layer = manager.NewLayer(layerName);
            var clip = loadClip("prop_" + prop.name, prop.state, propBaseObject);
            var off = layer.NewState("Off");
            var on = layer.NewState("On").WithAnimation(clip);
            var param = manager.NewBool("Prop_" + prop.name, synced: true, saved: prop.saved, def: prop.defaultOn);
            if (prop.securityEnabled) {
                var paramSecuritySync = manager.NewBool("SecurityLockSync");
                off.TransitionsTo(on).When(param.IsTrue().And(paramSecuritySync.IsTrue()));
                on.TransitionsTo(off).When(param.IsFalse());
                on.TransitionsTo(off).When(paramSecuritySync.IsFalse());
            } else {
                off.TransitionsTo(on).When(param.IsTrue());
                on.TransitionsTo(off).When(param.IsFalse());
            }
            if (physBoneResetter != null) {
                off.Drives(physBoneResetter, true);
                on.Drives(physBoneResetter, true);
            }
            manager.NewMenuToggle(prop.name, param);
        } else if (prop.type == VRCFuryProp.CONTROLLER) {
            if (prop.controller != null) {
                DataCopier.Copy(prop.controller, manager.GetRawController(), "[" + VRCFuryNameManager.prefix + "] [" + prop.name + "] ", from => {
                    var copy = manager.NewClip(prop.name+"__"+from.name);
                    motions.CopyWithAdjustedPrefixes(from, copy, propBaseObject);
                    return copy;
                });
            }
            if (prop.controllerMenu != null) {
                foreach (var control in prop.controllerMenu.controls) {
                    manager.GetFxMenu().controls.Add(control);
                }
            }
            if (prop.controllerParams != null) {
                foreach (var param in prop.controllerParams.parameters) {
                    manager.addSyncedParam(param);
                }
            }
        }
    }

    private void createGestureTriggerLayer(string name, VFABool lockParam, VFABool triggerParam, int gestureNum) {
        var layer = manager.NewLayer("Gesture - " + name);
        var off = layer.NewState("Off");
        var on = layer.NewState("On");

        var GestureLeft = manager.NewInt("GestureLeft", usePrefix: false);
        var GestureRight = manager.NewInt("GestureRight", usePrefix: false);

        off.TransitionsTo(on).When(lockParam.IsTrue());
        off.TransitionsTo(on).When(GestureLeft.IsEqualTo(gestureNum));
        off.TransitionsTo(on).When(GestureRight.IsEqualTo(gestureNum));
        on.TransitionsTo(off)
            .When(lockParam.IsFalse()
            .And(GestureLeft.IsNotEqualTo(gestureNum))
            .And(GestureRight.IsNotEqualTo(gestureNum)));

        off.Drives(triggerParam, false);
        on.Drives(triggerParam, true);
    }

    private VRCFuryNameManager manager;
    private VRCFuryClipUtils motions;
    private GameObject avatarObject;
    private string baseFile;
    private AnimationClip noopClip;
    private AnimationClip defaultClip;

    private GameObject find(string path) {
        var found = avatarObject.transform.Find(path)?.gameObject;
        if (found == null) {
            throw new Exception("Failed to find path '" + path + "'");
        }
        return found;
    }

    private SkinnedMeshRenderer findSkin(string path) {
        return find(path).GetComponent(typeof(SkinnedMeshRenderer)) as SkinnedMeshRenderer;
    }

    private List<SkinnedMeshRenderer> getAllSkins() {
        List<SkinnedMeshRenderer> skins = new List<SkinnedMeshRenderer>();
        foreach (Transform child in avatarObject.transform) {
            var skin = child.gameObject.GetComponent(typeof(SkinnedMeshRenderer)) as SkinnedMeshRenderer;
            if (skin != null) {
                skins.Add(skin);
            }
        }
        return skins;
    }

    private AnimationClip loadClip(string name, VRCFuryState state, GameObject prefixObj = null) {
        if (state.clip != null) {
            AnimationClip output = null;
            if (prefixObj != null && prefixObj != avatarObject) {
                var copy = manager.NewClip(name);
                motions.CopyWithAdjustedPrefixes(state.clip, copy, prefixObj);
                output = copy;
            } else {
                output = state.clip;
            }
            foreach (var binding in AnimationUtility.GetCurveBindings(output)) {
                var exists = AnimationUtility.GetFloatValue(avatarObject, binding, out var value);
                if (exists) {
                    AnimationUtility.SetEditorCurve(defaultClip, binding, motions.OneFrame(value));
                } else {
                    Debug.LogWarning("Missing default value for: " + binding.path);
                }
            }
            foreach (var binding in AnimationUtility.GetObjectReferenceCurveBindings(output)) {
                var exists = AnimationUtility.GetObjectReferenceValue(avatarObject, binding, out var value);
                if (exists) {
                    AnimationUtility.SetObjectReferenceCurve(defaultClip, binding, motions.OneFrame(value));
                } else {
                    Debug.LogWarning("Missing default value for: " + binding.path);
                }
            }
            return output;
        }
        if (state.actions.Count == 0) {
            return noopClip;
        }
        var clip = manager.NewClip(name);
        foreach (var action in state.actions) {
            if (action.type == VRCFuryAction.TOGGLE) {
                if (action.obj == null) {
                    Debug.LogWarning("Missing object in action: " + name);
                    continue;
                }
                motions.Enable(clip, action.obj, !action.obj.activeSelf);
                motions.Enable(defaultClip, action.obj, action.obj.activeSelf);
            }
            if (action.type == VRCFuryAction.BLENDSHAPE) {
                var foundOne = false;
                foreach (var skin in getAllSkins()) {
                    var blendShapeIndex = skin.sharedMesh.GetBlendShapeIndex(action.blendShape);
                    if (blendShapeIndex < 0) continue;
                    foundOne = true;
                    var defValue = skin.GetBlendShapeWeight(blendShapeIndex);
                    motions.BlendShape(clip, skin, action.blendShape, 100);
                    motions.BlendShape(defaultClip, skin, action.blendShape, defValue);
                }
                if (!foundOne) {
                    Debug.LogWarning("BlendShape not found in avatar: " + action.blendShape);
                }
            }
        }
        return clip;
    }

    private List<VRCFuryProp> getAllBaseProps(VRCFury config) {
        var props = new List<VRCFuryProp>();
        props.AddRange(config.props.props);

        // Toes
        {
            VRCFuryProp toes = new VRCFuryProp();
            toes.name = "Toes";
            toes.type = VRCFuryProp.PUPPET;
            if (StateExists(config.stateToesDown)) toes.puppetStops.Add(new VRCFuryPropPuppetStop(0,-1,config.stateToesDown));
            if (StateExists(config.stateToesUp)) toes.puppetStops.Add(new VRCFuryPropPuppetStop(0,1,config.stateToesUp));
            if (StateExists(config.stateToesSplay)) {
                toes.puppetStops.Add(new VRCFuryPropPuppetStop(-1,0,config.stateToesSplay));
                toes.puppetStops.Add(new VRCFuryPropPuppetStop(1,0,config.stateToesSplay));
            }
            if (toes.puppetStops.Count > 0) {
                props.Add(toes);
            }
        }

        // Breathing
        if (config.breatheObject != null || !string.IsNullOrEmpty(config.breatheBlendshape)) {
            var clip = manager.NewClip("Breathing");

            if (config.breatheObject != null) {
                motions.Scale(clip, config.breatheObject, motions.FromSeconds(
                    new Keyframe(0, config.breatheScaleMin),
                    new Keyframe(2.3f, config.breatheScaleMax),
                    new Keyframe(2.7f, config.breatheScaleMax),
                    new Keyframe(5, config.breatheScaleMin)
                ));
            }
            if (!string.IsNullOrEmpty(config.breatheBlendshape)) {
                var breathingSkins = getAllSkins().FindAll(skin => skin.sharedMesh.GetBlendShapeIndex(config.breatheBlendshape) != -1); 
                foreach (var skin in breathingSkins) {
                    motions.BlendShape(clip, skin, config.breatheBlendshape, motions.FromSeconds(
                        new Keyframe(0, 0),
                        new Keyframe(2.3f, 100),
                        new Keyframe(2.7f, 100),
                        new Keyframe(5, 0)
                    ));
                }
            }

            var prop = new VRCFuryProp();
            prop.type = VRCFuryProp.TOGGLE;
            prop.name = "Breathing";
            prop.defaultOn = true;
            prop.state = new VRCFuryState();
            prop.state.clip = clip;
            props.Add(prop);
        }

        return props;
    }

    private VFABool createPhysboneResetter(string layerName, List<GameObject> physBones) {
        var layer = manager.NewLayer(layerName + "_PhysBoneReset");
        var param = manager.NewTrigger(layerName + "_PhysBoneReset");
        var idle = layer.NewState("Idle");
        var pause = layer.NewState("Pause");
        var reset1 = layer.NewState("Reset").Move(pause, 1, 0);
        var reset2 = layer.NewState("Reset").Move(idle, 1, 0);
        idle.TransitionsTo(pause).When(param.IsTrue());
        pause.TransitionsTo(reset1).When(Always());
        reset1.TransitionsTo(reset2).When(Always());
        reset2.TransitionsTo(idle).When(Always());

        var resetClip = manager.NewClip(layerName + "_PhysBoneReset");
        foreach (var physBone in physBones) {
            if (physBone == null) {
                Debug.LogWarning("Physbone object in physboneResetter is missing!: " + layerName);
                continue;
            }
            motions.Enable(resetClip, physBone, false);
            motions.Enable(defaultClip, physBone, true);
        }

        reset1.WithAnimation(resetClip);
        reset2.WithAnimation(resetClip);

        return param;
    }

    public static bool IsVrcfAsset(UnityEngine.Object obj) {
        return obj != null && AssetDatabase.GetAssetPath(obj).Contains("_VRCFury");
    }

    private static bool StateExists(VRCFuryState state) {
        return state != null && !state.isEmpty();
    }
}

}
