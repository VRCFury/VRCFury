using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using VF.Builder;
using VF.Model.StateAction;
using VF.Utils;
using VRC.SDK3.Avatars.Components;

namespace VF.Inspector {

[CustomPropertyDrawer(typeof(VF.Model.StateAction.Action))]
public class VRCFuryActionDrawer : PropertyDrawer {
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
            if (e.menu.MenuItems().Count > 0) {
                e.menu.AppendSeparator();
            }
            e.menu.AppendAction("Desktop Only", a => {
                desktopActive.boolValue = !desktopActive.boolValue;
                androidActive.boolValue = false;
                prop.serializedObject.ApplyModifiedProperties();
            }, desktopActive.boolValue ? DropdownMenuAction.Status.Checked : DropdownMenuAction.Status.Normal);
            e.menu.AppendAction("Android Only", a => {
                androidActive.boolValue = !androidActive.boolValue;
                desktopActive.boolValue = false;
                prop.serializedObject.ApplyModifiedProperties();
            }, androidActive.boolValue ? DropdownMenuAction.Status.Checked : DropdownMenuAction.Status.Normal);
        }));
        
        col.Add(VRCFuryEditorUtils.RefreshOnChange(() => {
            var row = new VisualElement();
            row.style.flexWrap = Wrap.Wrap;
            row.style.flexDirection = FlexDirection.Row;

            void AddFlag(string tag) {
                var flag = new Label(tag);
                flag.style.width = StyleKeyword.Auto;
                flag.style.backgroundColor = new Color(1f, 1f, 1f, 0.1f);
                flag.style.borderTopRightRadius = 5;
                flag.style.marginRight = 5;
                VRCFuryEditorUtils.Padding(flag, 2, 4);
                row.Add(flag);
            }
            
            if (desktopActive.boolValue) AddFlag("Desktop Only");
            if (androidActive.boolValue) AddFlag("Android Only");

            return row;
        }, desktopActive, androidActive));
        
        return col;
    }
    
    private static VisualElement RenderInner(SerializedProperty prop) {
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
        string GetPath(VFGameObject obj) {
            return avatarObject == null ? obj.name : obj.GetPath(avatarObject);
        }

        switch (type) {
            case nameof(MaterialAction): {
                var content = new VisualElement();
                content.Add(new Label("Material Swap"));
                content.Add(VRCFuryEditorUtils.Prop(prop.FindPropertyRelative("obj"), "Renderer"));
                content.Add(VRCFuryEditorUtils.Prop(prop.FindPropertyRelative("materialIndex"), "Slot Number"));
                content.Add(VRCFuryEditorUtils.Prop(prop.FindPropertyRelative("mat"), "Material"));
                return content;
            }
            case nameof(FlipbookAction): {
                var output = new VisualElement();
                output.Add(new Label("Poiyomi Flipbook Frame"));
                output.Add(VRCFuryEditorUtils.Prop(prop.FindPropertyRelative("renderer"), "Renderer"));
                output.Add(VRCFuryEditorUtils.Prop(prop.FindPropertyRelative("frame"), "Frame Number"));
                return output;
            }
            case nameof(ShaderInventoryAction): {
                var output = new VisualElement();
                output.Add(new Label("SCSS Shader Inventory"));
                output.Add(VRCFuryEditorUtils.Prop(prop.FindPropertyRelative("renderer"), "Renderer"));
                output.Add(VRCFuryEditorUtils.Prop(prop.FindPropertyRelative("slot"), "Slot Number"));
                return output;
            }
            case nameof(PoiyomiUVTileAction): {
                var content = new VisualElement();

                content.Add(new Label("Poiyomi UV Tile"));
                content.Add(VRCFuryEditorUtils.Prop(prop.FindPropertyRelative("renderer"), "Renderer"));
                content.Add(VRCFuryEditorUtils.Prop(prop.FindPropertyRelative("row"), "Row (0-3)"));
                content.Add(VRCFuryEditorUtils.Prop(prop.FindPropertyRelative("column"), "Column (0-3)"));

                var adv = new Foldout {
                    text = "Advanced UV Tile Options",
                    style = {
                        flexDirection = FlexDirection.Column,
                    },
                    value = false
                };
                adv.Add(VRCFuryEditorUtils.Prop(prop.FindPropertyRelative("dissolve"), "Use UV Tile Dissolve"));
                adv.Add(VRCFuryEditorUtils.Prop(prop.FindPropertyRelative("renamedMaterial"), "Renamed Material", tooltip: "Material suffix when using poiyomi renamed properties"));
                content.Add(adv);
                return content;
            }
            case nameof(MaterialPropertyAction): {
                var content = new VisualElement();

                content.Add(new Label("Material Property"));

                var affectAllMeshesProp = prop.FindPropertyRelative("affectAllMeshes");
                var rendererProp = prop.FindPropertyRelative("renderer");
                content.Add(RendererSelector(
                    affectAllMeshesProp,
                    rendererProp
                ));

                var propertyNameProp = prop.FindPropertyRelative("propertyName");
                {
                    var row = new VisualElement().Row();
                    row.Add(VRCFuryEditorUtils.Prop(propertyNameProp, "Property").FlexGrow(1));
                    row.Add(new Button(SearchClick) { text = "Search" }.Margin(0));
                    content.Add(row);
                }

                var valueProp = prop.FindPropertyRelative("value");
                content.Add(VRCFuryEditorUtils.Prop(valueProp, "Value"));

                return content;

                void SearchClick() {
                    var searchWindow = new VrcfSearchWindow("Material Properties");
                    GetTreeEntries(searchWindow);
                    searchWindow.Open(value => {
                        propertyNameProp.stringValue = value;
                        Apply();
                    });
                }

                void GetTreeEntries(VrcfSearchWindow searchWindow) {
                    var mainGroup = searchWindow.GetMainGroup();
                    var renderers = new List<Renderer>();
                    if (affectAllMeshesProp.boolValue) {
                        if (avatarObject != null) {
                            renderers.AddRange(avatarObject.GetComponentsInSelfAndChildren<Renderer>());
                        }
                    } else {
                        renderers.Add(rendererProp.objectReferenceValue as Renderer);
                    }

                    if (renderers.Count == 0) return;

                    foreach (var renderer in renderers) {
                        if (renderer == null) continue;
                        var sharedMaterials = renderer.sharedMaterials;
                        if (sharedMaterials.Length == 0) continue;

                        var rendererGroup = renderers.Count > 1
                            ? mainGroup.AddGroup("Mesh: " + GetPath(renderer.owner()))
                            : mainGroup;
                        foreach (var material in sharedMaterials) {
                            if (material == null) continue;

                            var matGroup = sharedMaterials.Length > 1
                                ? rendererGroup.AddGroup("Material: " + material.name)
                                : rendererGroup;
                            var shader = material.shader;
                            
                            if (shader == null) continue;
                            
                            var count = ShaderUtil.GetPropertyCount(shader);
                            var materialProperties = MaterialEditor.GetMaterialProperties(new Object[]{ material });
                            for (var i = 0; i < count; i++)
                            {
                                var propertyName = ShaderUtil.GetPropertyName(shader, i);
                                var readableName = ShaderUtil.GetPropertyDescription(shader, i);
                                var matProp = System.Array.Find(materialProperties, p => p.name == propertyName);
                                if ((matProp.flags & MaterialProperty.PropFlags.HideInInspector) != 0) continue;
                                            
                                var propType = ShaderUtil.GetPropertyType(shader, i);
                                
                                if (propType != ShaderUtil.ShaderPropertyType.Float &&
                                    propType != ShaderUtil.ShaderPropertyType.Range) continue;
                                
                                var prioritizePropName = readableName.Length > 25f;
                                var entryName = prioritizePropName ? propertyName : readableName;
                                if (renderers.Count > 1) {
                                    entryName += $" (Mesh: {GetPath(renderer.owner())})";
                                }
                                if (sharedMaterials.Length > 1) {
                                    entryName += $" (Mat: {material.name})";
                                }

                                entryName += prioritizePropName ? $" ({readableName})" : $" ({propertyName})";
                                matGroup.Add(entryName, propertyName);
                            }
                        }    
                    }
                }
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

                row.Add(VRCFuryEditorUtils.Prop(
                    prop.FindPropertyRelative("mode"),
                    formatEnum: str => {
                        if (str == "Toggle") return "Flip State (Deprecated)";
                        return str;
                    },
                    style: s => {
                        s.flexGrow = 0;
                        s.flexBasis = 100;
                    }));

                row.Add(VRCFuryEditorUtils.Prop(
                    prop.FindPropertyRelative("obj"),
                    style: s => s.flexGrow = 1
                ));

                return row;
            }
            case nameof(SpsOnAction): {
                var row = new VisualElement {
                    style = {
                        flexDirection = FlexDirection.Row,
                        alignItems = Align.FlexStart
                    }
                };

                var label = new Label("Enable SPS") {
                    style = {
                        flexGrow = 0,
                        flexBasis = 100
                    }
                };
                row.Add(label);

                var propField = VRCFuryEditorUtils.Prop(prop.FindPropertyRelative("target"));
                propField.style.flexGrow = 1;
                row.Add(propField);

                return row;
            }
            case nameof(FxFloatAction): {
                var col = new VisualElement();

                var row = new VisualElement {
                    style = {
                        flexDirection = FlexDirection.Row,
                        alignItems = Align.FlexStart
                    }
                };

                var label = new Label("Set an FX Float") {
                    style = {
                        flexGrow = 0,
                        flexBasis = 100
                    }
                };
                row.Add(label);

                var propField = VRCFuryEditorUtils.Prop(prop.FindPropertyRelative("name"));
                propField.style.flexGrow = 1;
                row.Add(propField);
                
                var valueField = VRCFuryEditorUtils.Prop(prop.FindPropertyRelative("value"));
                valueField.style.flexBasis = 30;
                row.Add(valueField);

                col.Add(row);
                
                col.Add(VRCFuryEditorUtils.Warn("Warning: This will cause the FX parameter to be 'animated', which means it cannot be used in a menu or otherwise controlled by VRChat."));

                return col;
            }
            case nameof(BlendShapeAction): {
                var content = new VisualElement();

                content.Add(new Label { text = "BlendShape" });

                var allRenderersProp = prop.FindPropertyRelative("allRenderers");
                var rendererProp = prop.FindPropertyRelative("renderer");
                content.Add(RendererSelector(allRenderersProp, rendererProp));

                var row = new VisualElement {
                    style = {
                        flexDirection = FlexDirection.Row
                    }
                };
                var blendshapeProp = prop.FindPropertyRelative("blendShape");
                row.Add(VRCFuryEditorUtils.Prop(blendshapeProp, "Blendshape", style: s => s.flexGrow = 1));
                var selectButton = new Button(SelectButtonPress) { text = "Search" }.Margin(0);
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
                    var window = new VrcfSearchWindow("Blendshapes");
                    var allRenderers = allRenderersProp.boolValue;
                    var singleRenderer = rendererProp.objectReferenceValue as Renderer;

                    var shapes = new Dictionary<string, string>();
                    if (avatarObject != null) {
                        foreach (var skin in avatarObject.GetComponentsInSelfAndChildren<SkinnedMeshRenderer>()) {
                            if (!allRenderers && skin != singleRenderer) continue;
                            if (!skin.sharedMesh) continue;
                            for (var i = 0; i < skin.sharedMesh.blendShapeCount; i++) {
                                var bs = skin.sharedMesh.GetBlendShapeName(i);
                                if (shapes.ContainsKey(bs)) {
                                    shapes[bs] += ", " + skin.owner().name;
                                } else {
                                    shapes[bs] = skin.owner().name;
                                }
                            }
                        }
                    }

                    var mainGroup = window.GetMainGroup();
                    foreach (var entry in shapes.OrderBy(entry => entry.Key)) {
                        mainGroup.Add(entry.Key + " (" + entry.Value + ")", entry.Key);
                    }
                    
                    window.Open(value => {
                        blendshapeProp.stringValue = value;
                        Apply();
                    });
                }

                return content;
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
            case nameof(BlockBlinkingAction): {
                return new Label {
                    text = "Disable Blinking"
                };
            }
            case nameof(BlockVisemesAction): {
                return new Label {
                    text = "Disable Visemes"
                };
            }
            case nameof(ResetPhysboneAction): {
                var row = new VisualElement {
                    style = {
                        flexDirection = FlexDirection.Row,
                        alignItems = Align.FlexStart
                    }
                };

                var label = new Label {
                    text = "Reset Physbone",
                    style = {
                        flexGrow = 0,
                        flexBasis = 100
                    }
                };
                row.Add(label);

                var propField = VRCFuryEditorUtils.Prop(prop.FindPropertyRelative("physBone"));
                propField.style.flexGrow = 1;
                row.Add(propField);

                return row;
            }
            case nameof(FlipBookBuilderAction): {
                var output = new VisualElement();
                output.Add(new Label {
                    text = "Flipbook Builder"
                });
                output.Add(VRCFuryEditorUtils.Info(
                    "This will create a clip made up of one frame per child action. This is mostly useful for" +
                    " VRCFury Toggles with 'Use Slider Wheel' enabled, as you can put various presets in these slots" +
                    " and use the slider to select one of them."
                ));
                output.Add(VRCFuryEditorUtils.List(prop.FindPropertyRelative("states")));
                return output;
            }
        }

        return VRCFuryEditorUtils.WrappedLabel($"Unknown action type: {type}");
    }

    private static VisualElement RendererSelector(SerializedProperty allRenderersProp, SerializedProperty rendererProp) {
        var content = new VisualElement();

        var allRenderersField = VRCFuryEditorUtils.Prop(allRenderersProp, "Apply to all renderers");
        content.Add(allRenderersField);

        var rendererField = VRCFuryEditorUtils.Prop(rendererProp, "Renderer");
        content.Add(rendererField);

        void UpdateVisibility() {
            var visible = !allRenderersProp.boolValue;
            rendererField.SetVisible(visible);
            if (!visible) {
                rendererProp.objectReferenceValue = null;
                rendererProp.serializedObject.ApplyModifiedPropertiesWithoutUndo();
            }
        }
        UpdateVisibility();
        allRenderersField.RegisterCallback<ChangeEvent<bool>>(e => UpdateVisibility());
        return content;
    }
}

}
