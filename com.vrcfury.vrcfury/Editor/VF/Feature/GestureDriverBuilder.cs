using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.UIElements;
using VF.Builder;
using VF.Feature.Base;
using VF.Injector;
using VF.Inspector;
using VF.Model.Feature;
using VF.Plugin;
using VF.Service;

namespace VF.Feature {
    public class GestureDriverBuilder : FeatureBuilder<GestureDriver> {
        private int i = 0;
        private readonly Dictionary<string, VFABool> lockMenuItems = new Dictionary<string, VFABool>();
        private readonly Dictionary<string, VFACondition> excludeConditions = new Dictionary<string, VFACondition>();
        [VFAutowired] private readonly ParamSmoothingPlugin smoothing;
        [VFAutowired] private readonly ActionClipService actionClipService;
        
        [FeatureBuilderAction]
        public void Apply() {
            foreach (var gesture in model.gestures) {
                MakeGesture(gesture);
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
            var name = "Gesture " + uniqueNum + " - " + hand + " " + gesture.sign;
            if (hand == GestureDriver.Hand.COMBO) {
                name += " " + gesture.comboSign;
            }
            var uid = "gesture_" + uniqueNum;

            var layer = fx.NewLayer(name);
            var off = layer.NewState("Off");
            var on = layer.NewState("On");

            VFABool lockMenuParam = null;
            if (gesture.enableLockMenuItem && !string.IsNullOrWhiteSpace(gesture.lockMenuItem)) {
                if (!lockMenuItems.TryGetValue(gesture.lockMenuItem, out lockMenuParam)) {
                    lockMenuParam = fx.NewBool(uid + "_lock", synced: true);
                    manager.GetMenu().NewMenuToggle(gesture.lockMenuItem, lockMenuParam);
                    lockMenuItems[gesture.lockMenuItem] = lockMenuParam;
                }
            }

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
            
            var clip = actionClipService.LoadState(uid, gesture.state);
            if (gesture.enableWeight && weightHand > 0) {
                MakeWeightParams();
                var weightParam = weightHand == 1 ? leftWeightParam : rightWeightParam;
                var tree = fx.NewBlendTree(uid + "_blend");
                tree.blendType = BlendTreeType.Simple1D;
                tree.useAutomaticThresholds = false;
                tree.blendParameter = weightParam.Name();
                tree.AddChild(fx.GetEmptyClip(), 0);
                tree.AddChild(clip, 1);
                on.WithAnimation(tree);
            } else {
                on.WithAnimation(clip);
            }

            var transitionTime = gesture.customTransitionTime && gesture.transitionTime >= 0 ? gesture.transitionTime : 0.1f;
            off.TransitionsTo(on).WithTransitionDurationSeconds(transitionTime).When(onCondition);
            on.TransitionsTo(off).WithTransitionDurationSeconds(transitionTime).When(onCondition.Not());
        }

        private VFAFloat leftWeightParam;
        private VFAFloat rightWeightParam;
        private void MakeWeightParams() {
            if (leftWeightParam != null) return;
            var fx = GetFx();
            var GestureLeftWeight = fx.GestureLeftWeight();
            var GestureRightWeight = fx.GestureRightWeight();
            var GestureLeftCondition = fx.GestureLeft().IsEqualTo(1);
            var GestureRightCondition = fx.GestureRight().IsEqualTo(1);
            leftWeightParam = MakeWeightLayer(GestureLeftWeight, GestureLeftCondition);
            rightWeightParam = MakeWeightLayer(GestureRightWeight, GestureRightCondition);
        }
        private VFAFloat MakeWeightLayer(VFAFloat input, VFACondition whenEnabled) {
            var maintained =
                smoothing.SetValueWithConditions(
                    $"{input.Name()}Maintained",
                    0, 1, 0,
                    (input, whenEnabled),
                    (null, null)
                );
            return smoothing.Smooth($"{input.Name()}Smoothed", maintained, 0.2f);
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
                    leftBox.style.flexShrink = 1;
                    w.Add(leftBox);
                    w.Add(new Label("R") { style = { flexBasis = 10 }});
                    rightBox.style.flexGrow = 1;
                    rightBox.style.flexShrink = 1;
                    w.Add(rightBox);
                } else {
                    leftBox.style.flexGrow = 1;
                    leftBox.style.flexShrink = 1;
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
                if (customTransitionTimeProp.boolValue) w.Add(VRCFuryEditorUtils.Prop(transitionTimeProp, "Custom transition time (seconds)"));
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