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

public class SenkyFX : MonoBehaviour {
    public GameObject avatar;

    public SenkyFXState stateBlink;
    public string visemeFolder;

    public GameObject breatheObject;
    public string breatheBlendshape;
    public float breatheScaleMin;
    public float breatheScaleMax;

    public SenkyFXState stateToesDown;
    public SenkyFXState stateToesUp;
    public SenkyFXState stateToesSplay;

    public SenkyFXState stateEyesClosed;
    public SenkyFXState stateEyesHappy;
    public SenkyFXState stateEyesSad;
    public SenkyFXState stateEyesAngry;

    public SenkyFXState stateMouthBlep;
    public SenkyFXState stateMouthSuck;
    public SenkyFXState stateMouthSad;
    public SenkyFXState stateMouthAngry;
    public SenkyFXState stateMouthHappy;

    public SenkyFXState stateEarsBack;

    public SenkyFXState stateTalkGlow;

    public SenkyFXProps props;
}

[CustomEditor(typeof(SenkyFX), true)]
public class SenkyFXEditor : Editor {
    private Dictionary<string, Boolean> expanded = new Dictionary<string, Boolean>();

    public override void OnInspectorGUI() {
        serializedObject.Update();
        //DrawDefaultInspector();

        var obj = serializedObject;

        EditorGUILayout.PropertyField(obj.FindProperty("avatar"));

        renderProp("stateBlink", "Blinking");
        renderProp("stateTalkGlow", "Talk Glow");
        renderProp("visemeFolder", "Viseme Folder");

        foldout("Breathing", () => {
            renderProp("breatheObject", "Object");
            renderProp("breatheBlendshape", "BlendShape");
            renderProp("breatheScaleMin", "Min Scale");
            renderProp("breatheScaleMax", "Max Scale");
        });

        foldoutOpen("Face", () => {
            foldoutOpen("Eyes", () => {
                renderProp("stateEyesClosed", "Closed");
                renderProp("stateEyesHappy", "Happy");
                renderProp("stateEyesSad", "Sad");
                renderProp("stateEyesAngry", "Angry");
            });
            foldoutOpen("Mouth", () => {
                renderProp("stateMouthBlep", "Blep");
                renderProp("stateMouthSuck", "Suck");
                renderProp("stateMouthSad", "Sad");
                renderProp("stateMouthAngry", "Angry");
                renderProp("stateMouthHappy", "Happy");
            });
            foldoutOpen("Ears", () => {
                renderProp("stateEarsBack", "Back");
            });
        });

        foldoutOpen("Toes", () => {
            renderProp("stateToesDown", "Down");
            renderProp("stateToesUp", "Up");
            renderProp("stateToesSplay", "Splay");
        });

        foldoutOpen("Props", () => {
            renderProp("props");
        });

        if (GUILayout.Button("Generate")) {
            var builder = new SenkyFXBuilder();
            var inputs = (SenkyFX) target;
            builder.Run(inputs);
            Debug.Log("SenkyFX Finished!");
        }
        serializedObject.ApplyModifiedProperties();
    }

    private void foldoutOpen(string header, Action with) {
        foldout(header, with, true);
    }
    private void foldout(string header, Action with, bool def = false) {
        var oldExpanded = expanded.TryGetValue(header, out var dictVal) ? dictVal : def;
        var newExpanded = EditorGUILayout.Foldout(oldExpanded, header);
        expanded[header] = newExpanded;
        if (newExpanded) {
            EditorGUI.indentLevel++;
            with();
            EditorGUI.indentLevel--;
        }
    }
    private void renderProp(string prop, string label="") {
        EditorGUILayout.PropertyField(serializedObject.FindProperty(prop), new GUIContent(label));
    }
}
