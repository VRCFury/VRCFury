using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using VF.Builder;
using VF.Feature.Base;
using VF.Inspector;
using VF.Model.Feature;

namespace VF.Feature {
    public class GestureDriverBuilder : FeatureBuilder<GestureDriver> {
        [FeatureBuilderAction]
        public void Apply() {
            var lockMenuItems = new Dictionary<string, VFABool>();
            var excludeConditions = new Dictionary<string, VFACondition>();
            
            var i = 0;
            foreach (var gesture in model.gestures) {
                i++;
                var layer = controller.NewLayer("Gesture - " + i);
                var off = layer.NewState("Off");
                var on = layer.NewState("On").WithAnimation(LoadState("gesture" + i, gesture.state));

                VFABool lockMenuParam = null;
                if (gesture.enableLockMenuItem && !string.IsNullOrWhiteSpace(gesture.lockMenuItem)) {
                    if (!lockMenuItems.TryGetValue(gesture.lockMenuItem, out lockMenuParam)) {
                        // This doesn't actually need synced, but vrc gets annoyed if the menu is using an unsynced param
                        lockMenuParam = controller.NewBool("handGestureLock" + i, synced: true);
                        menu.NewMenuToggle(gesture.lockMenuItem, lockMenuParam);
                        lockMenuItems[gesture.lockMenuItem] = lockMenuParam;
                    }
                }

                var GestureLeft = controller.NewInt("GestureLeft", usePrefix: false);
                var GestureRight = controller.NewInt("GestureRight", usePrefix: false);

                VFACondition onCondition;
                if (gesture.hand == GestureDriver.Hand.LEFT) {
                    onCondition = GestureLeft.IsEqualTo((int)gesture.sign);
                } else if (gesture.hand == GestureDriver.Hand.RIGHT) {
                    onCondition = GestureRight.IsEqualTo((int)gesture.sign);
                } else if (gesture.hand == GestureDriver.Hand.EITHER) {
                    onCondition = GestureLeft.IsEqualTo((int)gesture.sign).Or(GestureRight.IsEqualTo((int)gesture.sign));
                } else if (gesture.hand == GestureDriver.Hand.COMBO) {
                    onCondition = GestureLeft.IsEqualTo((int)gesture.sign).And(GestureRight.IsEqualTo((int)gesture.comboSign));
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
                    var disableBlinkParam = controller.NewBool("gestureDisableBlink" + i);
                    off.Drives(disableBlinkParam, false);
                    on.Drives(disableBlinkParam, true);
                    addOtherFeature(new BlinkingBuilder.BlinkingPrevention { param = disableBlinkParam });
                }
                
                var transitionTime = gesture.customTransitionTime && gesture.transitionTime >= 0 ? gesture.transitionTime : 0.1f;
                off.TransitionsTo(on).WithTransitionDurationSeconds(transitionTime).When(onCondition);
                on.TransitionsTo(off).WithTransitionDurationSeconds(transitionTime).When(onCondition.Not());
            }
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
                //advMenu.AddItem(new GUIContent("Use gesture weight (fist only)"), enableWeightProp.boolValue, () => {
                //    enableWeightProp.boolValue = !enableWeightProp.boolValue;
                //    gesture.serializedObject.ApplyModifiedProperties();
                //});
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