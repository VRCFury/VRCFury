using System;
using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;
using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using VRC.SDK3.Avatars.Components;
using AnimatorAsCode.V0;
using UnityEditor.UIElements;
using UnityEngine.UIElements;
using UnityEditorInternal;

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
}

[CustomPropertyDrawer(typeof(SenkyFXPropMode))]
public class SenkyFXPropModeDrawer : BetterPropertyDrawer {
    protected override void render(SerializedProperty prop, GUIContent label) {
        renderProp(prop.FindPropertyRelative("state"), label.text);
    }
}

[CustomPropertyDrawer(typeof(SenkyFXProp))]
public class SenkyFXPropDrawer : PropertyDrawer {
    public override VisualElement CreatePropertyGUI(SerializedProperty prop) {
        var wrapper = new VisualElement();

        var innerWrapper = new VisualElement();
        wrapper.Add(innerWrapper);
        innerWrapper.Add(render(prop));
        Action update = () => {
            innerWrapper.Clear();
            innerWrapper.Add(render(prop));
            innerWrapper.Bind(prop.serializedObject);
        };

        wrapper.Add(SenkyUIHelper.OnChange<string>(prop.FindPropertyRelative("type"), update));
        wrapper.Add(SenkyUIHelper.OnChange<bool>(prop.FindPropertyRelative("saved"), update));
        wrapper.Add(SenkyUIHelper.OnChange<bool>(prop.FindPropertyRelative("slider"), update));
        wrapper.Add(SenkyUIHelper.OnChange<bool>(prop.FindPropertyRelative("lewdLocked"), update));
        wrapper.Add(SenkyUIHelper.OnChange<bool>(prop.FindPropertyRelative("defaultOn"), update));
        wrapper.Add(SenkyUIHelper.OnSizeChange(prop.FindPropertyRelative("resetPhysbones"), update));

        return wrapper;
    }

    private VisualElement render(SerializedProperty prop) {
        var form = new SenkyUIHelper(prop);

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
                form.Save();
            });
        }
        if (showSlider) {
            var boolProp = prop.FindPropertyRelative("slider");
            if (boolProp.boolValue) tags.Add("Slider");
            advMenu.AddItem(new GUIContent("Use Slider Wheel"), boolProp.boolValue, () => {
                boolProp.boolValue = !boolProp.boolValue;
                form.Save();
            });
        }
        if (showLewd) {
            var boolProp = prop.FindPropertyRelative("lewdLocked");
            if (boolProp.boolValue) tags.Add("Lewd");
            advMenu.AddItem(new GUIContent("Protect with Lewd Safety"), boolProp.boolValue, () => {
                boolProp.boolValue = !boolProp.boolValue;
                form.Save();
            });
        }
        if (showDefaultOn) {
            var boolProp = prop.FindPropertyRelative("defaultOn");
            if (boolProp.boolValue) tags.Add("Default On");
            advMenu.AddItem(new GUIContent("Default On"), boolProp.boolValue, () => {
                boolProp.boolValue = !boolProp.boolValue;
                form.Save();
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
        form.Add(flex);

        var name = new PropertyField(prop.FindPropertyRelative("name"));
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
        form.Add(content);

        var tagsStr = String.Join(" | ", tags.ToArray());
        if (tagsStr != "") {
            content.Add(new Label(tagsStr));
        }

        if (type == SenkyFXProp.TOGGLE) {
            content.Add(new PropertyField(prop.FindPropertyRelative("state")));
        } else if (type == SenkyFXProp.MODES) {
            content.Add(form.List("modes"));
        } else {
            content.Add(new Label("Unknown type: " + type));
        }

        if (showResetPhysbones && resetPhysboneProp.arraySize > 0) {
            content.Add(new Label("Reset PhysBones:"));
            content.Add(form.List("resetPhysbones"));
        }

        return form.Render();
    }
}

[Serializable]
public class SenkyFXProps {
    public List<SenkyFXProp> props = new List<SenkyFXProp>();
}

[CustomPropertyDrawer(typeof(SenkyFXProps))]
public class SenkyFXPropsDrawer : PropertyDrawer {
    public override VisualElement CreatePropertyGUI(SerializedProperty prop) {
        var form = new SenkyUIHelper(prop);
        form.Add(form.List("props", newItem));
        return form.Render();
    }

    private void newItem(SerializedProperty list, Action<Action<SerializedProperty>> add) {
        var menu = new GenericMenu();
        menu.AddItem(new GUIContent("Toggle"), false, () => {
            add(entry => entry.FindPropertyRelative("type").stringValue = SenkyFXProp.TOGGLE);
        });
        menu.AddItem(new GUIContent("Multi-Mode"), false, () => {
            add(entry => entry.FindPropertyRelative("type").stringValue = SenkyFXProp.MODES);
        });
        menu.AddItem(new GUIContent("Puppet"), false, () => {
            add(entry => entry.FindPropertyRelative("type").stringValue = SenkyFXProp.PUPPET);
        });
        menu.ShowAsContext();
    }
}
