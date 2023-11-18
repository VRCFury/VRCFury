using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using VF.Builder;
using VF.Model.StateAction;
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
        
        var avatarObject = (prop.serializedObject.targetObject as UnityEngine.Component)?
            .owner()
            .GetComponentInSelfOrParent<VRCAvatarDescriptor>()?
            .owner();

        string GetPath(VFGameObject obj) {
            return avatarObject == null ? obj.name : obj.GetPath(avatarObject);
        }

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
            case nameof(ShaderInventoryAction): {
                var row = new VisualElement {
                    style = {
                        flexDirection = FlexDirection.Row,
                        alignItems = Align.FlexStart
                    }
                };

                var label = new Label("Shader Inventory") {
                    style = {
                        flexGrow = 0,
                        flexBasis = 100
                    }
                };
                row.Add(label);

                var propField = VRCFuryEditorUtils.Prop(prop.FindPropertyRelative("renderer"));
                propField.style.flexGrow = 1;
                row.Add(propField);
            
                var propField3 = VRCFuryEditorUtils.Prop(prop.FindPropertyRelative("slot"));
                propField3.style.flexGrow = 0;
                propField3.style.flexBasis = 30;
                row.Add(propField3);

                return row;
            }
            case nameof(PoiyomiUVTileAction): {
                var content = new VisualElement();
                var row = new VisualElement {
                    style = {
                        alignItems = Align.FlexStart,
                        flexDirection = FlexDirection.Row,
                    }
                };

                var label = new Label("Poiyomi UV Tile") {
                    style = {
                        flexGrow = 0,
                        flexBasis = 100,
                    }
                };
                row.Add(label);

                var propField = VRCFuryEditorUtils.Prop(prop.FindPropertyRelative("renderer"));
                propField.style.flexGrow = 1;
                row.Add(propField);

                row.Add(new Label("Row") {
                    style = {
                        flexGrow = 0,
                        flexShrink = 0,
                        flexBasis = 30,
                        unityTextAlign = TextAnchor.MiddleCenter,
                    }
                });
                row.Add(VRCFuryEditorUtils.Prop(prop.FindPropertyRelative("row"), style: s => {
                    s.flexGrow = 0;
                    s.flexShrink = 0;
                    s.flexBasis = 20;
                }));

                row.Add(new Label("Col") {
                    style = {
                        flexGrow = 0,
                        flexShrink = 0,
                        flexBasis = 30,
                        unityTextAlign = TextAnchor.MiddleCenter,
                    }
                });
                row.Add(VRCFuryEditorUtils.Prop(prop.FindPropertyRelative("column"), style: s => {
                    s.flexGrow = 0;
                    s.flexShrink = 0;
                    s.flexBasis = 20;
                }));

                content.Add(row);

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
                var row = new VisualElement {
                    style = {
                        flexDirection = FlexDirection.Row,
                        alignItems = Align.FlexStart
                    }
                };

                var label = new Label("Material Property") {
                    style = {
                        flexGrow = 0,
                        flexBasis = 100
                    }
                };

                var col = new VisualElement
                {
                    style =
                    {
                        flexDirection = FlexDirection.Column,
                        flexGrow = 1
                    }
                };
                
                row.Add(label);

                var rendererRow = new VisualElement {
                    style = {
                        flexDirection = FlexDirection.Row,
                        alignItems = Align.Center,
                        marginBottom = 1
                    }
                };

                var affectAllMeshesProp = prop.FindPropertyRelative("affectAllMeshes");
                
                var rendererProp = prop.FindPropertyRelative("renderer");
                var propField = VRCFuryEditorUtils.Prop(rendererProp);
                propField.style.flexGrow = 1;
                propField.style.flexShrink = 1;
                propField.SetEnabled(!affectAllMeshesProp.boolValue);
                rendererRow.Add(propField);
                
                rendererRow.Add(new Label("All Meshes") {
                    style = {
                        marginLeft = 2,
                        marginRight = 2,
                        flexGrow = 1,
                        flexBasis = 100,
                        unityTextAlign = TextAnchor.MiddleRight
                    }
                });
                
                var propField4 = VRCFuryEditorUtils.RefreshOnChange(() => {
                    propField.SetEnabled(!affectAllMeshesProp.boolValue);
                    var field = VRCFuryEditorUtils.Prop(affectAllMeshesProp);
                    return field;
                }, affectAllMeshesProp);
                propField4.style.flexGrow = 0;
                propField4.style.flexShrink = 0;
                propField4.style.flexBasis = 16;
                rendererRow.Add(propField4);
                
                col.Add(rendererRow);
                
                var materialRow = new VisualElement {
                    style = {
                        flexDirection = FlexDirection.Row,
                        alignItems = Align.FlexStart
                    }
                };
            
                var propertyNameProp = prop.FindPropertyRelative("propertyName");
                var propField2 = VRCFuryEditorUtils.Prop(propertyNameProp);
                propField2.style.flexGrow = 1;
                propField2.tooltip = "Property Name";
                materialRow.Add(propField2);
                
                var propField3 = VRCFuryEditorUtils.Prop(prop.FindPropertyRelative("value"));
                propField3.style.flexGrow = 0;
                propField3.style.flexBasis = 60;
                propField3.tooltip = "Property Value";
                materialRow.Add(propField3);

                var searchButton = new Button(SearchClick)
                {
                    text = "Search",
                    style =
                    {
                        marginTop = 0,
                        marginLeft = 0,
                        marginRight = 0,
                        marginBottom = 0
                    }
                };
                materialRow.Add(searchButton);
                col.Add(materialRow);
                
                row.Add(col);

                return row;

                void SearchClick() {
                    var targetWidth = row.GetFirstAncestorOfType<UnityEditor.UIElements.InspectorElement>().worldBound
                        .width;
                    var searchContext = new UnityEditor.Experimental.GraphView.SearchWindowContext(GUIUtility.GUIToScreenPoint(Event.current.mousePosition), targetWidth, 300);
                    var provider = ScriptableObject.CreateInstance<VRCFurySearchWindowProvider>();
                    provider.InitProvider(GetTreeEntries, (entry, userData) => {
                        propertyNameProp.stringValue = (string) entry.userData;
                        prop.serializedObject.ApplyModifiedProperties();
                        return true;
                    });
                    UnityEditor.Experimental.GraphView.SearchWindow.Open(searchContext, provider);
                }

                List<UnityEditor.Experimental.GraphView.SearchTreeEntry> GetTreeEntries() {
                    var entries = new List<UnityEditor.Experimental.GraphView.SearchTreeEntry> {
                        new UnityEditor.Experimental.GraphView.SearchTreeGroupEntry(new GUIContent("Material Properties"))
                    };
                    var renderers = new List<Renderer>();
                    if (affectAllMeshesProp.boolValue) {
                        if (avatarObject != null) {
                            renderers.AddRange(avatarObject.GetComponentsInSelfAndChildren<Renderer>());
                        }
                    } else {
                        renderers.Add(rendererProp.objectReferenceValue as Renderer);
                    }

                    if (renderers.Count == 0) return entries;
                    
                    var singleRenderer = renderers.Count == 1;
                    foreach (var renderer in renderers) {
                        if (renderer == null) continue;
                        var nest = 1;
                        var sharedMaterials = renderer.sharedMaterials;
                        if (sharedMaterials.Length == 0) return entries;
                        var singleMaterial = sharedMaterials.Length == 1;
                        if (!singleRenderer) {
                            entries.Add(new UnityEditor.Experimental.GraphView.SearchTreeGroupEntry(new GUIContent("Mesh: " + GetPath(renderer.owner())), nest));
                        }
                        foreach (var material in sharedMaterials) {
                            if (material == null) continue;
                            
                            nest = singleRenderer ? 1 : 2;
                            if (!singleMaterial) {
                                entries.Add(new UnityEditor.Experimental.GraphView.SearchTreeGroupEntry(new GUIContent("Material: " + material.name),  nest));
                                nest++;
                            }
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
                                if (!singleRenderer) {
                                    entryName += $" (Mesh: {GetPath(renderer.owner())})";
                                }
                                if (!singleMaterial) {
                                    entryName += $" (Mat: {material.name})";
                                }

                                entryName += prioritizePropName ? $" ({readableName})" : $" ({propertyName})";
                                entries.Add(new UnityEditor.Experimental.GraphView.SearchTreeEntry(new GUIContent(entryName))
                                {
                                    level = nest,
                                    userData = propertyName
                                });
                            }
                        }    
                    }
                    return entries;
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
                    var shapes = new Dictionary<string,string>();
                    if (avatarObject != null) {
                        foreach (var skin in avatarObject.GetComponentsInSelfAndChildren<SkinnedMeshRenderer>()) {
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
            case nameof(BlockBlinkingAction): {
                return new Label {
                    text = "Disable Blinking"
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
            case nameof(SetGlobalParamAction): {
                var row = new VisualElement {
                    style = {
                        flexDirection = FlexDirection.Row,
                        alignItems = Align.FlexStart
                    }
                };

                var label = new Label("Set a Global Param") {
                    style = {
                        flexGrow = 0,
                        flexBasis = 120
                    }
                };
                row.Add(label);

                var propField = VRCFuryEditorUtils.Prop(prop.FindPropertyRelative("param"));
                propField.style.flexGrow = 1;
                row.Add(propField);
                
                var valueField = VRCFuryEditorUtils.Prop(prop.FindPropertyRelative("value"));
                valueField.style.flexBasis = 30;
                row.Add(valueField);

                return row;
            }
        }

        return VRCFuryEditorUtils.WrappedLabel($"Unknown action type: {type}");
    }
}

}
