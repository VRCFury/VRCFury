using System;
using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using UnityEditor.UIElements;
using UnityEngine.UIElements;
using VRC.SDK3.Avatars.Components;
using VRCF.Builder;
using VRCF.Model;
using UnityEditor.Animations;

namespace VRCF.Inspector {

public class VRCFuryMenuItem {
    [MenuItem("Tools/Force Run VRCFury on Selection")]
    static void Run() {
        var obj = Selection.activeTransform.gameObject;
        var vrcfury = obj.GetComponent<VRCFury>();
        var builder = new VRCFuryBuilder();
        builder.SafeRun(vrcfury, obj);
    }

    [MenuItem("Tools/Force Run VRCFury on Selection", true)]
    static bool Check() {
        var obj = Selection.activeTransform.gameObject;
        if (obj == null) return false;
        var avatar = obj.GetComponent<VRCAvatarDescriptor>();
        if (avatar == null) return false;
        if (obj.GetComponent<VRCFury>() != null) return true;
        if (obj.GetComponentsInChildren<VRCFury>(true).Length > 0) return true;
        return false;
    }
}

[CustomEditor(typeof(VRCFury), true)]
public class VRCFuryEditor : Editor {
    private Stack<VisualElement> form;

    public override VisualElement CreateInspectorGUI() {
        var obj = serializedObject;
        form = new Stack<VisualElement>();
        form.Push(new VisualElement());

        AddProperty("avatar", "Avatar / Object Root");

        var pointingToAvatar = false;
        var self = (VRCFury)target;
        if (self.gameObject.GetComponent<VRCAvatarDescriptor>() != null) {
            pointingToAvatar = true;
        }

        if (pointingToAvatar) {
            AddState("stateBlink", "Blinking");
            AddState("stateTalking", "Talking");
            AddProperty("viseme", "Advanced Visemes");
            AddProperty("scaleEnabled", "Enable Avatar Scale Slider");

            AddProperty("securityCodeLeft", "Security Left");
            AddProperty("securityCodeRight", "Security Right");

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
            var box = new Box();
            box.style.marginTop = box.style.marginBottom = 10;
            form.Peek().Add(box);

            var label = new Label("VRCFury builds automatically when your avatar uploads. You only need to click this button if you want to verify its changes in the editor or in play mode.");
            VRCFuryEditorUtils.Padding(box, 5);
            label.style.whiteSpace = WhiteSpace.Normal;
            box.Add(label);

            var genButton = new Button(() => {
                var builder = new VRCFuryBuilder();
                builder.SafeRun(self, self.gameObject);
            });
            genButton.style.marginTop = 5;
            genButton.text = "Force Build Now";
            box.Add(genButton);
        }

        return form.Peek();
    }

    private void AddProperty(string prop, string label = null) {
        form.Peek().Add(new PropertyField(serializedObject.FindProperty(prop), label));
    }
    private void AddState(string prop, string label) {
        form.Peek().Add(VRCFuryStateEditor.render(serializedObject.FindProperty(prop), label));
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

}
