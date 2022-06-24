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
    public override VisualElement CreateInspectorGUI() {
        var form = new SenkyUIHelper(serializedObject);

        form.Property("avatar");

        form.Property("stateBlink", "Blinking");
        form.Property("stateTalkGlow", "Talk Glow");
        form.Property("visemeFolder", "Viseme Folder");

        form.Foldout("Breathing", () => {
            form.Property("breatheObject", "Object");
            form.Property("breatheBlendshape", "BlendShape");
            form.Property("breatheScaleMin", "Min Scale");
            form.Property("breatheScaleMax", "Max Scale");
        });

        form.FoldoutOpen("Face", () => {
            form.FoldoutOpen("Eyes", () => {
                form.Property("stateEyesClosed", "Closed");
                form.Property("stateEyesHappy", "Happy");
                form.Property("stateEyesSad", "Sad");
                form.Property("stateEyesAngry", "Angry");
            });
            form.FoldoutOpen("Mouth", () => {
                form.Property("stateMouthBlep", "Blep");
                form.Property("stateMouthSuck", "Suck");
                form.Property("stateMouthSad", "Sad");
                form.Property("stateMouthAngry", "Angry");
                form.Property("stateMouthHappy", "Happy");
            });
            form.FoldoutOpen("Ears", () => {
                form.Property("stateEarsBack", "Back");
            });
        });

        form.FoldoutOpen("Toes", () => {
            form.Property("stateToesDown", "Down");
            form.Property("stateToesUp", "Up");
            form.Property("stateToesSplay", "Splay");
        });

        form.FoldoutOpen("Props", () => {
            form.Property("props");
        });

        form.Button("Generate", () => {
            var builder = new SenkyFXBuilder();
            var inputs = (SenkyFX) target;
            builder.Run(inputs);
            Debug.Log("SenkyFX Finished!");
        });

        return form.Render();
    }
}
