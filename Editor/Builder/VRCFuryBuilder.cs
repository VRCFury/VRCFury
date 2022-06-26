using System;
using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;
using System.Collections.Generic;
using VRC.SDK3.Avatars.Components;
using VRC.SDK3.Avatars.ScriptableObjects;
using VRCF.Model;

namespace VRCF.Builder {

public class VRCFuryBuilder {
    public void Run(VRCFury inputs) {
        Debug.Log("VRCFury is running for " + inputs.gameObject.name + "...");
        EditorUtility.DisplayProgressBar("VRCFury is building ...", "", 0);

        this.inputs = inputs;
        rootObject = inputs.gameObject;
        var avatar = rootObject.GetComponent(typeof(VRCAvatarDescriptor)) as VRCAvatarDescriptor;
        fxController = (AnimatorController)avatar.baseAnimationLayers[4].animatorController;
        var menu = avatar.expressionsMenu;
        var syncedParams = avatar.expressionParameters;
        manager = new VRCFuryNameManager("VRCFury", menu, syncedParams, fxController);
        baseFile = AssetDatabase.GetAssetPath(fxController);
        motions = new VRCFuryClipUtils(rootObject);

        // CLEANUP OLD DATA
        manager.Purge();

        // REMOVE ANIMATORS FROM PREFAB INSTANCES (often used for prop testing)
        foreach (var otherAnimator in rootObject.GetComponentsInChildren<Animator>(true)) {
            if (otherAnimator.gameObject != rootObject && PrefabUtility.IsPartOfPrefabInstance(otherAnimator.gameObject)) {
                UnityEngine.Object.DestroyImmediate(otherAnimator);
            }
        }

        // DEFAULTS
        noopClip = manager.GetNoopClip();
        defaultClip = manager.NewClip("Defaults");
        var defaultLayer = manager.NewLayer("Defaults");
        defaultLayer.NewState("Defaults").WithAnimation(defaultClip);

        // Common Params
        var GestureLeft = manager.NewInt("GestureLeft", usePrefix: false);
        var GestureRight = manager.NewInt("GestureRight", usePrefix: false);
        var Viseme = manager.NewInt("Viseme", usePrefix: false);

        var paramTrue = manager.NewBool("True", def: true);
        always = paramTrue.IsTrue();
        var paramOrifaceMouthRing = manager.NewBool("OrifaceMouthRing", synced: true);
        var paramOrifaceMouthHole = manager.NewBool("OrifaceMouthHole", synced: true);
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
        var blinkTriggerSynced = manager.NewBool("BlinkTriggerSynced", synced: true);
        var blinkTrigger = manager.NewTrigger("BlinkTrigger");
        var blinkActive = manager.NewBool("BlinkActive", def: true);
        var paramScale = manager.NewFloat("Scale", synced: true, def: 0.5f);
        manager.NewMenuSlider("Scale", paramScale);

        // VISEMES
        if (inputs.visemeFolder != "") {
            var visemes = manager.NewLayer("Visemes");
            var VisemeParam = manager.NewInt("Viseme", usePrefix: false);
            Action<int, string> addViseme = (index, text) => {
                var animFileName = "Viseme-" + text;
                var clip = getClip(inputs.visemeFolder + "/" + animFileName);
                if (clip == null) throw new Exception("Missing animation for viseme " + animFileName);
                var state = visemes.NewState(text).WithAnimation(clip);
                if (text == "sil") state.Move(3, -8);
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

        {
            var layer = manager.NewLayer("Eyes");
            var idle = layer.NewState("Idle").Drives(blinkActive, true);
            var closed = layer.NewState("Closed").WithAnimation(loadClip("eyesClosed", inputs.stateEyesClosed)).Drives(blinkActive, false);
            var happy = layer.NewState("Happy").WithAnimation(loadClip("eyesHappy", inputs.stateEyesHappy)).Drives(blinkActive, false);
            //var bedroom = layer.NewState("Bedroom").WithAnimation(loadClip("eyesBedroom", inputs.stateEyesBedroom)).Drives(blinkActive, false)
            var sad = layer.NewState("Sad").WithAnimation(loadClip("eyesSad", inputs.stateEyesSad)).Drives(blinkActive, false);
            var angry = layer.NewState("Angry").WithAnimation(loadClip("eyesAngry", inputs.stateEyesAngry)).Drives(blinkActive, false);

            closed.TransitionsFromAny().WithTransitionToSelf().WithTransitionDurationSeconds(0.1f).When(paramOrifaceMouthRing.IsTrue());
            closed.TransitionsFromAny().WithTransitionToSelf().WithTransitionDurationSeconds(0.1f).When(paramOrifaceMouthHole.IsTrue());
            happy.TransitionsFromAny().WithTransitionToSelf().WithTransitionDurationSeconds(0.1f).When(paramEmoteHappy.IsTrue());
            //bedroom.TransitionsFromAny().WithTransitionToSelf().WithTransitionDurationSeconds(0.1f).When(bedroom.IsTrue());
            sad.TransitionsFromAny().WithTransitionToSelf().WithTransitionDurationSeconds(0.1f).When(paramEmoteSad.IsTrue());
            angry.TransitionsFromAny().WithTransitionToSelf().WithTransitionDurationSeconds(0.1f).When(paramEmoteAngry.IsTrue());
            idle.TransitionsFromAny().WithTransitionToSelf().WithTransitionDurationSeconds(0.1f).When(always);
        }

        {
            var layer = manager.NewLayer("Mouth");
            var idle = layer.NewState("Idle");
            var blep = layer.NewState("Blep").WithAnimation(loadClip("mouthBlep", inputs.stateMouthBlep));
            var suck = layer.NewState("Suck").WithAnimation(loadClip("mouthSuck", inputs.stateMouthSuck));
            var sad = layer.NewState("Sad").WithAnimation(loadClip("mouthSad", inputs.stateMouthSad));
            var angry = layer.NewState("Angry").WithAnimation(loadClip("mouthAngry", inputs.stateMouthAngry));
            var happy = layer.NewState("Happy").WithAnimation(loadClip("mouthHappy", inputs.stateMouthHappy));

            suck.TransitionsFromAny().WithTransitionToSelf().WithTransitionDurationSeconds(0.1f).When(paramOrifaceMouthRing.IsTrue());
            suck.TransitionsFromAny().WithTransitionToSelf().WithTransitionDurationSeconds(0.1f).When(paramOrifaceMouthHole.IsTrue());
            blep.TransitionsFromAny().WithTransitionToSelf().WithTransitionDurationSeconds(0.1f).When(paramEmoteTongue.IsTrue());
            happy.TransitionsFromAny().WithTransitionToSelf().WithTransitionDurationSeconds(0.1f).When(paramEmoteHappy.IsTrue());
            sad.TransitionsFromAny().WithTransitionToSelf().WithTransitionDurationSeconds(0.1f).When(paramEmoteSad.IsTrue());
            angry.TransitionsFromAny().WithTransitionToSelf().WithTransitionDurationSeconds(0.1f).When(paramEmoteAngry.IsTrue());
            idle.TransitionsFromAny().WithTransitionToSelf().WithTransitionDurationSeconds(0.1f).When(always);
        }

        {
            var layer = manager.NewLayer("Ears");
            var idle = layer.NewState("Idle");
            var back = layer.NewState("Back").WithAnimation(loadClip("earsBack", inputs.stateEarsBack));

            back.TransitionsFromAny().WithTransitionToSelf().WithTransitionDurationSeconds(0.1f).When(paramEmoteSad.IsTrue());
            back.TransitionsFromAny().WithTransitionToSelf().WithTransitionDurationSeconds(0.1f).When(paramEmoteAngry.IsTrue());
            idle.TransitionsFromAny().WithTransitionToSelf().WithTransitionDurationSeconds(0.1f).When(always);
        }

        createGestureTriggerLayer("Tongue", paramEmoteTongueLock, paramEmoteTongue, 4);
        createGestureTriggerLayer("Happy", paramEmoteHappyLock, paramEmoteHappy, 7);
        createGestureTriggerLayer("Sad", paramEmoteSadLock, paramEmoteSad, 6);
        createGestureTriggerLayer("Angry", paramEmoteAngryLock, paramEmoteAngry, 5);

        // BLINKING
        {
            var blinkCounter = manager.NewInt("BlinkCounter");
            var layer = manager.NewLayer("Blink - Generator");
            var idle = layer.NewState("Idle");
            var subtract = layer.NewState("Subtract");
            var trigger0 = layer.NewState("Trigger 0").Move(subtract, 1, 0);
            var trigger1 = layer.NewState("Trigger 1").Move(trigger0, 1, 0);
            var randomize = layer.NewState("Randomize").Move(idle, 1, 0);
            layer.AddRemoteEntry();

            idle.TransitionsTo(trigger0).When(blinkCounter.IsLessThan(1).And(blinkTriggerSynced.IsTrue()));
            trigger0.Drives(blinkTriggerSynced, false);
            trigger0.TransitionsTo(randomize).When(always);

            idle.TransitionsTo(trigger1).When(blinkCounter.IsLessThan(1).And(blinkTriggerSynced.IsFalse()));
            trigger1.Drives(blinkTriggerSynced, true);
            trigger1.TransitionsTo(randomize).When(always);

            randomize.DrivesRandom(blinkCounter, 2, 10);
            randomize.TransitionsTo(idle).When(always);

            idle.TransitionsTo(subtract).WithTransitionDurationSeconds(1f).When(always);
            subtract.DrivesDelta(blinkCounter, -1);
            subtract.TransitionsTo(idle).When(always);
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
            var blinkClip = loadClip("blink", inputs.stateBlink);
            var blinkDuration = 0.07f;
            var layer = manager.NewLayer("Blink - Animate");
            var idle = layer.NewState("Idle");
            var checkActive = layer.NewState("Check Active");
            var blink = layer.NewState("Blink").WithAnimation(blinkClip);

            idle.TransitionsTo(checkActive).When(blinkTrigger.IsTrue());
            checkActive.TransitionsTo(blink).WithTransitionDurationSeconds(blinkDuration).When(blinkActive.IsTrue());
            checkActive.TransitionsTo(idle).When(always);
            blink.TransitionsTo(idle).WithTransitionDurationSeconds(blinkDuration).When(always);
        }

        // SCALE
        {
            var scaleClip = manager.NewClip("Scale");
            motions.Scale(scaleClip, rootObject, motions.FromFrames(
                new Keyframe(0,0.1f),
                new Keyframe(2,1),
                new Keyframe(3,2),
                new Keyframe(4,10)
            ));

            var layer = manager.NewLayer("Scale");
            var main = layer.NewState("Scale").WithAnimation(scaleClip).MotionTime(paramScale);
        }

        // SECURITY LOCK
        var paramSecuritySync = manager.NewBool("SecurityLockSync", synced: true, defTrueInEditor: true);
        {
            // This doesn't actually need synced, but vrc gets annoyed that the menu is using an unsynced param
            var paramSecurityMenu = manager.NewBool("SecurityLockMenu", synced: true);
            manager.NewMenuToggle("Security", paramSecurityMenu);
            var layer = manager.NewLayer("SecurityLock");
            var locked = layer.NewState("Locked");
            var check = layer.NewState("Check");
            var unlocked = layer.NewState("Unlocked").Move(check, 1, 0);
            layer.AddRemoteEntry();

            locked.Drives(paramSecurityMenu, false);
            locked.Drives(paramSecuritySync, false);
            locked.TransitionsTo(check).When(paramSecurityMenu.IsTrue());

            check.TransitionsTo(unlocked).When(GestureLeft.IsEqualTo(4).And(GestureRight.IsEqualTo(4)));
            check.TransitionsTo(locked).When(always);

            unlocked.Drives(paramSecuritySync, true);
            unlocked.TransitionsTo(locked).When(paramSecurityMenu.IsFalse());
        }

        // TALK GLOW
        if (!inputs.stateTalkGlow.isEmpty()) {
            var layer = manager.NewLayer("Talk Glow");
            var clip = loadClip("TalkGlow", inputs.stateTalkGlow);
            var off = layer.NewState("Off");
            var on = layer.NewState("On").WithAnimation(clip);

            off.TransitionsTo(on).When(Viseme.IsGreaterThan(9));
            on.TransitionsTo(off).When(Viseme.IsLessThan(10));
        }

        // PROPS
        var allConfigs = new List<VRCFury>();
        allConfigs.Add(inputs);
        foreach (var otherConfig in rootObject.GetComponentsInChildren<VRCFury>(true)) {
            if (otherConfig == inputs) continue;
            Debug.Log("Importing config from " + otherConfig.gameObject.name);
            allConfigs.Add(otherConfig);
        }
        foreach (var conf in allConfigs) {
            var prefixObj = conf == inputs ? null : conf.gameObject;
            var allProps = conf == inputs ? getAllProps() : conf.props.props;
            foreach (var prop in allProps) {
                var layerName = "Prop - " + prop.name;
                var layer = manager.NewLayer(layerName);

                VFABool physBoneResetter = null;
                if (prop.resetPhysbones.Count > 0) {
                    physBoneResetter = createPhysboneResetter(layerName, prop.resetPhysbones);
                }

                if (prop.type == VRCFuryProp.PUPPET || (prop.type == VRCFuryProp.TOGGLE && prop.slider)) {
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
                        tree.AddChild(loadClip("prop_" + prop.name + "_" + i++, stop.state, prefixObj), new Vector2(stop.x,stop.y));
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
                    var off = layer.NewState("Off");
                    if (physBoneResetter != null) off.Drives(physBoneResetter, true);
                    var param = manager.NewInt("Prop_" + prop.name, synced: true, saved: prop.saved);
                    manager.NewMenuToggle(prop.name + " - Off", param, 0);
                    var i = 1;
                    foreach (var mode in prop.modes) {
                        var num = i++;
                        var clip = loadClip("prop_" + prop.name+"_"+num, mode.state, prefixObj);
                        var state = layer.NewState(""+num).WithAnimation(clip);
                        if (physBoneResetter != null) state.Drives(physBoneResetter, true);
                        if (prop.securityEnabled) {
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
                    var clip = loadClip("prop_" + prop.name, prop.state, prefixObj);
                    var off = layer.NewState("Off");
                    var on = layer.NewState("On").WithAnimation(clip);
                    var param = manager.NewBool("Prop_" + prop.name, synced: true, saved: prop.saved, def: prop.defaultOn);
                    if (prop.securityEnabled) {
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
                }
            }
        }

        Debug.Log("VRCFury Finished!");
        EditorUtility.ClearProgressBar();
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
    private VRCFury inputs;
    private GameObject rootObject;
    private string baseFile;
    private AnimationClip noopClip;
    private AnimationClip defaultClip;
    private AnimatorController fxController;
    private VFACondition always;

    private GameObject find(string path) {
        var found = rootObject.transform.Find(path)?.gameObject;
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
        foreach (Transform child in rootObject.transform) {
            var skin = child.gameObject.GetComponent(typeof(SkinnedMeshRenderer)) as SkinnedMeshRenderer;
            if (skin != null) {
                skins.Add(skin);
            }
        }
        return skins;
    }

    private AnimationClip getClip(string path) {
        var absPath = Canonicalize(baseFile + "/../" + path + ".anim");
        var motion = AssetDatabase.LoadMainAssetAtPath(absPath) as AnimationClip;
        return motion;
    }

    private AnimationClip loadClip(string name, VRCFuryState state, GameObject prefixObj = null) {
        if (state.clip != null) {
            AnimationClip output = null;
            if (prefixObj != null && prefixObj != rootObject) {
                var copy = manager.NewClip(name);
                motions.CopyWithAdjustedPrefixes(state.clip, copy, prefixObj);
                output = copy;
            } else {
                output = state.clip;
            }
            foreach (var binding in AnimationUtility.GetCurveBindings(output)) {
                var exists = AnimationUtility.GetFloatValue(rootObject, binding, out var value);
                if (exists) {
                    AnimationUtility.SetEditorCurve(defaultClip, binding, motions.OneFrame(value));
                } else {
                    Debug.LogWarning("Missing default value for: " + binding.path);
                }
            }
            foreach (var binding in AnimationUtility.GetObjectReferenceCurveBindings(output)) {
                var exists = AnimationUtility.GetObjectReferenceValue(rootObject, binding, out var value);
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

    private static string Canonicalize(string path) {
        var fakeRoot = Environment.CurrentDirectory;
        var combined = System.IO.Path.Combine(fakeRoot, path);
        combined = System.IO.Path.GetFullPath(combined);
        return RelativeTo(combined, fakeRoot);
    }
    private static string RelativeTo(string filespec, string folder)
    {
        var pathUri = new Uri(filespec);
        // Folders must end in a slash
        if (!folder.EndsWith(System.IO.Path.DirectorySeparatorChar.ToString())) folder += System.IO.Path.DirectorySeparatorChar;
        var folderUri = new Uri(folder);
        return Uri.UnescapeDataString(folderUri.MakeRelativeUri(pathUri).ToString()
            .Replace('/', System.IO.Path.DirectorySeparatorChar));
    }

    private List<VRCFuryProp> getAllProps() {
        var props = new List<VRCFuryProp>();
        props.AddRange(inputs.props.props);

        // Toes
        {
            VRCFuryProp toes = new VRCFuryProp();
            toes.name = "Toes";
            toes.type = VRCFuryProp.PUPPET;
            if (!inputs.stateToesDown.isEmpty()) toes.puppetStops.Add(new VRCFuryPropPuppetStop(0,-1,inputs.stateToesDown));
            if (!inputs.stateToesUp.isEmpty()) toes.puppetStops.Add(new VRCFuryPropPuppetStop(0,1,inputs.stateToesUp));
            if (!inputs.stateToesSplay.isEmpty()) {
                toes.puppetStops.Add(new VRCFuryPropPuppetStop(-1,0,inputs.stateToesSplay));
                toes.puppetStops.Add(new VRCFuryPropPuppetStop(1,0,inputs.stateToesSplay));
            }
            if (toes.puppetStops.Count > 0) {
                props.Add(toes);
            }
        }

        // Breathing
        if (inputs.breatheObject != null || inputs.breatheBlendshape != "") {
            var clip = manager.NewClip("Breathing");
            var layer = manager.NewLayer("Breathing");
            var main = layer.NewState("Breathe").WithAnimation(clip);

            if (inputs.breatheObject != null) {
                motions.Scale(clip, inputs.breatheObject, motions.FromSeconds(
                    new Keyframe(0, inputs.breatheScaleMin),
                    new Keyframe(2.3f, inputs.breatheScaleMax),
                    new Keyframe(2.7f, inputs.breatheScaleMax),
                    new Keyframe(5, inputs.breatheScaleMin)
                ));
            }
            if (inputs.breatheBlendshape != "") {
                var breathingSkins = getAllSkins().FindAll(skin => skin.sharedMesh.GetBlendShapeIndex(inputs.breatheBlendshape) != -1); 
                foreach (var skin in breathingSkins) {
                    motions.BlendShape(clip, skin, inputs.breatheBlendshape, motions.FromSeconds(
                        new Keyframe(0, 0),
                        new Keyframe(2.3f, 100),
                        new Keyframe(2.7f, 100),
                        new Keyframe(5, 0)
                    ));
                }
            }

            var prop = new VRCFuryProp();
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
        pause.TransitionsTo(reset1).When(always);
        reset1.TransitionsTo(reset2).When(always);
        reset2.TransitionsTo(idle).When(always);

        var resetClip = manager.NewClip(layerName + "_PhysBoneReset");
        foreach (var physBone in physBones) {
            motions.Enable(resetClip, physBone, false);
            motions.Enable(defaultClip, physBone, true);
        }

        reset1.WithAnimation(resetClip);
        reset2.WithAnimation(resetClip);

        return param;
    }
}

}
