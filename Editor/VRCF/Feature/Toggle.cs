using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using VRCF.Inspector;
using VRCF.Model;

namespace VRCF.Feature {

public class Toggle : BaseFeature {
    public void Generate(Model.Feature.Toggle config) {
        if (config.slider) {
            var stops = new List<VRCFuryPropPuppetStop> {
                new VRCFuryPropPuppetStop(1,0,config.state)
            };
            var puppet = new Model.Feature.Puppet {
                name = config.name,
                saved = config.saved,
                slider = true,
                stops = stops,
            };
            addOtherFeature(puppet);
            return;
        }

        var physBoneResetter = CreatePhysBoneResetter(config.resetPhysbones, config.name);

        var layerName = config.name;
        var layer = manager.NewLayer(layerName);
        var clip = loadClip(config.name, config.state, featureBaseObject);
        var off = layer.NewState("Off");
        var on = layer.NewState("On").WithAnimation(clip);
        var param = manager.NewBool(config.name, synced: true, saved: config.saved, def: config.defaultOn);
        if (config.securityEnabled) {
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
        manager.NewMenuToggle(config.name, param);
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


