using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using VF.Builder;
using VF.Model.StateAction;
using VF.Service;
using VF.Utils;
using VRC.SDK3.Avatars.Components;
using Object = UnityEngine.Object;

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
            e.menu.AppendAction("Android Only", a => {
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
                content.Add(Title("Material Swap"));
                content.Add(VRCFuryEditorUtils.Prop(prop.FindPropertyRelative("renderer"), "Renderer"));
                content.Add(VRCFuryEditorUtils.Prop(prop.FindPropertyRelative("materialIndex"), "Slot Number"));
                content.Add(VRCFuryEditorUtils.Prop(prop.FindPropertyRelative("mat"), "Material"));
                return content;
            }
            case nameof(FlipbookAction): {
                var output = new VisualElement();
                output.Add(Title("Poiyomi Flipbook Frame"));
                output.Add(VRCFuryEditorUtils.Prop(prop.FindPropertyRelative("renderer"), "Renderer"));
                output.Add(VRCFuryEditorUtils.Prop(prop.FindPropertyRelative("frame"), "Frame Number"));
                return output;
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
            case nameof(MaterialPropertyAction): {
                var content = new VisualElement();

                content.Add(Title("Material Property"));

                var affectAllMeshesProp = prop.FindPropertyRelative("affectAllMeshes");
                var rendererProp = prop.FindPropertyRelative("renderer");
                content.Add(RendererSelector(
                    affectAllMeshesProp,
                    rendererProp
                ));
                
                var valueFloat = VRCFuryEditorUtils.Prop(prop.FindPropertyRelative("value"), "Value");
                var valueVector = VRCFuryEditorUtils.Prop(prop.FindPropertyRelative("valueVector"), "Value");
                var valueColor = VRCFuryEditorUtils.Prop(prop.FindPropertyRelative("valueColor"), "Value");

                var propertyNameProp = prop.FindPropertyRelative("propertyName");
                {
                    var row = new VisualElement().Row();
                    var propField = VRCFuryEditorUtils.Prop(propertyNameProp, "Property").FlexGrow(1);
                    propField.RegisterCallback<ChangeEvent<string>>(e => UpdateValueType());
                    row.Add(propField);
                    row.Add(new Button(SearchClick) { text = "Search" }.Margin(0));
                    content.Add(row);
                }

                content.Add(valueFloat);
                content.Add(valueVector);
                content.Add(valueColor);
                UpdateValueType();

                void UpdateValueType() {
                    var (_, valueType) = ActionClipService.MatPropLookup(
                        affectAllMeshesProp.boolValue,
                        rendererProp.objectReferenceValue as Renderer,
                        avatarObject,
                        propertyNameProp.stringValue
                    );
                    valueFloat.SetVisible(valueType != ShaderUtil.ShaderPropertyType.Color && valueType != ShaderUtil.ShaderPropertyType.Vector);
                    valueVector.SetVisible(valueType == ShaderUtil.ShaderPropertyType.Vector);
                    valueColor.SetVisible(valueType == ShaderUtil.ShaderPropertyType.Color);
                }

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
                    var (renderers,_) = ActionClipService.MatPropLookup(
                        affectAllMeshesProp.boolValue,
                        rendererProp.objectReferenceValue as Renderer,
                        avatarObject,
                        null
                    );
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
                            for (var i = 0; i < count; i++) {
                                var propertyName = ShaderUtil.GetPropertyName(shader, i);
                                var readableName = ShaderUtil.GetPropertyDescription(shader, i);
                                var propType = ShaderUtil.GetPropertyType(shader, i);
                                if (propType != ShaderUtil.ShaderPropertyType.Float &&
                                    propType != ShaderUtil.ShaderPropertyType.Range &&
                                    propType != ShaderUtil.ShaderPropertyType.Color &&
                                    propType != ShaderUtil.ShaderPropertyType.Vector) continue;
                                var matProp = System.Array.Find(materialProperties, p => p.name == propertyName);
                                if ((matProp.flags & MaterialProperty.PropFlags.HideInInspector) != 0) continue;

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
                var row = new VisualElement();
                row.Add(Title("Scale"));
                row.Add(VRCFuryEditorUtils.Prop(prop.FindPropertyRelative("obj"), "Object"));
                row.Add( VRCFuryEditorUtils.Prop(prop.FindPropertyRelative("scale"), "Multiplier"));
                return row;
            }
            case nameof(ObjectToggleAction): {
                var row = new VisualElement().Row();

                row.Add(VRCFuryEditorUtils.Prop(
                    prop.FindPropertyRelative("mode"),
                    formatEnum: str => {
                        if (str == "Toggle") return "Flip State (Deprecated)";
                        return str;
                    }
                ).FlexBasis(100));

                row.Add(VRCFuryEditorUtils.Prop(prop.FindPropertyRelative("obj")).FlexGrow(1));

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
                content.Add(RendererSelector(allRenderersProp, rendererProp));

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
                    var window = new VrcfSearchWindow("Blendshapes");
                    var allRenderers = allRenderersProp.boolValue;
                    var singleRenderer = rendererProp.objectReferenceValue as Renderer;

                    var shapes = new Dictionary<string, string>();
                    if (avatarObject != null) {
                        foreach (var skin in avatarObject.GetComponentsInSelfAndChildren<SkinnedMeshRenderer>()) {
                            if (!allRenderers && skin != singleRenderer) continue;
                            foreach (var bs in skin.GetBlendshapeNames()) {
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
                var row = new VisualElement().Row();
                row.Add(Title("Animation Clip").FlexBasis(100));
                row.Add(VRCFuryEditorUtils.Prop(prop.FindPropertyRelative("clip")).FlexGrow(1));
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
                    " VRCFury Toggles with 'Use Slider Wheel' enabled, as you can put various presets in these slots" +
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
}

}
