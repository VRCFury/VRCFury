using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using VF.Feature.Base;
using VF.Injector;
using VF.Inspector;
using VF.Model.Feature;
using VF.Service;
using VF.Utils;
using VF.Utils.Controller;

namespace VF.Feature {

    [FeatureTitle("Gestures")]
    internal class GestureDriverBuilder : FeatureBuilder<GestureDriver> {
        private int i = 0;
        private readonly Dictionary<string, VFABool> lockMenuItems = new Dictionary<string, VFABool>();
        private readonly Dictionary<string, VFCondition> excludeConditions = new Dictionary<string, VFCondition>();
        [VFAutowired] private readonly SmoothingService smoothing;
        [VFAutowired] private readonly ActionClipService actionClipService;
        [VFAutowired] private readonly ControllersService controllers;
        private ControllerManager fx => controllers.GetFx();
        [VFAutowired] private readonly MenuService menuService;
        private MenuManager menu => menuService.GetMenu();
        [VFAutowired] private readonly GlobalsService globals;
        [VFAutowired] private readonly DbtLayerService dbtLayerService;

        [FeatureBuilderAction]
        public void Apply() {
            foreach (var gesture in model.gestures) {
                MakeGesture(gesture);
            }
        }

        private void MakeGesture(GestureDriver.Gesture gesture, GestureDriver.Hand handOverride = GestureDriver.Hand.EITHER) {
            var hand = handOverride == GestureDriver.Hand.EITHER ? gesture.hand : handOverride;

            var uniqueNum = i++;
            var name = "Gesture " + uniqueNum + " - " + hand + " " + gesture.sign;
            if (hand == GestureDriver.Hand.COMBO) {
                name += " " + gesture.comboSign;
            }
            var uid = "gesture_" + uniqueNum;
            var uidExit = "gesture_" + uniqueNum + "_exit";

            var layer = fx.NewLayer(name);
            var off = layer.NewState("Off");
            var on = layer.NewState("On");

            var hasExit = gesture.exitState.actions.Count > 0;
            VFState exit = null;
            if (hasExit) {
                exit = layer.NewState("Exit");
            }

            VFABool lockMenuParam = null;
            if (gesture.enableLockMenuItem && !string.IsNullOrWhiteSpace(gesture.lockMenuItem)) {
                if (!lockMenuItems.TryGetValue(gesture.lockMenuItem, out lockMenuParam)) {
                    lockMenuParam = fx.NewBool(uid + "_lock", synced: true);
                    menu.NewMenuToggle(gesture.lockMenuItem, lockMenuParam);
                    lockMenuItems[gesture.lockMenuItem] = lockMenuParam;
                }
            }

            var directTree = new Lazy<VFBlendTreeDirect>(() => dbtLayerService.Create());
            var blendtreeMath = new Lazy<BlendtreeMath>(() => dbtLayerService.GetMath(directTree.Value));
            void MakeHand(bool right, ref VFCondition aggCondition, ref VFAFloat aggWeight) {
                if (hand == GestureDriver.Hand.LEFT && right) return;
                if (hand == GestureDriver.Hand.RIGHT && !right) return;
                var sign = (right && hand == GestureDriver.Hand.COMBO) ? gesture.comboSign : gesture.sign;

                var myCondition = (right ? fx.GestureRight() : fx.GestureLeft()).IsEqualTo((int)sign);
                if (aggCondition == null) aggCondition = myCondition;
                else if (hand == GestureDriver.Hand.EITHER) aggCondition = aggCondition.Or(myCondition);
                else if (hand == GestureDriver.Hand.COMBO) aggCondition = aggCondition.And(myCondition);

                if (sign == GestureDriver.HandSign.FIST && gesture.enableWeight) {
                    var myWeight = right ? fx.GestureRightWeight() : fx.GestureLeftWeight();
                    if (aggWeight == null) aggWeight = myWeight;
                    else aggWeight = blendtreeMath.Value.Max(aggWeight, myWeight);
                }
            }

            VFCondition onCondition = null;
            VFAFloat weight = null;
            MakeHand(false, ref onCondition, ref weight);
            MakeHand(true, ref onCondition, ref weight);

            var transitionTime = gesture.customTransitionTime && gesture.transitionTime >= 0 ? gesture.transitionTime : 0.1f;

            if (weight != null) {
                var clip = actionClipService.LoadState(uid, gesture.state, motionTime: ActionClipService.MotionTimeMode.Always);
                var smoothedWeight = MakeWeightLayer(
                    directTree.Value,
                    weight,
                    onCondition
                );
                onCondition = smoothedWeight.IsGreaterThan(0.05f);
                transitionTime = 0.05f;
                on.WithAnimation(clip).MotionTime(smoothedWeight);

                if (hasExit) {
                    var exitClip = actionClipService.LoadState(uidExit, gesture.exitState, motionTime: ActionClipService.MotionTimeMode.Always);
                    exit.WithAnimation(exitClip).MotionTime(smoothedWeight);
                }
            } else {
                var clip = actionClipService.LoadState(uid, gesture.state);
                on.WithAnimation(clip);

                if  (hasExit) {
                    var exitClip = actionClipService.LoadState(uidExit, gesture.exitState);
                    exit.WithAnimation(exitClip);
                }
            }

            if (lockMenuParam != null) {
                onCondition = onCondition.Or(lockMenuParam.IsTrue());
            }

            if (gesture.enableExclusiveTag) {
                foreach (var tag in gesture.exclusiveTag.Split(',')) {
                    var trimmedTag = tag.Trim();
                    if (!string.IsNullOrWhiteSpace(trimmedTag)) {
                        var main = globals.allBuildersInRun.OfType<GestureDriverBuilder>().First();
                        if (main.excludeConditions.TryGetValue(trimmedTag, out var excludeCondition)) {
                            main.excludeConditions[trimmedTag] = excludeCondition.Or(onCondition);
                            onCondition = onCondition.And(excludeCondition.Not());
                        } else {
                            main.excludeConditions[trimmedTag] = onCondition;
                        }
                    }
                }
            }

            off.TransitionsTo(on).WithTransitionDurationSeconds(transitionTime).When(onCondition);
            if (!hasExit) {
                on.TransitionsTo(off).WithTransitionDurationSeconds(transitionTime).When(onCondition.Not());
            } else {
                on.TransitionsTo(exit).WithTransitionDurationSeconds(transitionTime).When(onCondition.Not());
                exit.TransitionsTo(off).WithTransitionDurationSeconds(transitionTime).When(onCondition.Not());
            }
         }

        private VFAFloat MakeWeightLayer(VFBlendTreeDirect directTree, VFAFloat input, VFCondition enabled) {
            var layer = fx.NewLayer($"{input.Name()} Target");

            var target = fx.MakeAap($"{input.Name()}/Target", def: input.GetDefault());

            var off = layer.NewState("Off").WithAnimation(target.MakeSetter(0));
            var on = layer.NewState("On").WithAnimation(BlendtreeMath.MakeCopier(input, target));
            off.TransitionsTo(on).When(enabled);
            on.TransitionsTo(off).When(enabled.Not());

            return smoothing.Smooth(directTree, $"{input.Name()}/Smoothed", target, 0.15f);
        }

        [FeatureEditor]
        public static VisualElement Editor(SerializedProperty prop) {
            return VRCFuryEditorUtils.List(prop.FindPropertyRelative("gestures"));
        }

        [CustomPropertyDrawer(typeof(GestureDriver.Gesture))]
        public class GestureDrawer : PropertyDrawer {
            public override VisualElement CreatePropertyGUI(SerializedProperty prop) {
                return RenderGestureEditor(prop);
            }
        }

        private static VisualElement RenderGestureEditor(SerializedProperty gesture) {
            var wrapper = new VisualElement();

            var handProp = gesture.FindPropertyRelative("hand");
            var signProp = gesture.FindPropertyRelative("sign");
            var comboSignProp = gesture.FindPropertyRelative("comboSign");

            var row = new VisualElement().Row();
            row.Add(VRCFuryEditorUtils.Prop(handProp).FlexBasis(70));
            var handSigns = VRCFuryEditorUtils.RefreshOnChange(() => {
                var w = new VisualElement().Row().AlignItems(Align.Center);
                if ((GestureDriver.Hand)handProp.enumValueIndex == GestureDriver.Hand.COMBO) {
                    w.Add(new Label("L").FlexBasis(10).TextAlign(TextAnchor.MiddleCenter));
                    w.Add(VRCFuryEditorUtils.Prop(signProp).FlexGrow(1).FlexShrink(1));
                    w.Add(new Label("R").FlexBasis(10).TextAlign(TextAnchor.MiddleCenter));
                    w.Add(VRCFuryEditorUtils.Prop(comboSignProp).FlexGrow(1).FlexShrink(1));
                } else {
                    w.Add(VRCFuryEditorUtils.Prop(signProp).FlexGrow(1).FlexShrink(1));
                }
                return w;
            }, handProp);
            handSigns.FlexGrow(1);
            row.Add(handSigns);
            wrapper.Add(row);

            var customTransitionTimeProp = gesture.FindPropertyRelative("customTransitionTime");
            var transitionTimeProp = gesture.FindPropertyRelative("transitionTime");
            var enableLockMenuItemProp = gesture.FindPropertyRelative("enableLockMenuItem");
            var lockMenuItemProp = gesture.FindPropertyRelative("lockMenuItem");
            var enableExclusiveTagProp = gesture.FindPropertyRelative("enableExclusiveTag");
            var exclusiveTagProp = gesture.FindPropertyRelative("exclusiveTag");
            var enableWeightProp = gesture.FindPropertyRelative("enableWeight");
            var enableExitProp = gesture.FindPropertyRelative("enableExit");

            if (enableExitProp.boolValue) {
                wrapper.Add(new Label("On enter gesture"));
            }
            wrapper.Add(VRCFuryActionSetDrawer.render(gesture.FindPropertyRelative("state")));
            if (enableExitProp.boolValue) {
                wrapper.Add(new Label("On exit gesture"));
                wrapper.Add(VRCFuryActionSetDrawer.render(gesture.FindPropertyRelative("exitState")));
            }

            row.Add(new Button().Text("Options").FlexBasis(70).OnClick(() => {
                var advMenu = new GenericMenu();
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
                advMenu.AddItem(new GUIContent("Enable gesture exit"), enableExitProp.boolValue, () => {
                    enableExitProp.boolValue = !enableExitProp.boolValue;
                    gesture.serializedObject.ApplyModifiedProperties();
                });
                advMenu.ShowAsContext();
            }));

            wrapper.Add(VRCFuryEditorUtils.RefreshOnChange(() => {
                var w = new VisualElement();
                if (customTransitionTimeProp.boolValue) w.Add(VRCFuryEditorUtils.Prop(transitionTimeProp, "Custom transition time (seconds)"));
                if (enableLockMenuItemProp.boolValue) w.Add(VRCFuryEditorUtils.Prop(lockMenuItemProp, "Lock menu item path"));
                if (enableExclusiveTagProp.boolValue) w.Add(VRCFuryEditorUtils.Prop(exclusiveTagProp, "Exclusive Tag"));
                if (enableWeightProp.boolValue) w.Add(VRCFuryEditorUtils.WrappedLabel("Use gesture weight (fist only)"));
                return w;
            }, customTransitionTimeProp, enableLockMenuItemProp, enableExclusiveTagProp, enableWeightProp, enableExitProp));

            return wrapper;
        }
    }
}
