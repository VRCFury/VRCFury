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
public class SenkyFXPropDrawer : BetterPropertyDrawer {
    private void advancedMenu(SerializedProperty prop) {
        var menu = new GenericMenu();
        menu.ShowAsContext();
        prop.serializedObject.ApplyModifiedProperties();
    }
    protected override void render(SerializedProperty prop, GUIContent label) {
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
            addToMenu(advMenu, "Saved Between Worlds", boolProp.boolValue, () => {
                boolProp.boolValue = !boolProp.boolValue;
            });
        }
        if (showSlider) {
            var boolProp = prop.FindPropertyRelative("slider");
            if (boolProp.boolValue) tags.Add("Slider");
            addToMenu(advMenu, "Use Slider Wheel", boolProp.boolValue, () => {
                boolProp.boolValue = !boolProp.boolValue;
            });
        }
        if (showLewd) {
            var boolProp = prop.FindPropertyRelative("lewdLocked");
            if (boolProp.boolValue) tags.Add("Lewd");
            addToMenu(advMenu, "Protect with Lewd Safety", boolProp.boolValue, () => {
                boolProp.boolValue = !boolProp.boolValue;
            });
        }
        if (showDefaultOn) {
            var boolProp = prop.FindPropertyRelative("defaultOn");
            if (boolProp.boolValue) tags.Add("Default On");
            addToMenu(advMenu, "Default On", boolProp.boolValue, () => {
                boolProp.boolValue = !boolProp.boolValue;
            });
        }
        var resetPhysboneProp = prop.FindPropertyRelative("resetPhysbones");
        var resetPhysboneList = makeList(resetPhysboneProp, getLabel: index => "");
        if (showResetPhysbones) {
            addToMenu(advMenu, "Add PhysBone to Reset", false, () => {
                addToList(resetPhysboneProp);
            });
        }

        if (advMenu.GetItemCount() > 0) {
            var rects = renderFlex(line, 1, line);
            renderProp(rects[0], prop.FindPropertyRelative("name"));
            renderButton(rects[1], "*", () => {
                advMenu.ShowAsContext();
            });
        } else {
            renderProp(prop.FindPropertyRelative("name"));
        }

        renderSpace();
        EditorGUI.indentLevel++;

        var tagsStr = String.Join(" | ", tags.ToArray());
        if (tagsStr != "") {
            renderLabel(tagsStr);
        }

        if (type == SenkyFXProp.TOGGLE) {
            renderProp(prop.FindPropertyRelative("state"));
        } else if (type == SenkyFXProp.MODES) {
            var modeList = makeList(prop.FindPropertyRelative("modes"), getLabel: (index) => {
                return "Mode "+(index+1);
            });
            renderList(modeList);
        } else {
            renderLabel("Unknown type: " + type);
        }

        if (showResetPhysbones && resetPhysboneProp.arraySize > 0) {
            renderLabel("Reset PhysBones:");
            renderList(resetPhysboneList);
        }

        EditorGUI.indentLevel--;
    }
}

[Serializable]
public class SenkyFXProps {
    public List<SenkyFXProp> props = new List<SenkyFXProp>();
}

[CustomPropertyDrawer(typeof(SenkyFXProps))]
public class SenkyFXPropsDrawer : BetterPropertyDrawer {
    private void newItem(SerializedProperty list) {
        var menu = new GenericMenu();
        menu.AddItem(new GUIContent("Toggle"), false, () => {
            addToList(list, entry => entry.FindPropertyRelative("type").stringValue = SenkyFXProp.TOGGLE);
        });
        menu.AddItem(new GUIContent("Multi-Mode"), false, () => {
            addToList(list, entry => entry.FindPropertyRelative("type").stringValue = SenkyFXProp.MODES);
        });
        menu.AddItem(new GUIContent("Puppet"), false, () => {
            addToList(list, entry => entry.FindPropertyRelative("type").stringValue = SenkyFXProp.PUPPET);
        });
        menu.ShowAsContext();
    }
    protected override void render(SerializedProperty prop, GUIContent label) {
        var listProp = prop.FindPropertyRelative("props");
        var list = makeList(listProp, () => newItem(listProp));
        renderList(list);
    }
}
