using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using VF.Actions;
using VF.Builder;
using VF.Feature.Base;
using VF.Model;
using VF.Model.StateAction;
using VF.Utils;

namespace VF.Inspector {

[CustomPropertyDrawer(typeof(VF.Model.StateAction.Action))]
internal class VRCFuryActionDrawer : PropertyDrawer {
    public override VisualElement CreatePropertyGUI(SerializedProperty prop) {
        var el = new VisualElement();
        el.AddToClassList("vfAction");
        el.Add(Render(prop));
        return el;
    }

    private static VisualElement Render(SerializedProperty prop) {
        var col = new VisualElement();
        
        var el = RenderInner(prop);
        col.Add(el);
        
        var desktopActive = prop.FindPropertyRelative("desktopActive");
        var androidActive = prop.FindPropertyRelative("androidActive");
        col.AddManipulator(new ContextualMenuManipulator(e => {
            if (e.menu.MenuItems().OfType<DropdownMenuAction>().Any(i => i.name == "Desktop Only")) {
                return;
            }
            if (e.menu.MenuItems().Count > 0) {
                e.menu.AppendSeparator();
            }
            e.menu.AppendAction("Desktop Only", a => {
                desktopActive.boolValue = !desktopActive.boolValue;
                androidActive.boolValue = false;
                prop.serializedObject.ApplyModifiedProperties();
            }, desktopActive.boolValue ? DropdownMenuAction.Status.Checked : DropdownMenuAction.Status.Normal);
            e.menu.AppendAction("Quest+Android+iOS Only", a => {
                androidActive.boolValue = !androidActive.boolValue;
                desktopActive.boolValue = false;
                prop.serializedObject.ApplyModifiedProperties();
            }, androidActive.boolValue ? DropdownMenuAction.Status.Checked : DropdownMenuAction.Status.Normal);
        }));
        
        col.Add(VRCFuryEditorUtils.RefreshOnChange(() => {
            var row = new VisualElement().Row().FlexWrap();

            void AddFlag(string tag) {
                var flag = new Label(tag);
                flag.style.width = StyleKeyword.Auto;
                flag.style.backgroundColor = new Color(1f, 1f, 1f, 0.1f);
                flag.style.borderTopRightRadius = 5;
                flag.style.marginRight = 5;
                flag.Padding(2, 4);
                row.Add(flag);
            }
            
            if (desktopActive.boolValue) AddFlag("Desktop Only");
            if (androidActive.boolValue) AddFlag("Quest+Android+iOS Only");

            return row;
        }, desktopActive, androidActive));

        return col;
    }
    
    private static VisualElement RenderInner(SerializedProperty prop) {
        var modelType = VRCFuryEditorUtils.GetManagedReferenceType(prop);
        var builderType = FeatureFinder.GetBuilderType(modelType);
        if (builderType != null) {
            return FeatureFinder.RenderFeatureEditor(prop, (title, body) => {
                var output = new VisualElement();
                if (builderType.GetCustomAttribute<FeatureHideTitleInEditorAttribute>() == null)
                    output.Add(Title(title));
                output.Add(body);
                return output;
            });
        }
        
        var type = VRCFuryEditorUtils.GetManagedReferenceTypeName(prop);

        var component = prop.serializedObject.targetObject as UnityEngine.Component;
        var avatarObject = VRCAvatarUtils.GuessAvatarObject(component);
        if (avatarObject == null) {
            avatarObject = component.owner().root;
        }

        void Apply() {
            prop.serializedObject.ApplyModifiedProperties();
        }
        void ApplyWithoutUndo() {
            prop.serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }

        switch (type) {
            case nameof(MaterialAction): {
                var content = new VisualElement();
                content.Add(Title("Material Swap"));
                var rendererProp = prop.FindPropertyRelative("renderer");
                var indexProp = prop.FindPropertyRelative("materialIndex");

                content.Add(VRCFuryEditorUtils.Prop(rendererProp, "Renderer"));

                var indexField = VRCFuryEditorUtils.RefreshOnChange(() => {
                    var renderer = rendererProp.objectReferenceValue as Renderer;
                    if (renderer == null) {
                        var f = new PopupField<string>(
                            new List<string>() { "Select a renderer" },
                            0
                        );
                        f.SetEnabled(false);
                        return f;
                    } else {
                        var choices = Enumerable.Range(0, renderer.sharedMaterials.Length).ToList();
                        int selectedIndex;
                        if (indexProp.intValue >= 0 && indexProp.intValue < renderer.sharedMaterials.Length) {
                            selectedIndex = indexProp.intValue;
                        } else {
                            choices.Add(indexProp.intValue);
                            selectedIndex = choices.Count - 1;
                        }

                        string FormatLabel(int i) {
                            if (i >= 0 && i < renderer.sharedMaterials.Length) {
                                var mat = renderer.sharedMaterials[i];
                                if (mat != null) return $"{i} - {mat.name}";
                            }

                            return $"{i} - ???";
                        }

                        var f = new PopupField<int>(choices, selectedIndex, FormatLabel, FormatLabel);
                        f.RegisterValueChangedCallback(cb => {
                            if (cb.newValue >= 0 && cb.newValue < renderer.sharedMaterials.Length) {
                                indexProp.intValue = cb.newValue;
                                indexProp.serializedObject.ApplyModifiedProperties();
                            }
                        });
                        return f;
                    }
                }, rendererProp, indexProp);
                content.Add(VRCFuryEditorUtils.Prop(prop.FindPropertyRelative("materialIndex"), "Slot", fieldOverride: indexField));
                content.Add(VRCFuryEditorUtils.Prop(prop.FindPropertyRelative("mat"), "Material"));
                return content;
            }
            case nameof(ShaderInventoryAction): {
                var output = new VisualElement();
                output.Add(Title("SCSS Shader Inventory"));
                output.Add(VRCFuryEditorUtils.Prop(prop.FindPropertyRelative("renderer"), "Renderer"));
                output.Add(VRCFuryEditorUtils.Prop(prop.FindPropertyRelative("slot"), "Slot Number"));
                return output;
            }
            case nameof(PoiyomiUVTileAction): {
                var content = new VisualElement();

                content.Add(Title("Poiyomi UV Tile"));
                content.Add(VRCFuryEditorUtils.Prop(prop.FindPropertyRelative("renderer"), "Renderer"));
                content.Add(VRCFuryEditorUtils.Prop(prop.FindPropertyRelative("row"), "Row (0-3)"));
                content.Add(VRCFuryEditorUtils.Prop(prop.FindPropertyRelative("column"), "Column (0-3)"));

                var adv = new Foldout {
                    text = "Advanced UV Tile Options",
                    value = false
                };
                adv.Add(VRCFuryEditorUtils.Prop(prop.FindPropertyRelative("dissolve"), "Use UV Tile Dissolve"));
                adv.Add(VRCFuryEditorUtils.Prop(prop.FindPropertyRelative("renamedMaterial"), "Renamed Material", tooltip: "Material suffix when using poiyomi renamed properties"));
                content.Add(adv);
                return content;
            }
            case nameof(ScaleAction): {
                var row = new VisualElement();
                row.Add(Title("Scale"));
                row.Add(VRCFuryEditorUtils.Prop(prop.FindPropertyRelative("obj"), "Object"));
                row.Add( VRCFuryEditorUtils.Prop(prop.FindPropertyRelative("scale"), "Multiplier"));
                return row;
            }
            case nameof(SpsOnAction): {
                var row = new VisualElement().Row();
                row.Add(Title("Enable SPS").FlexBasis(100));
                row.Add(VRCFuryEditorUtils.Prop(prop.FindPropertyRelative("target")).FlexGrow(1));
                return row;
            }
            case nameof(FxFloatAction): {
                var col = new VisualElement();

                var row = new VisualElement().Row();
                row.Add(Title("Set an FX Float").FlexBasis(100));
                row.Add(VRCFuryEditorUtils.Prop(prop.FindPropertyRelative("name")).FlexGrow(1));
                row.Add(VRCFuryEditorUtils.Prop(prop.FindPropertyRelative("value")).FlexBasis(30));
                col.Add(row);
                
                col.Add(VRCFuryEditorUtils.Warn("Warning: This will cause the FX parameter to be 'animated', which means it cannot be used in a menu or otherwise controlled by VRChat."));

                return col;
            }
            case nameof(BlendShapeAction): {
                var content = new VisualElement();

                content.Add(Title("BlendShape"));

                var allRenderersProp = prop.FindPropertyRelative("allRenderers");
                var rendererProp = prop.FindPropertyRelative("renderer");
                content.Add(MaterialPropertyActionBuilder.RendererSelector(allRenderersProp, rendererProp));

                var row = new VisualElement().Row();
                var blendshapeProp = prop.FindPropertyRelative("blendShape");
                row.Add(VRCFuryEditorUtils.Prop(blendshapeProp, "Blendshape").FlexGrow(1));
                var selectButton = new Button(SelectButtonPress) { text = "Search" };
                row.Add(selectButton);
                content.Add(row);

                var valueProp = prop.FindPropertyRelative("blendShapeValue");
                var valueField = VRCFuryEditorUtils.Prop(valueProp, "Value (0-100)");
                valueField.RegisterCallback<ChangeEvent<float>>(e => {
                    if (e.newValue < 0) {
                        valueProp.floatValue = 0;
                        ApplyWithoutUndo();
                    }
                    if (e.newValue > 100) {
                        valueProp.floatValue = 100;
                        ApplyWithoutUndo();
                    }
                });
                content.Add(valueField);

                void SelectButtonPress() {
                    var allRenderers = allRenderersProp.boolValue;
                    var singleRenderer = rendererProp.objectReferenceValue as Renderer;
                    var skins = avatarObject.GetComponentsInSelfAndChildren<SkinnedMeshRenderer>()
                        .Where(skin => allRenderers || skin == singleRenderer)
                        .ToArray();
                    ShowBlendshapeSearchWindow(skins, value => {
                        blendshapeProp.stringValue = value;
                        Apply();
                    });
                }

                return content;
            }
            case nameof(AnimationClipAction): {
                var row = new VisualElement().Row();
                row.Add(Title("Animation Clip").FlexBasis(100));
                var clipProp = prop.FindPropertyRelative("clip");
                row.Add(VRCFuryEditorUtils.Prop(clipProp).FlexGrow(1));
                row.Add(new Button(() => {
                    var clip = (clipProp.GetObject() as GuidAnimationClip)?.Get();
                    if (clip == null) {
                        var newPath = EditorUtility.SaveFilePanelInProject("VRCFury Recorder", "New Animation", "anim", "Path to new animation");
                        if (string.IsNullOrEmpty(newPath)) return;
                        clip = new AnimationClip();
                        AssetDatabase.CreateAsset(clip, newPath);
                        GuidWrapperPropertyDrawer.SetValue(clipProp, clip);
                        clipProp.serializedObject.ApplyModifiedProperties();
                    }
                    RecorderUtils.Record(clip, component.owner());
                }) { text = "Record" });
                return row;
            }
            case nameof(BlockBlinkingAction): {
                return Title("Disable Blinking");
            }
            case nameof(BlockVisemesAction): {
                return Title("Disable Visemes");
            }
            case nameof(ResetPhysboneAction): {
                var row = new VisualElement().Row();
                row.Add(Title("Reset Physbone").FlexBasis(100));
                row.Add(VRCFuryEditorUtils.Prop(prop.FindPropertyRelative("physBone")).FlexGrow(1));
                return row;
            }
            case nameof(FlipBookBuilderAction): {
                var output = new VisualElement();
                output.Add(Title("Flipbook Builder"));
                output.Add(VRCFuryEditorUtils.Info(
                    "This will create a clip made up of one frame per child action. This is mostly useful for" +
                    " VRCFury Toggles with 'Use a Slider (Radial)' enabled, as you can put various presets in these slots" +
                    " and use the slider to select one of them."
                ));
                output.Add(VRCFuryEditorUtils.List(prop.FindPropertyRelative("pages")));
                return output;
            }
            case nameof(SmoothLoopAction): {
                var output = new VisualElement();
                output.Add(Title("Smooth Loop Builder (Breathing, etc)"));
                output.Add(VRCFuryEditorUtils.Info(
                    "This will create an animation smoothly looping between two states." +
                    " You can use this for a breathing cycle or any other type of smooth two-state loop."));
                output.Add(VRCFuryStateEditor.render(prop.FindPropertyRelative("state1"), "State A"));
                output.Add(VRCFuryStateEditor.render(prop.FindPropertyRelative("state2"), "State B"));
                output.Add(VRCFuryEditorUtils.Prop(prop.FindPropertyRelative("loopTime"), "Loop time (seconds)"));
                return output;
            }

            case nameof(SyncParamAction): {
                var col = new VisualElement();

                var row = new VisualElement().Row();
                row.Add(Title("Set a Synced Param").FlexBasis(150));
                row.Add(VRCFuryEditorUtils.Prop(prop.FindPropertyRelative("param")).FlexGrow(1));
                row.Add(VRCFuryEditorUtils.Prop(prop.FindPropertyRelative("value")).FlexBasis(30));
                col.Add(row);

                return col;
            }
            case nameof(ToggleStateAction): {
                var col = new VisualElement();

                var row = new VisualElement().Row();
                row.Add(Title("Set a Toggle State").FlexBasis(150));
                row.Add(VRCFuryEditorUtils.Prop(prop.FindPropertyRelative("toggle")).FlexGrow(1));
                row.Add(VRCFuryEditorUtils.Prop(prop.FindPropertyRelative("value")).FlexBasis(30));
                col.Add(row);

                return col;
            }
            case nameof(TagStateAction): {
                var col = new VisualElement();

                var row = new VisualElement().Row();
                row.Add(Title("Set a Tag State").FlexBasis(150));
                row.Add(VRCFuryEditorUtils.Prop(prop.FindPropertyRelative("tag")).FlexGrow(1));
                row.Add(VRCFuryEditorUtils.Prop(prop.FindPropertyRelative("value")).FlexBasis(30));
                col.Add(row);

                return col;
            }
        }

        return VRCFuryEditorUtils.WrappedLabel($"Unknown action type: {type}");
    }

    private static VisualElement Title(string title) {
        var label = new Label(title);
        label.style.unityFontStyleAndWeight = FontStyle.Bold;
        return label;
    }

    [CustomPropertyDrawer(typeof(FlipBookBuilderAction.FlipBookPage))]
    public class FlipbookPageDrawer : PropertyDrawer {
        public override VisualElement CreatePropertyGUI(SerializedProperty prop) {
            var content = new VisualElement();
            var match = Regex.Match(prop.propertyPath, @"\[(\d+)\]$");
            string pageNum;
            if (match.Success && int.TryParse(match.Groups[1].ToString(), out var num)) {
                pageNum = (num + 1).ToString();
            } else {
                pageNum = "?";
            }
            content.Add(Title($"Page #{pageNum}"));
            content.Add(VRCFuryEditorUtils.Prop(prop.FindPropertyRelative("state")));
            return content;
        }
    }
    
    public static void ShowBlendshapeSearchWindow(IList<SkinnedMeshRenderer> skins, Action<string> onSelect) {
        var window = new VrcfSearchWindow("Blendshapes");

        var shapes = new Dictionary<string, string>();
        foreach (var skin in skins) {
            foreach (var bs in skin.GetBlendshapeNames()) {
                if (shapes.ContainsKey(bs)) {
                    shapes[bs] += ", " + skin.owner().name;
                } else {
                    shapes[bs] = skin.owner().name;
                }
            }
        }

        var mainGroup = window.GetMainGroup();
        foreach (var entry in shapes.OrderBy(entry => entry.Key)) {
            mainGroup.Add(entry.Key + " (" + entry.Value + ")", entry.Key);
        }
                    
        window.Open(onSelect);
    }
}

}
