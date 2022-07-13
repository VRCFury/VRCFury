using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using VF.Feature.Base;
using VF.Inspector;
using VF.Model.Feature;
using Toggle = VF.Model.Feature.Toggle;

namespace VF.Feature {

public class ToggleBuilder : FeatureBuilder<Toggle> {
    [FeatureBuilderAction]
    public void Apply() {
        if (model.slider) {
            var stops = new List<Puppet.Stop> {
                new Puppet.Stop(1,0,model.state)
            };
            var puppet = new Puppet {
                name = model.name,
                saved = model.saved,
                slider = true,
                stops = stops,
            };
            addOtherFeature(puppet);
            return;
        }

        var physBoneResetter = CreatePhysBoneResetter(model.resetPhysbones, model.name);

        var layerName = model.name;
        var layer = controller.NewLayer(layerName);
        var clip = LoadState(model.name, model.state);
        var off = layer.NewState("Off");
        var on = layer.NewState("On").WithAnimation(clip);
        var param = controller.NewBool(model.name, synced: true, saved: model.saved, def: model.defaultOn);
        if (model.securityEnabled) {
            var paramSecuritySync = controller.NewBool("SecurityLockSync");
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
        menu.NewMenuToggle(model.name, param);
    }

    public override string GetEditorTitle() {
        return "Toggleable Prop";
    }

    public override VisualElement CreateEditor(SerializedProperty prop) {
        return CreateEditor(prop, true, true, content => content.Add(VRCFuryStateEditor.render(prop.FindPropertyRelative("state"))));
    }

    public static VisualElement CreateEditor(SerializedProperty prop, bool allowSlider, bool alloDefaultOn, Action<VisualElement> renderBody) {
        var container = new VisualElement();

        var savedProp = prop.FindPropertyRelative("saved");
        var sliderProp = prop.FindPropertyRelative("slider");
        var securityEnabledProp = prop.FindPropertyRelative("securityEnabled");
        var defaultOnProp = prop.FindPropertyRelative("defaultOn");
        var resetPhysboneProp = prop.FindPropertyRelative("resetPhysbones");

        var flex = new VisualElement {
            style = {
                flexDirection = FlexDirection.Row,
                alignItems = Align.FlexStart,
                marginBottom = 10
            }
        };
        container.Add(flex);

        var name = VRCFuryEditorUtils.PropWithoutLabel(prop.FindPropertyRelative("name"));
        name.style.flexGrow = 1;
        flex.Add(name);

        var button = new Button(() => {
            var advMenu = new GenericMenu();
            if (savedProp != null) {
                advMenu.AddItem(new GUIContent("Saved Between Worlds"), savedProp.boolValue, () => {
                    savedProp.boolValue = !savedProp.boolValue;
                    prop.serializedObject.ApplyModifiedProperties();
                });
            }
            if (sliderProp != null) {
                advMenu.AddItem(new GUIContent("Use Slider Wheel"), sliderProp.boolValue, () => {
                    sliderProp.boolValue = !sliderProp.boolValue;
                    prop.serializedObject.ApplyModifiedProperties();
                });
            }
            if (securityEnabledProp != null) {
                advMenu.AddItem(new GUIContent("Protect with Security"), securityEnabledProp.boolValue, () => {
                    securityEnabledProp.boolValue = !securityEnabledProp.boolValue;
                    prop.serializedObject.ApplyModifiedProperties();
                });
            }
            if (defaultOnProp != null) {
                advMenu.AddItem(new GUIContent("Default On"), defaultOnProp.boolValue, () => {
                    defaultOnProp.boolValue = !defaultOnProp.boolValue;
                    prop.serializedObject.ApplyModifiedProperties();
                });
            }
            if (resetPhysboneProp != null) {
                advMenu.AddItem(new GUIContent("Add PhysBone to Reset"), false, () => {
                    VRCFuryEditorUtils.AddToList(resetPhysboneProp);
                });
            }
            advMenu.ShowAsContext();
        }) {
            text = "*",
            style = {
                flexGrow = 0
            }
        };
        flex.Add(button);

        var content = new VisualElement();
        //content.style.paddingLeft = 20;
        container.Add(content);

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
            var tagsStr = string.Join(" | ", tags.ToArray());
            if (tagsStr != "") {
                return new Label(tagsStr);
            }

            return new VisualElement();
        },
            savedProp,
            sliderProp,
            securityEnabledProp,
            defaultOnProp
        ));

        renderBody(content);

        if (resetPhysboneProp != null) {
            content.Add(VRCFuryEditorUtils.RefreshOnChange(() => {
                var c = new VisualElement();
                if (resetPhysboneProp.arraySize > 0) {
                    c.Add(new Label("Reset PhysBones:"));
                    c.Add(VRCFuryEditorUtils.List(prop.FindPropertyRelative("resetPhysbones"), renderElement: (i,el) => VRCFuryEditorUtils.PropWithoutLabel(el)));
                }
                return c;
            }, resetPhysboneProp));
        }

        return container;
    }
}

}


