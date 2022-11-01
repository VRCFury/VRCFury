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
        private readonly Dictionary<string, VFABool> lockMenuItems = new Dictionary<string, VFABool>();
        private readonly Dictionary<string, VFACondition> excludeConditions = new Dictionary<string, VFACondition>();
        
        [FeatureBuilderAction]
        public void Apply() {
            foreach (var gesture in model.gestures) {
                MakeGesture(gesture);
            }
        }

        private void MakeGesture(GestureDriver.Gesture gesture) {
            if (gesture.enableWeight && gesture.hand == GestureDriver.Hand.EITHER &&
                gesture.sign == GestureDriver.HandSign.FIST) {
                var clone = VRCFuryEditorUtils.DeepCloneSerializable(gesture);
                clone.hand = GestureDriver.Hand.LEFT;
                MakeGesture(clone);
                clone.hand = GestureDriver.Hand.RIGHT;
                MakeGesture(clone);
                return;
            }

            var fx = GetFx();
            var uniqueNum = i++;
            var name = "Gesture " + uniqueModelNum + "#" + uniqueNum + " - " + gesture.hand + " " + gesture.sign;
            if (gesture.hand == GestureDriver.Hand.COMBO) {
                name += " " + gesture.comboSign;
            }
            var uid = "gesture_" + uniqueModelNum + "_" + uniqueNum;

            var layer = fx.NewLayer(name);
            var off = layer.NewState("Off");
            var on = layer.NewState("On");

            VFABool lockMenuParam = null;
            if (gesture.enableLockMenuItem && !string.IsNullOrWhiteSpace(gesture.lockMenuItem)) {
                if (!lockMenuItems.TryGetValue(gesture.lockMenuItem, out lockMenuParam)) {
                    // This doesn't actually need synced, but vrc gets annoyed if the menu is using an unsynced param
                    lockMenuParam = fx.NewBool(uid + "_lock", synced: true);
                    manager.GetMenu().NewMenuToggle(gesture.lockMenuItem, lockMenuParam);
                    lockMenuItems[gesture.lockMenuItem] = lockMenuParam;
                }
            }

            var GestureLeft = fx.NewInt("GestureLeft", usePrefix: false);
            var GestureRight = fx.NewInt("GestureRight", usePrefix: false);

            VFACondition onCondition;
            int weightHand = 0;
            if (gesture.hand == GestureDriver.Hand.LEFT) {
                onCondition = GestureLeft.IsEqualTo((int)gesture.sign);
                if (gesture.sign == GestureDriver.HandSign.FIST) weightHand = 1;
            } else if (gesture.hand == GestureDriver.Hand.RIGHT) {
                onCondition = GestureRight.IsEqualTo((int)gesture.sign);
                if (gesture.sign == GestureDriver.HandSign.FIST) weightHand = 2;
            } else if (gesture.hand == GestureDriver.Hand.EITHER) {
                onCondition = GestureLeft.IsEqualTo((int)gesture.sign).Or(GestureRight.IsEqualTo((int)gesture.sign));
            } else if (gesture.hand == GestureDriver.Hand.COMBO) {
                onCondition = GestureLeft.IsEqualTo((int)gesture.sign).And(GestureRight.IsEqualTo((int)gesture.comboSign));
                if (gesture.comboSign == GestureDriver.HandSign.FIST) weightHand = 2;
                else if(gesture.sign == GestureDriver.HandSign.FIST) weightHand = 1;
            } else {
                throw new Exception("Unknown hand type");
            }

            if (lockMenuParam != null) {
                onCondition = onCondition.Or(lockMenuParam.IsTrue());
            }

            if (gesture.enableExclusiveTag) {
                foreach (var tag in gesture.exclusiveTag.Split(',')) {
                    var trimmedTag = tag.Trim();
                    if (!string.IsNullOrWhiteSpace(trimmedTag)) {
                        if (excludeConditions.TryGetValue(trimmedTag, out var excludeCondition)) {
                            excludeConditions[trimmedTag] = excludeCondition.Or(onCondition);
                            onCondition = onCondition.And(excludeCondition.Not());
                        } else {
                            excludeConditions[trimmedTag] = onCondition;
                        }
                    }
                }
            }

            if (gesture.disableBlinking) {
                var disableBlinkParam = fx.NewBool(uid + "_disableBlink");
                off.Drives(disableBlinkParam, false);
                on.Drives(disableBlinkParam, true);
                addOtherFeature(new BlinkingBuilder.BlinkingPrevention { param = disableBlinkParam });
            }
            
            var clip = LoadState(uid, gesture.state);
            if (weightHand > 0) {
                MakeWeightParams();
                var weightParam = weightHand == 1 ? leftWeightParam : rightWeightParam;
                var tree = manager.GetClipStorage().NewBlendTree(uid + "_blend");
                tree.blendType = BlendTreeType.Simple1D;
                tree.useAutomaticThresholds = false;
                tree.blendParameter = weightParam.Name();
                tree.AddChild(manager.GetClipStorage().GetNoopClip(), 0);
                tree.AddChild(clip, 1);
                on.WithAnimation(tree);
            } else {
                on.WithAnimation(clip);
            }

            var transitionTime = gesture.customTransitionTime && gesture.transitionTime >= 0 ? gesture.transitionTime : 0.1f;
            off.TransitionsTo(on).WithTransitionDurationSeconds(transitionTime).When(onCondition);
            on.TransitionsTo(off).WithTransitionDurationSeconds(transitionTime).When(onCondition.Not());
        }

        private VFANumber leftWeightParam;
        private VFANumber rightWeightParam;
        private void MakeWeightParams() {
            if (leftWeightParam != null) return;
            var fx = GetFx();
            var GestureLeftWeight = fx.NewFloat("GestureLeftWeight", usePrefix: false);
            var GestureRightWeight = fx.NewFloat("GestureRightWeight", usePrefix: false);
            var GestureLeftCondition = fx.NewInt("GestureLeft", usePrefix: false).IsEqualTo(1);
            var GestureRightCondition = fx.NewInt("GestureRight", usePrefix: false).IsEqualTo(1);
            leftWeightParam = MakeWeightLayer("left", GestureLeftWeight, GestureLeftCondition);
            rightWeightParam = MakeWeightLayer("right", GestureRightWeight, GestureRightCondition);
        }
        private VFANumber MakeWeightLayer(string name, VFANumber input, VFACondition whenEnabled) {
            var fx = GetFx();
            var layer = fx.NewLayer("GestureWeight_" + name);
            var output = fx.NewFloat(input.Name() + "_cached");
            
            var initClip = manager.GetClipStorage().NewClip("GestureWeightInit_" + output.Name());
            initClip.SetCurve("", typeof(Animator), output.Name(), AnimationCurve.Constant(0, 600, 1));
            var driveClip = manager.GetClipStorage().NewClip("GestureWeightDrive_" + output.Name());
            driveClip.SetCurve("", typeof(Animator), output.Name(), AnimationCurve.Linear(0, 0, 600, 1));

            var init = layer.NewState("Init");
            var off = layer.NewState("Off").Move(1,-1);
            var on = layer.NewState("On");
            var whenWeightSeen = input.IsGreaterThan(0);

            init.TransitionsTo(on).When(whenWeightSeen);
            init.WithAnimation(initClip);
            off.TransitionsTo(on).When(whenEnabled);
            off.WithAnimation(driveClip).MotionTime(output);
            on.TransitionsTo(off).When(whenEnabled.Not());
            on.WithAnimation(driveClip).MotionTime(input);
            
            return output;
        }

        public override string GetEditorTitle() {
            return "Gestures";
        }

        public override VisualElement CreateEditor(SerializedProperty prop) {
            return VRCFuryEditorUtils.List(prop.FindPropertyRelative("gestures"),
                (i,el) => RenderGestureEditor(el));
        }

        private VisualElement RenderGestureEditor(SerializedProperty gesture) {
            var wrapper = new VisualElement();
            
            var handProp = gesture.FindPropertyRelative("hand");
            var signProp = gesture.FindPropertyRelative("sign");
            var comboSignProp = gesture.FindPropertyRelative("comboSign");

            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            var hand = VRCFuryEditorUtils.PropWithoutLabel(handProp);
            hand.style.flexBasis = 70;
            row.Add(hand);
            var handSigns = VRCFuryEditorUtils.RefreshOnChange(() => {
                var w = new VisualElement();
                w.style.flexDirection = FlexDirection.Row;
                w.style.alignItems = Align.Center;
                var leftBox = VRCFuryEditorUtils.PropWithoutLabel(signProp);
                var rightBox = VRCFuryEditorUtils.PropWithoutLabel(comboSignProp);
                if ((GestureDriver.Hand)handProp.enumValueIndex == GestureDriver.Hand.COMBO) {
                    w.Add(new Label("L") { style = { flexBasis = 10 }});
                    leftBox.style.flexGrow = 1;
                    w.Add(leftBox);
                    w.Add(new Label("R") { style = { flexBasis = 10 }});
                    rightBox.style.flexGrow = 1;
                    w.Add(rightBox);
                } else {
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

            var button = new Button(() => {
                var advMenu = new GenericMenu();
                advMenu.AddItem(new GUIContent("Disable blinking when active"), disableBlinkProp.boolValue, () => {
                    disableBlinkProp.boolValue = !disableBlinkProp.boolValue;
                    gesture.serializedObject.ApplyModifiedProperties();
                });
                advMenu.AddItem(new GUIContent("Customize transition time"), customTransitionTimeProp.boolValue, () => {
                    customTransitionTimeProp.boolValue = !customTransitionTimeProp.boolValue;
                    gesture.serializedObject.ApplyModifiedProperties();
                });
                advMenu.AddItem(new GUIContent("Add 'Gesture Lock' toggle to menu"), enableLockMenuItemProp.boolValue, () => {
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
            }) {
                text = "Options",
                style = { flexBasis = 70 }
            };

            row.Add(button);
            
            wrapper.Add(VRCFuryEditorUtils.RefreshOnChange(() => {
                var w = new VisualElement();
                if (disableBlinkProp.boolValue) w.Add(new Label("Blinking disabled when active") { style = { marginLeft = 2 }});
                if (customTransitionTimeProp.boolValue) w.Add(new PropertyField(transitionTimeProp, "Custom transition time (s)"));
                if (enableLockMenuItemProp.boolValue) w.Add(new PropertyField(lockMenuItemProp, "Lock menu item path"));
                if (enableExclusiveTagProp.boolValue) w.Add(new PropertyField(exclusiveTagProp, "Exclusive Tag"));
                if (enableWeightProp.boolValue) w.Add(new Label("Use gesture weight (fist only)") { style = { marginLeft = 2 }});
                return w;
            }, disableBlinkProp, customTransitionTimeProp, enableLockMenuItemProp, enableExclusiveTagProp, enableWeightProp));
            
            return wrapper;
        }

        public override bool AvailableOnProps() {
            return false;
        }
    }
}