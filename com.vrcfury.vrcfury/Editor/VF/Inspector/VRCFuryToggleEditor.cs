using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using VF.Component;

namespace VF.Inspector {
    [CustomEditor(typeof(VRCFuryToggle))]
    public class VRCFuryToggleEditor : VRCFuryComponentEditor<VRCFuryToggle> {


        public override VisualElement CreateEditor(SerializedObject serializedObject, VRCFuryToggle target) {
            var content = new VisualElement();

            VRCFuryEditorUtils.Padding(content, 5);
            content.style.marginBottom = -5;

            var savedProp = serializedObject.FindProperty("saved");
            var sliderProp = serializedObject.FindProperty("slider");
            var securityEnabledProp = serializedObject.FindProperty("securityEnabled");
            var defaultOnProp = serializedObject.FindProperty("defaultOn");
            var includeInRestProp = serializedObject.FindProperty("includeInRest");
            var exclusiveOffStateProp = serializedObject.FindProperty("exclusiveOffState");
            var enableExclusiveTagProp = serializedObject.FindProperty("enableExclusiveTag");
            var resetPhysboneProp = serializedObject.FindProperty("resetPhysbones");
            var enableIconProp = serializedObject.FindProperty("enableIcon");
            var enableDriveGlobalParamProp = serializedObject.FindProperty("enableDriveGlobalParam");
            var separateLocalProp = serializedObject.FindProperty("separateLocal");
            var hasTransitionProp = serializedObject.FindProperty("hasTransition");
            var simpleOutTransitionProp = serializedObject.FindProperty("simpleOutTransition");
            var defaultSliderProp = serializedObject.FindProperty("defaultSliderValue");
            var holdButtonProp = serializedObject.FindProperty("holdButton");

            var flex = new VisualElement {
                style = {
                    flexDirection = FlexDirection.Row,
                    alignItems = Align.FlexStart
                }
            };
            content.Add(flex);

            var name = VRCFuryEditorUtils.Prop(serializedObject.FindProperty("name"), "Menu Path");
            name.style.flexGrow = 1;
            flex.Add(name);

            var button = VRCFuryEditorUtils.Button("Options", () => {
                var advMenu = new GenericMenu();
                if (savedProp != null) {
                    advMenu.AddItem(new GUIContent("Saved Between Worlds"), savedProp.boolValue, () => {
                        savedProp.boolValue = !savedProp.boolValue;
                        serializedObject.ApplyModifiedProperties();
                    });
                }

                if (sliderProp != null) {
                    advMenu.AddItem(new GUIContent("Use Slider Wheel"), sliderProp.boolValue, () => {
                        sliderProp.boolValue = !sliderProp.boolValue;
                        serializedObject.ApplyModifiedProperties();
                    });
                }

                if (securityEnabledProp != null) {
                    advMenu.AddItem(new GUIContent("Protect with Security"), securityEnabledProp.boolValue, () => {
                        securityEnabledProp.boolValue = !securityEnabledProp.boolValue;
                        serializedObject.ApplyModifiedProperties();
                    });
                }

                if (defaultOnProp != null) {
                    advMenu.AddItem(new GUIContent("Default On"), defaultOnProp.boolValue, () => {
                        defaultOnProp.boolValue = !defaultOnProp.boolValue;
                        serializedObject.ApplyModifiedProperties();
                    });
                }

                if (includeInRestProp != null) {
                    advMenu.AddItem(new GUIContent("Show in Rest Pose"), includeInRestProp.boolValue, () => {
                        includeInRestProp.boolValue = !includeInRestProp.boolValue;
                        serializedObject.ApplyModifiedProperties();
                    });
                }

                if (resetPhysboneProp != null) {
                    advMenu.AddItem(new GUIContent("Add PhysBone to Reset"), false, () => {
                        VRCFuryEditorUtils.AddToList(resetPhysboneProp);
                    });
                }

                if (enableExclusiveTagProp != null) {
                    advMenu.AddItem(new GUIContent("Enable Exclusive Tags"), enableExclusiveTagProp.boolValue, () => {
                        enableExclusiveTagProp.boolValue = !enableExclusiveTagProp.boolValue;
                        serializedObject.ApplyModifiedProperties();
                    });
                }
                
                if (exclusiveOffStateProp != null) {
                    advMenu.AddItem(new GUIContent("This is Exclusive Off State"), exclusiveOffStateProp.boolValue, () => {
                        exclusiveOffStateProp.boolValue = !exclusiveOffStateProp.boolValue;
                        serializedObject.ApplyModifiedProperties();
                    });
                }

                if (enableIconProp != null) {
                    advMenu.AddItem(new GUIContent("Set Custom Menu Icon"), enableIconProp.boolValue, () => {
                        enableIconProp.boolValue = !enableIconProp.boolValue;
                        serializedObject.ApplyModifiedProperties();
                    });
                }
                
                if (enableDriveGlobalParamProp != null) {
                    advMenu.AddItem(new GUIContent("Drive a Global Parameter"), enableDriveGlobalParamProp.boolValue, () => {
                        enableDriveGlobalParamProp.boolValue = !enableDriveGlobalParamProp.boolValue;
                        serializedObject.ApplyModifiedProperties();
                    });
                }

                if (separateLocalProp != null)
                {
                    advMenu.AddItem(new GUIContent("Separate Local State"), separateLocalProp.boolValue, () => {
                        separateLocalProp.boolValue = !separateLocalProp.boolValue;
                        serializedObject.ApplyModifiedProperties();
                    });
                }

                if (hasTransitionProp != null)
                {
                    advMenu.AddItem(new GUIContent("Enable Transition State"), hasTransitionProp.boolValue, () => {
                        hasTransitionProp.boolValue = !hasTransitionProp.boolValue;
                        serializedObject.ApplyModifiedProperties();
                    });
                }

                if (holdButtonProp != null)
                {
                    advMenu.AddItem(new GUIContent("Hold Button"), holdButtonProp.boolValue, () => {
                        holdButtonProp.boolValue = !holdButtonProp.boolValue;
                        serializedObject.ApplyModifiedProperties();
                    });
                }

                advMenu.ShowAsContext();
            });
            button.style.flexGrow = 0;
            flex.Add(button);

            content.Add(VRCFuryStateEditor.render(serializedObject.FindProperty("state")));

            if (resetPhysboneProp != null) {
                content.Add(VRCFuryEditorUtils.RefreshOnChange(() => {
                    var c = new VisualElement();
                    if (resetPhysboneProp.arraySize > 0) {
                        c.Add(VRCFuryEditorUtils.WrappedLabel("Reset PhysBones:"));
                        c.Add(VRCFuryEditorUtils.List(serializedObject.FindProperty("resetPhysbones")));
                    }
                    return c;
                }, resetPhysboneProp));
            }

            if (enableExclusiveTagProp != null) {
                content.Add(VRCFuryEditorUtils.RefreshOnChange(() => {
                    var c = new VisualElement();
                    if (enableExclusiveTagProp.boolValue) {
                        c.Add(VRCFuryEditorUtils.Prop(serializedObject.FindProperty("exclusiveTag"), "Exclusive Tags"));
                    }
                    return c;
                }, enableExclusiveTagProp));
            }

            content.Add(VRCFuryEditorUtils.RefreshOnChange(() => {
                var c = new VisualElement();
                if (sliderProp.boolValue && defaultOnProp.boolValue) {
                    c.Add(VRCFuryEditorUtils.Prop(serializedObject.FindProperty("defaultSliderValue"), "Default Value"));
                }
                return c;
            }, sliderProp, defaultOnProp));
            
            if (enableIconProp != null) {
                content.Add(VRCFuryEditorUtils.RefreshOnChange(() => {
                    var c = new VisualElement();
                    if (enableIconProp.boolValue) {
                        c.Add(VRCFuryEditorUtils.Prop(serializedObject.FindProperty("icon"), "Menu Icon"));
                    }
                    return c;
                }, enableIconProp));
            }

            if (enableDriveGlobalParamProp != null) {
                content.Add(VRCFuryEditorUtils.RefreshOnChange(() => {
                    var c = new VisualElement();
                    if (enableDriveGlobalParamProp.boolValue) {
                        c.Add(VRCFuryEditorUtils.Prop(serializedObject.FindProperty("driveGlobalParam"), "Drive Global Param"));
                        c.Add(VRCFuryEditorUtils.Warn(
                            "Warning, Drive Global Param is an advanced feature. The driven parameter should not be placed in a menu " +
                            "or controlled by any other driver or shared with any other toggle. It should only be used as an input to " +
                            "manually-created state transitions in your avatar. This should NEVER be used on vrcfury props, as any merged " +
                            "full controllers will have their parameters rewritten."));
                    }
                    return c;
                }, enableDriveGlobalParamProp));
            }

            if (separateLocalProp != null)
            {
                content.Add(VRCFuryEditorUtils.RefreshOnChange(() => {
                    var c = new VisualElement();
                    if (separateLocalProp.boolValue)
                    {
                        c.Add(VRCFuryEditorUtils.Prop(serializedObject.FindProperty("localState"), "Local State"));
                    }
                    return c;
                }, separateLocalProp));
            }

            content.Add(VRCFuryEditorUtils.RefreshOnChange(() => {
                var c = new VisualElement();
                if (hasTransitionProp.boolValue)
                {
                    c.Add(VRCFuryEditorUtils.Prop(serializedObject.FindProperty("transitionStateIn"), "Transition In"));

                    if (!simpleOutTransitionProp.boolValue)
                        c.Add(VRCFuryEditorUtils.Prop(serializedObject.FindProperty("transitionStateOut"), "Transition Out"));
                }
                return c;
            }, hasTransitionProp, simpleOutTransitionProp));

            content.Add(VRCFuryEditorUtils.RefreshOnChange(() => {
                var c = new VisualElement();
                if (separateLocalProp.boolValue && hasTransitionProp.boolValue)
                {
                    c.Add(VRCFuryEditorUtils.Prop(serializedObject.FindProperty("localTransitionStateIn"), "Local Trans. In"));

                    if (!simpleOutTransitionProp.boolValue)
                        c.Add(VRCFuryEditorUtils.Prop(serializedObject.FindProperty("localTransitionStateOut"), "Local Trans. Out"));
                        
                }
                return c;
            }, separateLocalProp, hasTransitionProp, simpleOutTransitionProp));

            content.Add(VRCFuryEditorUtils.RefreshOnChange(() => {
                var c = new VisualElement();
                if (hasTransitionProp.boolValue)
                {
                    c.Add(VRCFuryEditorUtils.Prop(serializedObject.FindProperty("simpleOutTransition"), "Transition Out is reverse of Transition In"));
                }
                return c;
            }, hasTransitionProp));

            // Tags
            content.Add(VRCFuryEditorUtils.RefreshOnChange(() => {
                    var tags = new List<string>();
                    if (savedProp != null && savedProp.boolValue)
                        tags.Add("Saved");
                    if (sliderProp != null && sliderProp.boolValue)
                        tags.Add("Slider");
                    if (securityEnabledProp != null && securityEnabledProp.boolValue)
                        tags.Add("Security");
                    if (defaultOnProp != null && defaultOnProp.boolValue)
                        tags.Add("Default On");
                    if (includeInRestProp != null && includeInRestProp.boolValue)
                        tags.Add("Shown in Rest Pose");
                    if (exclusiveOffStateProp != null && exclusiveOffStateProp.boolValue)
                        tags.Add("This is the Exclusive Off State");
                    if (holdButtonProp != null && holdButtonProp.boolValue)
                        tags.Add("Hold Button");

                    var row = new VisualElement();
                    row.style.flexWrap = Wrap.Wrap;
                    row.style.flexDirection = FlexDirection.Row;
                    foreach (var tag in tags) {
                        var flag = new Label(tag);
                        flag.style.width = StyleKeyword.Auto;
                        flag.style.backgroundColor = new Color(1f, 1f, 1f, 0.1f);
                        flag.style.borderTopRightRadius = 5;
                        flag.style.marginRight = 5;
                        VRCFuryEditorUtils.Padding(flag, 2, 4);
                        row.Add(flag);
                    }

                    return row;
                },
                savedProp,
                sliderProp,
                securityEnabledProp,
                defaultOnProp,
                includeInRestProp,
                exclusiveOffStateProp
            ));

            return content;
        }
    }
}
