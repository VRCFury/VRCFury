using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using VF.Builder;
using VF.Feature.Base;
using VF.Inspector;
using VF.Model.Feature;

namespace VF.Feature {
    public class GestureDriverBuilder : FeatureBuilder<GestureDriver> {
        private int i = 0;
        private readonly Dictionary<string, VFACondition> excludeConditions = new Dictionary<string, VFACondition>();


        
        [FeatureBuilderAction]
        public void Apply() {
            foreach (var gesture in model.gestures) {
                MakeGesture(gesture);
            }

            if (model.disableable && !string.IsNullOrWhiteSpace(model.enablePath)) {
                var fx = GetFx();
                var enabledParam = fx.NewBool("VRCFury Gestures Enabled", synced: true, usePrefix: false, saved: model.saveEnable, def: model.defaultEnable);

                manager.GetMenu().NewMenuToggle(model.enablePath, enabledParam);
            }
        }

        private void MakeGesture(GestureDriver.Gesture gesture, GestureDriver.Hand handOverride = GestureDriver.Hand.EITHER) {
            var hand = handOverride == GestureDriver.Hand.EITHER ? gesture.hand : handOverride;
            
            if (gesture.enableWeight && hand == GestureDriver.Hand.EITHER &&
                gesture.sign == GestureDriver.HandSign.FIST) {
                MakeGesture(gesture, GestureDriver.Hand.LEFT);
                MakeGesture(gesture, GestureDriver.Hand.RIGHT);
                return;
            }

            var fx = GetFx();
            var uniqueNum = i++;
            var name = "Gesture " + uniqueNum + " - " + hand;
            if (hand != GestureDriver.Hand.NEITHER) {
                name += " " + gesture.sign;
            }
            if (hand == GestureDriver.Hand.COMBO) {
                name += " " + gesture.comboSign;
            }
            var uid = "gesture_" + uniqueNum;

            var GestureLeft = fx.GestureLeft();
            var GestureRight = fx.GestureRight();

            VFACondition onCondition;
            int weightHand = 0;
            if (hand == GestureDriver.Hand.LEFT) {
                onCondition = GestureLeft.IsEqualTo((int)gesture.sign);
                if (gesture.sign == GestureDriver.HandSign.FIST) weightHand = 1;
            } else if (hand == GestureDriver.Hand.RIGHT) {
                onCondition = GestureRight.IsEqualTo((int)gesture.sign);
                if (gesture.sign == GestureDriver.HandSign.FIST) weightHand = 2;
            } else if (hand == GestureDriver.Hand.EITHER) {
                onCondition = GestureLeft.IsEqualTo((int)gesture.sign).Or(GestureRight.IsEqualTo((int)gesture.sign));
            } else if (hand == GestureDriver.Hand.COMBO) {
                onCondition = GestureLeft.IsEqualTo((int)gesture.sign).And(GestureRight.IsEqualTo((int)gesture.comboSign));
                if (gesture.comboSign == GestureDriver.HandSign.FIST) weightHand = 2;
                else if(gesture.sign == GestureDriver.HandSign.FIST) weightHand = 1;
            } else if (hand == GestureDriver.Hand.NEITHER) {
                onCondition = fx.Never();
            } else {
                throw new Exception("Unknown hand type");
            }

            if (gesture.enableExclusiveTag) {
                foreach (var tag in gesture.exclusiveTag.Split(',')) {
                    var trimmedTag = tag.Trim();
                    if (!string.IsNullOrWhiteSpace(trimmedTag)) {
                        if(hand != GestureDriver.Hand.NEITHER) {
                            if (excludeConditions.TryGetValue(trimmedTag, out var excludeCondition)) {
                                excludeConditions[trimmedTag] = excludeCondition.Or(onCondition);
                                onCondition = onCondition.And(excludeCondition.Not());
                            } else {
                                excludeConditions[trimmedTag] = onCondition;
                            }
                        }
                    }
                }
            }
            
            string disableBlinkingName = null;

            if (gesture.disableBlinking) {
                disableBlinkingName = uid + "_disableBlink";
                var disableBlinkParam = fx.NewBool(disableBlinkingName);
                addOtherFeature(new BlinkingBuilder.BlinkingPrevention { param = disableBlinkParam });
            }

            var clip = LoadState(uid, gesture.state);

            Motion motion;
            if (gesture.enableWeight && weightHand > 0) {
                MakeWeightParams();
                var weightParam = weightHand == 1 ? leftWeightParam : rightWeightParam;
                var tree = manager.GetClipStorage().NewBlendTree(uid + "_blend");
                tree.blendType = BlendTreeType.Simple1D;
                tree.useAutomaticThresholds = false;
                tree.blendParameter = weightParam.Name();
                tree.AddChild(manager.GetClipStorage().GetNoopClip(), 0);
                tree.AddChild(clip, 1);
                motion = tree;
            } else {
                motion = clip;
            }

            var transitionTime = gesture.customTransitionTime && gesture.transitionTime >= 0 ? gesture.transitionTime : 0.1f;

            var enabledParam = fx.True();

            if (model.disableable && !string.IsNullOrWhiteSpace(model.enablePath)) {
                enabledParam = fx.NewBool("VRCFury Gestures Enabled", synced: true, usePrefix: false, saved: model.saveEnable, def: model.defaultEnable);
            }

            onCondition = onCondition.And(enabledParam.IsTrue());

            var toggle = new Model.Feature.Toggle {
                name = gesture.lockMenuItem ?? uid,
                addMenuItem = gesture.lockMenuItem != null,
                state = gesture.state,
                motionOverride = motion,
                altCondition = onCondition.transitions,
                enableDriveGlobalParam = gesture.disableBlinking,
                driveGlobalParam = disableBlinkingName,
                enableExclusiveTag = gesture.enableExclusiveTag,
                exclusiveTag = gesture.exclusiveTag,
                transitionTime = transitionTime
            };

            addOtherFeature(toggle);
        }

        private VFANumber leftWeightParam;
        private VFANumber rightWeightParam;
        private void MakeWeightParams() {
            if (leftWeightParam != null) return;
            var fx = GetFx();
            var GestureLeftWeight = fx.GestureLeftWeight();
            var GestureRightWeight = fx.GestureRightWeight();
            var GestureLeftCondition = fx.GestureLeft().IsEqualTo(1);
            var GestureRightCondition = fx.GestureRight().IsEqualTo(1);
            leftWeightParam = MakeWeightLayer("left", GestureLeftWeight, GestureLeftCondition);
            rightWeightParam = MakeWeightLayer("right", GestureRightWeight, GestureRightCondition);
        }
        private VFANumber MakeWeightLayer(string name, VFANumber input, VFACondition whenEnabled) {
            var fx = GetFx();
            var layer = fx.NewLayer("GestureWeight_" + name);
            var output = fx.NewFloat(input.Name() + "_cached");

            // == BEGIN Smoothing logic
            // == Inspired by https://github.com/regzo2/OSCmooth

            //Values: 0 => no smoothing, 1 => no change in value, 0.999 => very smooth
            //TODO: maybe make this configurable and split between local/remote
            var localSmoothParam = fx.NewFloat(input.Name() + "_smooth_local", def: 0.65f); 
            var remoteSmoothParam = fx.NewFloat(input.Name() + "_smooth_remote", def: 0.85f); 

            //FeedbackClips - they drive the feedback values back to the output param
            var minClip = manager.GetClipStorage().NewClip(input.Name() + "-1");
            minClip.SetCurve("", typeof(Animator), output.Name(), AnimationCurve.Constant(0, 0, -1f));
            var maxClip = manager.GetClipStorage().NewClip(input.Name() + "1");
            maxClip.SetCurve("", typeof(Animator), output.Name(), AnimationCurve.Constant(0, 0, 1f));

            //Update tree - moves toward the target value
            var updateTree = manager.GetClipStorage().NewBlendTree("GestureWeight_" + name + "_input");
            updateTree.blendType = BlendTreeType.Simple1D;
            updateTree.useAutomaticThresholds = false;
            updateTree.blendParameter = input.Name();
            updateTree.AddChild(minClip, -1);
            updateTree.AddChild(maxClip, 1);
            
            //Maintain tree - maintains the current value
            var maintainTree = manager.GetClipStorage().NewBlendTree("GestureWeight_" + name + "_driver");
            maintainTree.blendType = BlendTreeType.Simple1D;
            maintainTree.useAutomaticThresholds = false;
            maintainTree.blendParameter = output.Name();
            maintainTree.AddChild(minClip, -1);
            maintainTree.AddChild(maxClip, 1);

            //The following two trees merge the update and the maintain tree together. The smoothParam controls 
            //how much from either tree should be applied during each tick
            var localTree = manager.GetClipStorage().NewBlendTree("GestureWeight_" + name + "_root_local");
            localTree.blendType = BlendTreeType.Simple1D;
            localTree.useAutomaticThresholds = false;
            localTree.blendParameter = localSmoothParam.Name();
            localTree.AddChild(updateTree, 0);
            localTree.AddChild(maintainTree, 1);

            var remoteTree = manager.GetClipStorage().NewBlendTree("GestureWeight_" + name + "_root_remote");
            remoteTree.blendType = BlendTreeType.Simple1D;
            remoteTree.useAutomaticThresholds = false;
            remoteTree.blendParameter = remoteSmoothParam.Name();
            remoteTree.AddChild(updateTree, 0);
            remoteTree.AddChild(maintainTree, 1);

            var off = layer.NewState("Off");
            var onLocal = layer.NewState("On Local").Move(off, -0.5f, 2f);
            var onRemote = layer.NewState("On Remote").Move(onLocal, 1f, 0f);

            var whenLocal = whenEnabled.And(fx.IsLocal().IsTrue());
            var whenRemote = whenEnabled.And(fx.IsLocal().IsFalse());
            var whenOff = whenLocal.Not().And(whenRemote.Not());

            off.TransitionsTo(onLocal).When(whenLocal);
            off.TransitionsTo(onRemote).When(whenRemote);
            off.WithAnimation(maintainTree);
            onLocal.TransitionsTo(off).When(whenOff);
            onLocal.TransitionsTo(onRemote).When(whenRemote);
            onLocal.WithAnimation(localTree);
            onRemote.TransitionsTo(off).When(whenOff);
            onRemote.TransitionsTo(onLocal).When(whenLocal);
            onRemote.WithAnimation(remoteTree);

            return output;
        }

        public override string GetEditorTitle() {
            return "Gestures";
        }

        public override VisualElement CreateEditor(SerializedProperty prop) {
            var content = new VisualElement();

            var disableableProp = prop.FindPropertyRelative("disableable");

            var button = VRCFuryEditorUtils.Button("Options", () => {
            var advMenu = new GenericMenu();
                advMenu.AddItem(new GUIContent("Disableable"), disableableProp.boolValue, () => {
                    disableableProp.boolValue = !disableableProp.boolValue;
                    prop.serializedObject.ApplyModifiedProperties();
                });
                advMenu.ShowAsContext();
            });
            content.Add(button);

            content.Add(VRCFuryEditorUtils.RefreshOnChange(() => {
                var c = new VisualElement();
                if (disableableProp.boolValue) {
                    c.Add(VRCFuryEditorUtils.Prop(prop.FindPropertyRelative("enablePath"), "Menu Path for Disable Toggle"));
                    c.Add(VRCFuryEditorUtils.Prop(prop.FindPropertyRelative("defaultEnable"), "Enable Gestures by Default"));
                    c.Add(VRCFuryEditorUtils.Prop(prop.FindPropertyRelative("saveEnable"), "Save Gesture Disable Status"));
                }
                return c;
            }, disableableProp));


            content.Add(VRCFuryEditorUtils.List(prop.FindPropertyRelative("gestures"),
                (i,el) => RenderGestureEditor(el)));

            return content;
        }

        private VisualElement RenderGestureEditor(SerializedProperty gesture) {
            var wrapper = new VisualElement();
            
            var handProp = gesture.FindPropertyRelative("hand");
            var signProp = gesture.FindPropertyRelative("sign");
            var comboSignProp = gesture.FindPropertyRelative("comboSign");

            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            var hand = VRCFuryEditorUtils.Prop(handProp);
            hand.style.flexBasis = 70;
            row.Add(hand);
            var handSigns = VRCFuryEditorUtils.RefreshOnChange(() => {
                var w = new VisualElement();
                w.style.flexDirection = FlexDirection.Row;
                w.style.alignItems = Align.Center;
                var leftBox = VRCFuryEditorUtils.Prop(signProp);
                var rightBox = VRCFuryEditorUtils.Prop(comboSignProp);
                if ((GestureDriver.Hand)handProp.enumValueIndex == GestureDriver.Hand.COMBO) {
                    w.Add(new Label("L") { style = { flexBasis = 10 }});
                    leftBox.style.flexGrow = 1;
                    w.Add(leftBox);
                    w.Add(new Label("R") { style = { flexBasis = 10 }});
                    rightBox.style.flexGrow = 1;
                    w.Add(rightBox);
                } else if ((GestureDriver.Hand)handProp.enumValueIndex != GestureDriver.Hand.NEITHER) {
                    leftBox.style.flexGrow = 1;
                    w.Add(leftBox);
                }

                return w;
            }, handProp);
            handSigns.style.flexGrow = 1;
            row.Add(handSigns);
            wrapper.Add(row);

            wrapper.Add(VRCFuryStateEditor.render(gesture.FindPropertyRelative("state")));

            var disableBlinkProp = gesture.FindPropertyRelative("disableBlinking");
            var customTransitionTimeProp = gesture.FindPropertyRelative("customTransitionTime");
            var transitionTimeProp = gesture.FindPropertyRelative("transitionTime");
            var enableLockMenuItemProp = gesture.FindPropertyRelative("enableLockMenuItem");
            var lockMenuItemProp = gesture.FindPropertyRelative("lockMenuItem");
            var enableExclusiveTagProp = gesture.FindPropertyRelative("enableExclusiveTag");
            var exclusiveTagProp = gesture.FindPropertyRelative("exclusiveTag");
            var enableWeightProp = gesture.FindPropertyRelative("enableWeight");

            var button = VRCFuryEditorUtils.Button("Options", () => {
                var advMenu = new GenericMenu();
                advMenu.AddItem(new GUIContent("Disable blinking when active"), disableBlinkProp.boolValue, () => {
                    disableBlinkProp.boolValue = !disableBlinkProp.boolValue;
                    gesture.serializedObject.ApplyModifiedProperties();
                });
                advMenu.AddItem(new GUIContent("Customize transition time"), customTransitionTimeProp.boolValue, () => {
                    customTransitionTimeProp.boolValue = !customTransitionTimeProp.boolValue;
                    gesture.serializedObject.ApplyModifiedProperties();
                });
                advMenu.AddItem(new GUIContent("Add 'Gesture Lock' toggle to menu"), enableLockMenuItemProp.boolValue,
                    () => {
                        enableLockMenuItemProp.boolValue = !enableLockMenuItemProp.boolValue;
                        gesture.serializedObject.ApplyModifiedProperties();
                    });
                advMenu.AddItem(new GUIContent("Enable exclusive tag"), enableExclusiveTagProp.boolValue, () => {
                    enableExclusiveTagProp.boolValue = !enableExclusiveTagProp.boolValue;
                    gesture.serializedObject.ApplyModifiedProperties();
                });
                advMenu.AddItem(new GUIContent("Use gesture weight (fist only)"), enableWeightProp.boolValue, () => {
                    enableWeightProp.boolValue = !enableWeightProp.boolValue;
                    gesture.serializedObject.ApplyModifiedProperties();
                });
                advMenu.ShowAsContext();
            });
            button.style.flexBasis = 70;

            row.Add(button);
            
            wrapper.Add(VRCFuryEditorUtils.RefreshOnChange(() => {
                var w = new VisualElement();
                if (disableBlinkProp.boolValue) w.Add(VRCFuryEditorUtils.WrappedLabel("Blinking disabled when active"));
                if (customTransitionTimeProp.boolValue) w.Add(VRCFuryEditorUtils.Prop(transitionTimeProp, "Custom transition time (s)"));
                if (enableLockMenuItemProp.boolValue) w.Add(VRCFuryEditorUtils.Prop(lockMenuItemProp, "Lock menu item path"));
                if (enableExclusiveTagProp.boolValue) w.Add(VRCFuryEditorUtils.Prop(exclusiveTagProp, "Exclusive Tag"));
                if (enableWeightProp.boolValue) w.Add(VRCFuryEditorUtils.WrappedLabel("Use gesture weight (fist only)"));
                return w;
            }, disableBlinkProp, customTransitionTimeProp, enableLockMenuItemProp, enableExclusiveTagProp, enableWeightProp));
            
            return wrapper;
        }

        public override bool AvailableOnProps() {
            return false;
        }
    }
}