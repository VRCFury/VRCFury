#if UNITY_EDITOR

using System;
using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

[Serializable]
public class SenkyFXProp {
    public const string TOGGLE = "toggle";
    public const string MODES = "modes";
    public const string PUPPET = "puppet";

    public string type;
    public string name;
    public SenkyFXState state;
    public bool saved;
    public bool slider;
    public bool lewdLocked;
    public bool defaultOn;
    public List<SenkyFXPropPuppetStop> puppetStops = new List<SenkyFXPropPuppetStop>();
    public List<SenkyFXPropMode> modes = new List<SenkyFXPropMode>();
    public List<GameObject> resetPhysbones = new List<GameObject>();

    public bool ResetMePlease;
}

[Serializable]
public class SenkyFXPropPuppetStop {
    public float x;
    public float y;
    public SenkyFXState state;
    public SenkyFXPropPuppetStop(float x, float y, SenkyFXState state) {
        this.x = x;
        this.y = y;
        this.state = state;
    }
}

[Serializable]
public class SenkyFXPropMode {
    public SenkyFXState state;
    public SenkyFXPropMode(SenkyFXState state) {
        this.state = state;
    }

    public static VisualElement render(SerializedProperty prop, string label) {
        return SenkyFXState.render(prop.FindPropertyRelative("state"));
    }
}

[CustomPropertyDrawer(typeof(SenkyFXProp))]
public class SenkyFXPropDrawer : PropertyDrawer {
    public override VisualElement CreatePropertyGUI(SerializedProperty prop) {
        return SenkyUIHelper.RefreshOnChange(() => render(prop),
            prop.FindPropertyRelative("type"),
            prop.FindPropertyRelative("saved"),
            prop.FindPropertyRelative("slider"),
            prop.FindPropertyRelative("lewdLocked"),
            prop.FindPropertyRelative("defaultOn"),
            prop.FindPropertyRelative("resetPhysbones")
        );
    }

    private VisualElement render(SerializedProperty prop) {
        var container = new VisualElement();

        var showSaved = false;
        var showSlider = false;
        var showLewd = false;
        var showDefaultOn = false;
        var showResetPhysbones = false;

        var type = prop.FindPropertyRelative("type").stringValue;
        if (type == SenkyFXProp.TOGGLE) {
            showSaved = true;
            showSlider = true;
            showLewd = true;
            showDefaultOn = true;
            showResetPhysbones = true;
        } else if (type == SenkyFXProp.MODES) {
            showSaved = true;
            showLewd = true;
            showResetPhysbones = true;
        }

        List<string> tags = new List<string>();
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
        if (showLewd) {
            var boolProp = prop.FindPropertyRelative("lewdLocked");
            if (boolProp.boolValue) tags.Add("Lewd");
            advMenu.AddItem(new GUIContent("Protect with Lewd Safety"), boolProp.boolValue, () => {
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
                SenkyUIHelper.addToList(resetPhysboneProp);
            });
        }

        var flex = new VisualElement();
        flex.style.flexDirection = FlexDirection.Row;
        flex.style.alignItems = Align.FlexStart;
        flex.style.marginBottom = 10;
        container.Add(flex);

        var name = SenkyUIHelper.PropWithoutLabel(prop.FindPropertyRelative("name"));
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

        var tagsStr = String.Join(" | ", tags.ToArray());
        if (tagsStr != "") {
            content.Add(new Label(tagsStr));
        }

        if (type == SenkyFXProp.TOGGLE) {
            content.Add(SenkyFXState.render(prop.FindPropertyRelative("state")));
        } else if (type == SenkyFXProp.MODES) {
            content.Add(SenkyUIHelper.List(prop.FindPropertyRelative("modes"), renderElement: (i,e) => SenkyFXPropMode.render(e, "Mode " + (i+1))));
        } else {
            content.Add(new Label("Unknown type: " + type));
        }

        if (showResetPhysbones && resetPhysboneProp.arraySize > 0) {
            content.Add(new Label("Reset PhysBones:"));
            content.Add(SenkyUIHelper.List(prop.FindPropertyRelative("resetPhysbones"), renderElement: (i,el) => SenkyUIHelper.PropWithoutLabel(el)));
        }

        return container;
    }
}

[Serializable]
public class SenkyFXProps {
    public List<SenkyFXProp> props = new List<SenkyFXProp>();
}

[CustomPropertyDrawer(typeof(SenkyFXProps))]
public class SenkyFXPropsDrawer : PropertyDrawer {
    public override VisualElement CreatePropertyGUI(SerializedProperty prop) {
        var listProp = prop.FindPropertyRelative("props");
        var inspect = new VisualElement();
        inspect.Add(SenkyUIHelper.List(listProp, onPlus: () => {
            var menu = new GenericMenu();
            menu.AddItem(new GUIContent("Toggle"), false, () => {
                SenkyUIHelper.addToList(listProp, entry => entry.FindPropertyRelative("type").stringValue = SenkyFXProp.TOGGLE);
            });
            menu.AddItem(new GUIContent("Multi-Mode"), false, () => {
                SenkyUIHelper.addToList(listProp, entry => entry.FindPropertyRelative("type").stringValue = SenkyFXProp.MODES);
            });
            menu.AddItem(new GUIContent("Puppet"), false, () => {
                SenkyUIHelper.addToList(listProp, entry => entry.FindPropertyRelative("type").stringValue = SenkyFXProp.PUPPET);
            });
            menu.ShowAsContext();
        }));
        return inspect;
    }
}

#endif
