using System.Collections.Generic;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using VRCF.Model;

namespace VRCF.Inspector {

[CustomPropertyDrawer(typeof(VRCFuryProp))]
public class VRCFuryPropDrawer : PropertyDrawer {
    public override VisualElement CreatePropertyGUI(SerializedProperty prop) {
        return VRCFuryEditorUtils.RefreshOnChange(() => render(prop),
            prop.FindPropertyRelative("type"),
            prop.FindPropertyRelative("saved"),
            prop.FindPropertyRelative("slider"),
            prop.FindPropertyRelative("securityEnabled"),
            prop.FindPropertyRelative("defaultOn"),
            prop.FindPropertyRelative("resetPhysbones")
        );
    }

    private VisualElement render(SerializedProperty prop) {
        var container = new VisualElement();

        var showSaved = false;
        var showSlider = false;
        var showSecurity = false;
        var showDefaultOn = false;
        var showResetPhysbones = false;

        var type = prop.FindPropertyRelative("type").stringValue;
        if (type == VRCFuryProp.TOGGLE) {
            showSaved = true;
            showSlider = true;
            showSecurity = true;
            showDefaultOn = true;
            showResetPhysbones = true;
        } else if (type == VRCFuryProp.MODES) {
            showSaved = true;
            showSecurity = true;
            showResetPhysbones = true;
        }

        var tags = new List<string>();
        var advMenu = new GenericMenu();
        if (showSaved) {
            var boolProp = prop.FindPropertyRelative("saved");
            if (boolProp.boolValue) tags.Add("Saved");
            advMenu.AddItem(new GUIContent("Saved Between Worlds"), boolProp.boolValue, () => {
                boolProp.boolValue = !boolProp.boolValue;
                prop.serializedObject.ApplyModifiedProperties();
            });
        }
        if (showSlider) {
            var boolProp = prop.FindPropertyRelative("slider");
            if (boolProp.boolValue) tags.Add("Slider");
            advMenu.AddItem(new GUIContent("Use Slider Wheel"), boolProp.boolValue, () => {
                boolProp.boolValue = !boolProp.boolValue;
                prop.serializedObject.ApplyModifiedProperties();
            });
        }
        if (showSecurity) {
            var boolProp = prop.FindPropertyRelative("securityEnabled");
            if (boolProp.boolValue) tags.Add("Security");
            advMenu.AddItem(new GUIContent("Protect with Security"), boolProp.boolValue, () => {
                boolProp.boolValue = !boolProp.boolValue;
                prop.serializedObject.ApplyModifiedProperties();
            });
        }
        if (showDefaultOn) {
            var boolProp = prop.FindPropertyRelative("defaultOn");
            if (boolProp.boolValue) tags.Add("Default On");
            advMenu.AddItem(new GUIContent("Default On"), boolProp.boolValue, () => {
                boolProp.boolValue = !boolProp.boolValue;
                prop.serializedObject.ApplyModifiedProperties();
            });
        }
        var resetPhysboneProp = prop.FindPropertyRelative("resetPhysbones");
        if (showResetPhysbones) {
            advMenu.AddItem(new GUIContent("Add PhysBone to Reset"), false, () => {
                VRCFuryEditorUtils.AddToList(resetPhysboneProp);
            });
        }

        var flex = new VisualElement();
        flex.style.flexDirection = FlexDirection.Row;
        flex.style.alignItems = Align.FlexStart;
        flex.style.marginBottom = 10;
        container.Add(flex);

        var name = VRCFuryEditorUtils.PropWithoutLabel(prop.FindPropertyRelative("name"));
        name.style.flexGrow = 1;
        flex.Add(name);

        if (advMenu.GetItemCount() > 0) {
            var button = new Button(() => {
                advMenu.ShowAsContext();
            });
            button.text = "*";
            button.style.flexGrow = 0;
            flex.Add(button);
        }

        var content = new VisualElement();
        content.style.paddingLeft = 20;
        container.Add(content);

        var tagsStr = string.Join(" | ", tags.ToArray());
        if (tagsStr != "") {
            content.Add(new Label(tagsStr));
        }

        if (type == VRCFuryProp.TOGGLE) {
            content.Add(VRCFuryStateEditor.render(prop.FindPropertyRelative("state")));
        } else if (type == VRCFuryProp.MODES) {
            content.Add(VRCFuryEditorUtils.List(prop.FindPropertyRelative("modes"), renderElement: (i,e) => VRCFuryPropModeDrawer.render(e, "Mode " + (i+1))));
        } else if (type == VRCFuryProp.CONTROLLER) {
            content.Add(new PropertyField(prop.FindPropertyRelative("controller"), "Controller"));
            content.Add(new PropertyField(prop.FindPropertyRelative("controllerMenu"), "Menu"));
            content.Add(new PropertyField(prop.FindPropertyRelative("controllerParams"), "Params"));
        } else {
            content.Add(new Label("Unknown type: " + type));
        }

        if (showResetPhysbones && resetPhysboneProp.arraySize > 0) {
            content.Add(new Label("Reset PhysBones:"));
            content.Add(VRCFuryEditorUtils.List(prop.FindPropertyRelative("resetPhysbones"), renderElement: (i,el) => VRCFuryEditorUtils.PropWithoutLabel(el)));
        }

        return container;
    }
}

public class VRCFuryPropModeDrawer {
    public static VisualElement render(SerializedProperty prop, string label) {
        return VRCFuryStateEditor.render(prop.FindPropertyRelative("state"));
    }
}

[CustomPropertyDrawer(typeof(VRCFuryProps))]
public class VRCFuryPropsDrawer : PropertyDrawer {
    public override VisualElement CreatePropertyGUI(SerializedProperty prop) {
        var listProp = prop.FindPropertyRelative("props");
        var inspect = new VisualElement();
        inspect.Add(VRCFuryEditorUtils.List(listProp, onPlus: () => {
            var menu = new GenericMenu();
            menu.AddItem(new GUIContent("Toggle"), false, () => {
                VRCFuryEditorUtils.AddToList(listProp, entry => entry.FindPropertyRelative("type").stringValue = VRCFuryProp.TOGGLE);
            });
            menu.AddItem(new GUIContent("Multi-Mode"), false, () => {
                VRCFuryEditorUtils.AddToList(listProp, entry => entry.FindPropertyRelative("type").stringValue = VRCFuryProp.MODES);
            });
            //menu.AddItem(new GUIContent("Puppet"), false, () => {
            //    VRCFuryEditorUtils.AddToList(listProp, entry => entry.FindPropertyRelative("type").stringValue = VRCFuryProp.PUPPET);
            //});
            menu.AddItem(new GUIContent("Full Controller"), false, () => {
                VRCFuryEditorUtils.AddToList(listProp, entry => entry.FindPropertyRelative("type").stringValue = VRCFuryProp.CONTROLLER);
            });
            menu.ShowAsContext();
        }));
        return inspect;
    }
}

}
