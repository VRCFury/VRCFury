using System;
using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using UnityEditor.UIElements;
using UnityEngine.UIElements;
using VRC.SDK3.Avatars.Components;

[CustomEditor(typeof(SenkyFX), true)]
public class SenkyFXEditor : Editor {
    private Stack<VisualElement> form;

    public override VisualElement CreateInspectorGUI() {
        var obj = serializedObject;
        form = new Stack<VisualElement>();
        form.Push(new VisualElement());

        AddProperty("avatar", "Avatar / Object Root");

        var pointingToAvatar = false;
        var self = (SenkyFX)target;
        if (self.gameObject.GetComponent<VRCAvatarDescriptor>() != null) {
            pointingToAvatar = true;
        }

        if (pointingToAvatar) {
            AddProperty("stateBlink", "Blinking");
            AddState("stateTalkGlow", "Talk Glow");
            AddProperty("visemeFolder", "Viseme Folder");

            Foldout("Breathing", () => {
                AddProperty("breatheObject", "Object");
                AddProperty("breatheBlendshape", "BlendShape");
                AddProperty("breatheScaleMin", "Min Scale");
                AddProperty("breatheScaleMax", "Max Scale");
            });

            FoldoutOpen("Face", () => {
                FoldoutOpen("Eyes", () => {
                    AddState("stateEyesClosed", "Closed");
                    AddState("stateEyesHappy", "Happy");
                    AddState("stateEyesSad", "Sad");
                    AddState("stateEyesAngry", "Angry");
                });
                FoldoutOpen("Mouth", () => {
                    AddState("stateMouthBlep", "Blep");
                    AddState("stateMouthSuck", "Suck");
                    AddState("stateMouthSad", "Sad");
                    AddState("stateMouthAngry", "Angry");
                    AddState("stateMouthHappy", "Happy");
                });
                FoldoutOpen("Ears", () => {
                    AddState("stateEarsBack", "Back");
                });
            });

            FoldoutOpen("Toes", () => {
                AddState("stateToesDown", "Down");
                AddState("stateToesUp", "Up");
                AddState("stateToesSplay", "Splay");
            });
        }

        FoldoutOpen("Props", () => {
            AddProperty("props");
        });

        if (pointingToAvatar) {
            var genButton = new Button(() => {
                var builder = new SenkyFXBuilder();
                var inputs = (SenkyFX) target;
                builder.Run(inputs);
                Debug.Log("SenkyFX Finished!");
            });
            genButton.text = "Generate";
            form.Peek().Add(genButton);
        }

        return form.Peek();
    }

    private void AddProperty(string prop, string label = null) {
        form.Peek().Add(new PropertyField(serializedObject.FindProperty(prop), label));
    }
    private void AddState(string prop, string label) {
        form.Peek().Add(SenkyFXStateEditor.render(serializedObject.FindProperty(prop), label));
    }
    public void FoldoutOpen(string header, Action with) {
        Foldout(header, with, true);
    }
    public void Foldout(string header, Action with, bool def = false) {
        var foldout = new Foldout();
        foldout.text = header;
        form.Peek().Add(foldout);
        form.Push(foldout);
        with();
        form.Pop();
    }
}
