using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using VF.Model;
using VF.Model.StateAction;
using VRC.SDK3.Avatars.Components;

namespace VF.Inspector {

[CustomPropertyDrawer(typeof(VF.Model.StateAction.Action))]
public class VRCFuryActionDrawer : PropertyDrawer {
    public override VisualElement CreatePropertyGUI(SerializedProperty prop) {
        return Render(prop);
    }

    public static VisualElement Render(SerializedProperty prop) {

        var type = VRCFuryEditorUtils.GetManagedReferenceTypeName(prop);

        switch (type) {
            case nameof(MaterialAction): {
                var row = new VisualElement {
                    style = {
                        flexDirection = FlexDirection.Row,
                        alignItems = Align.FlexStart
                    }
                };

                var label = new Label("Material") {
                    style = {
                        flexGrow = 0,
                        flexBasis = 100
                    }
                };
                row.Add(label);

                var propField = VRCFuryEditorUtils.Prop(prop.FindPropertyRelative("obj"));
                propField.style.flexGrow = 1;
                row.Add(propField);
            
                var propField2 = VRCFuryEditorUtils.Prop(prop.FindPropertyRelative("materialIndex"));
                propField2.style.flexGrow = 0;
                propField2.style.flexBasis = 50;
                row.Add(propField2);
            
                var propField3 = VRCFuryEditorUtils.Prop(prop.FindPropertyRelative("mat"));
                propField3.style.flexGrow = 1;
                row.Add(propField3);

                return row;
            }
            case nameof(FlipbookAction): {
                var row = new VisualElement {
                    style = {
                        flexDirection = FlexDirection.Row,
                        alignItems = Align.FlexStart
                    }
                };

                var label = new Label("Flipbook Frame") {
                    style = {
                        flexGrow = 0,
                        flexBasis = 100
                    }
                };
                row.Add(label);

                var propField = VRCFuryEditorUtils.Prop(prop.FindPropertyRelative("obj"));
                propField.style.flexGrow = 1;
                row.Add(propField);
            
                var propField3 = VRCFuryEditorUtils.Prop(prop.FindPropertyRelative("frame"));
                propField3.style.flexGrow = 0;
                propField3.style.flexBasis = 30;
                row.Add(propField3);

                return row;
            }
            case nameof(ScaleAction): {
                var row = new VisualElement {
                    style = {
                        flexDirection = FlexDirection.Row,
                        alignItems = Align.FlexStart
                    }
                };

                var label = new Label("Scale") {
                    style = {
                        flexGrow = 0,
                        flexBasis = 100
                    }
                };
                row.Add(label);

                var propField = VRCFuryEditorUtils.Prop(prop.FindPropertyRelative("obj"));
                propField.style.flexGrow = 1;
                row.Add(propField);
            
                var propField3 = VRCFuryEditorUtils.Prop(prop.FindPropertyRelative("scale"));
                propField3.style.flexGrow = 0;
                propField3.style.flexBasis = 50;
                row.Add(propField3);

                return row;
            }
            case nameof(ObjectToggleAction): {
                var row = new VisualElement {
                    style = {
                        flexDirection = FlexDirection.Row,
                        alignItems = Align.FlexStart
                    }
                };

                var label = new Label("Object Toggle") {
                    style = {
                        flexGrow = 0,
                        flexBasis = 100
                    }
                };
                row.Add(label);

                var propField = VRCFuryEditorUtils.Prop(prop.FindPropertyRelative("obj"));
                propField.style.flexGrow = 1;
                row.Add(propField);

                return row;
            }
            case nameof(BlendShapeAction): {
                var row = new VisualElement {
                    style = {
                        flexDirection = FlexDirection.Row,
                        alignItems = Align.FlexStart
                    }
                };

                var label = new Label {
                    text = "BlendShape",
                    style = {
                        flexGrow = 0,
                        flexBasis = 100
                    }
                };
                row.Add(label);

                var blendshapeProp = prop.FindPropertyRelative("blendShape");
                var propField = VRCFuryEditorUtils.Prop(blendshapeProp);
                propField.style.flexGrow = 1;
                row.Add(propField);
            
                var valueField = VRCFuryEditorUtils.Prop(prop.FindPropertyRelative("blendShapeValue"));
                valueField.style.flexGrow = 0;
                valueField.style.flexBasis = 50;
                row.Add(valueField);

                System.Action selectButtonPress = () => {
                    var editorObject = prop.serializedObject.targetObject;
                    var shapes = new Dictionary<string,string>();
                    if (editorObject is Component c) {
                        var avatarObject = c.GetComponentInParent<VRCAvatarDescriptor>();
                        if (avatarObject) {
                            foreach (var skin in avatarObject.GetComponentsInChildren<SkinnedMeshRenderer>(true)) {
                                if (!skin.sharedMesh) continue;
                                for (var i = 0; i < skin.sharedMesh.blendShapeCount; i++) {
                                    var bs = skin.sharedMesh.GetBlendShapeName(i);
                                    if (shapes.ContainsKey(bs)) {
                                        shapes[bs] += ", " + skin.gameObject.name;
                                    } else {
                                        shapes[bs] = skin.gameObject.name;
                                    }
                                }
                            }
                        }
                    }

                    var menu = new GenericMenu();
                    foreach (var entry in shapes.OrderBy(entry => entry.Key)) {
                        menu.AddItem(
                            new GUIContent(entry.Key + " (" + entry.Value + ")"),
                            false,
                            () => {
                                blendshapeProp.stringValue = entry.Key;
                                blendshapeProp.serializedObject.ApplyModifiedProperties();
                            });
                    }
                    menu.ShowAsContext();
                };
                var selectButton = new Button(selectButtonPress) { text = "Select" };
                row.Add(selectButton);

                return row;
            }
            case nameof(AnimationClipAction): {
                var row = new VisualElement {
                    style = {
                        flexDirection = FlexDirection.Row,
                        alignItems = Align.FlexStart
                    }
                };

                var label = new Label {
                    text = "Animation Clip",
                    style = {
                        flexGrow = 0,
                        flexBasis = 100
                    }
                };
                row.Add(label);

                var propField = VRCFuryEditorUtils.Prop(prop.FindPropertyRelative("clip"));
                propField.style.flexGrow = 1;
                row.Add(propField);

                return row;
            }
        }

        return VRCFuryEditorUtils.WrappedLabel($"Unknown action type: {type}");
    }
}

}
