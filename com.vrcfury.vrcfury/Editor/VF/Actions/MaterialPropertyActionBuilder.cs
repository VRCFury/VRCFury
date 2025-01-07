using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UIElements;
using VF.Builder;
using VF.Feature.Base;
using VF.Injector;
using VF.Inspector;
using VF.Model.StateAction;
using VF.Utils;
using Object = UnityEngine.Object;

namespace VF.Actions {
    [FeatureTitle("Material Property")]
    internal class MaterialPropertyActionBuilder : ActionBuilder<MaterialPropertyAction> {
        [VFAutowired] private readonly VFGameObject avatarObject;

        private static readonly MethodInfo lilTranslator = ReflectionUtils.GetTypeFromAnyAssembly("lilToon.lilLanguageManager")?
            .GetMethod("GetLoc", BindingFlags.Static | BindingFlags.Public, null, new[] { typeof(string) }, null);

        public AnimationClip Build(MaterialPropertyAction materialPropertyAction) {
            var onClip = NewClip();

            // Prevent people from trying to animate "one part" of a .x .y .z .w bundle which breaks things
            if (materialPropertyAction.propertyName.Contains(".")) {
                return onClip;
            }
            var renderers = FindRenderers(
                materialPropertyAction.affectAllMeshes,
                materialPropertyAction.renderer2.asVf()?.GetComponent<Renderer>(),
                avatarObject
            );
            var type = GetMaterialPropertyActionTypeToUse(
                renderers,
                materialPropertyAction.propertyName,
                materialPropertyAction.propertyType,
                false
            );

            foreach (var renderer in renderers) {
                void AddOne(string suffix, float value) {
                    var propertyName = $"material.{materialPropertyAction.propertyName}{suffix}";
                    onClip.SetCurve(renderer, propertyName, value);
                }

                if (type == MaterialPropertyAction.Type.Float) {
                    AddOne("", materialPropertyAction.value);
                } else if (type == MaterialPropertyAction.Type.Color) {
                    AddOne(".r", materialPropertyAction.valueColor.r);
                    AddOne(".g", materialPropertyAction.valueColor.g);
                    AddOne(".b", materialPropertyAction.valueColor.b);
                    AddOne(".a", materialPropertyAction.valueColor.a);
                } else if (type == MaterialPropertyAction.Type.Vector || type == MaterialPropertyAction.Type.St) {
                    AddOne(".x", materialPropertyAction.valueVector.x);
                    AddOne(".y", materialPropertyAction.valueVector.y);
                    AddOne(".z", materialPropertyAction.valueVector.z);
                    AddOne(".w", materialPropertyAction.valueVector.w);
                }
            }

            return onClip;
        }

        [FeatureEditor]
        public static VisualElement Editor(SerializedProperty prop, VFGameObject avatarObject) {
            var content = new VisualElement();

            var affectAllMeshesProp = prop.FindPropertyRelative("affectAllMeshes");
            var rendererProp = prop.FindPropertyRelative("renderer2");
            content.Add(RendererSelector(
                affectAllMeshesProp,
                rendererProp
            ));
            
            var valueFloat = VRCFuryEditorUtils.Prop(prop.FindPropertyRelative("value"), "Value");
            var valueVector = VRCFuryEditorUtils.Prop(prop.FindPropertyRelative("valueVector"), "Value");
            var valueColor = VRCFuryEditorUtils.Prop(prop.FindPropertyRelative("valueColor"), "Value");
            var valueStField = new VisualElement();
            var valueStScale = new VisualElement().Row().AddTo(valueStField);
            valueStScale.Add(new Label("Scale").FlexBasis(60));
            valueStScale.Add(new Label("X"));
            valueStScale.Add(VRCFuryEditorUtils.Prop(prop.FindPropertyRelative("valueVector.x")).FlexBasis(0).FlexGrow(1));
            valueStScale.Add(new Label("Y"));
            valueStScale.Add(VRCFuryEditorUtils.Prop(prop.FindPropertyRelative("valueVector.y")).FlexBasis(0).FlexGrow(1));
            var valueStOffset = new VisualElement().Row().AddTo(valueStField);
            valueStOffset.Add(new Label("Offset").FlexBasis(60));
            valueStOffset.Add(new Label("X"));
            valueStOffset.Add(VRCFuryEditorUtils.Prop(prop.FindPropertyRelative("valueVector.z")).FlexBasis(0).FlexGrow(1));
            valueStOffset.Add(new Label("Y"));
            valueStOffset.Add(VRCFuryEditorUtils.Prop(prop.FindPropertyRelative("valueVector.w")).FlexBasis(0).FlexGrow(1));
            var valueSt = VRCFuryEditorUtils.Prop(null, "Value", fieldOverride: valueStField);

            var propertyTypeProp = prop.FindPropertyRelative("propertyType");
            var propertyNameProp = prop.FindPropertyRelative("propertyName");
            {
                var row = new VisualElement().Row();
                row.Add(VRCFuryEditorUtils.Prop(propertyNameProp, "Property").FlexGrow(1));
                row.Add(VRCFuryEditorUtils.OnChange(propertyNameProp, () => UpdateValueType(true)));
                row.Add(new Button(SearchClick) { text = "Search" }.Margin(0));
                content.Add(row);
            }

            var typeBox = new VisualElement().AddTo(content);
            typeBox.Add(VRCFuryEditorUtils.OnChange(propertyTypeProp, () => UpdateValueType(false)));
            typeBox.Add(valueFloat);
            typeBox.Add(valueVector);
            typeBox.Add(valueColor);
            typeBox.Add(valueSt);
            
            typeBox.AddManipulator(new ContextualMenuManipulator(e => {
                if (e.menu.MenuItems().Count > 0) {
                    e.menu.AppendSeparator();
                }
                e.menu.AppendAction("Force type to Float", a => {
                    propertyTypeProp.enumValueIndex = (int)MaterialPropertyAction.Type.Float;
                    propertyTypeProp.serializedObject.ApplyModifiedProperties();
                }, propertyTypeProp.enumValueIndex == (int)MaterialPropertyAction.Type.Float ? DropdownMenuAction.Status.Checked : DropdownMenuAction.Status.Normal);
                e.menu.AppendAction("Force type to Color", a => {
                    propertyTypeProp.enumValueIndex = (int)MaterialPropertyAction.Type.Color;
                    propertyTypeProp.serializedObject.ApplyModifiedProperties();
                }, propertyTypeProp.enumValueIndex == (int)MaterialPropertyAction.Type.Color ? DropdownMenuAction.Status.Checked : DropdownMenuAction.Status.Normal);
                e.menu.AppendAction("Force type to Vector", a => {
                    propertyTypeProp.enumValueIndex = (int)MaterialPropertyAction.Type.Vector;
                    propertyTypeProp.serializedObject.ApplyModifiedProperties();
                }, propertyTypeProp.enumValueIndex == (int)MaterialPropertyAction.Type.Vector ? DropdownMenuAction.Status.Checked : DropdownMenuAction.Status.Normal);
                e.menu.AppendAction("Force type to Texture Scale+Offset", a => {
                    propertyTypeProp.enumValueIndex = (int)MaterialPropertyAction.Type.St;
                    propertyTypeProp.serializedObject.ApplyModifiedProperties();
                }, propertyTypeProp.enumValueIndex == (int)MaterialPropertyAction.Type.St ? DropdownMenuAction.Status.Checked : DropdownMenuAction.Status.Normal);
            }));
            
            UpdateValueType(false);

            void UpdateValueType(bool redetectType) {
                var propName = propertyNameProp.stringValue;
                var renderers = FindRenderers(
                    affectAllMeshesProp.boolValue,
                    rendererProp.GetComponent<Renderer>(),
                    avatarObject
                );
                var oldType = (MaterialPropertyAction.Type)propertyTypeProp.enumValueIndex;
                var newType = GetMaterialPropertyActionTypeToUse(
                    renderers, propName, oldType, redetectType);
                if (newType != oldType) {
                    propertyTypeProp.enumValueIndex = (int)newType;
                    propertyTypeProp.serializedObject.ApplyModifiedPropertiesWithoutUndo();
                }

                valueFloat.SetVisible(newType == MaterialPropertyAction.Type.Float);
                valueVector.SetVisible(newType == MaterialPropertyAction.Type.Vector);
                valueColor.SetVisible(newType == MaterialPropertyAction.Type.Color);
                valueSt.SetVisible(newType == MaterialPropertyAction.Type.St);
            }

            return content;

            void SearchClick() {
                var searchWindow = new VrcfSearchWindow("Material Properties");
                GetTreeEntries(searchWindow);
                searchWindow.Open(value => {
                    propertyNameProp.stringValue = value;
                    propertyNameProp.serializedObject.ApplyModifiedProperties();
                });
            }

            void GetTreeEntries(VrcfSearchWindow searchWindow) {
                var mainGroup = searchWindow.GetMainGroup();
                var renderers = FindRenderers(
                    affectAllMeshesProp.boolValue,
                    rendererProp.GetComponent<Renderer>(),
                    avatarObject
                );
                if (renderers.Count == 0) return;

                foreach (var renderer in renderers) {
                    if (renderer == null) continue;
                    var sharedMaterials = renderer.sharedMaterials;
                    if (sharedMaterials.Length == 0) continue;

                    var rendererGroup = renderers.Count > 1
                        ? mainGroup.AddGroup("Mesh: " + renderer.owner().GetPath(avatarObject))
                        : mainGroup;
                    foreach (var material in sharedMaterials) {
                        if (material == null) continue;

                        var matGroup = sharedMaterials.Length > 1
                            ? rendererGroup.AddGroup("Material: " + material.name)
                            : rendererGroup;
                        var shader = material.shader;
                        
                        if (shader == null) continue;

                        var count = shader.GetPropertyCount();
                        var sectionStack = new Stack<string>();
                        for (var i = 0; i < count; i++) {
                            var propertyName = shader.GetPropertyName(i);
                            if (propertyName == "_DummyProperty") continue;
                            var description = shader.GetPropertyDescription(i);
                            if (description.Contains("{condition_showS:(0==1)}")) continue;
                            
                            var readableName = description;
                            readableName = readableName.RemoveAfter("--");

                            if (shader.name.Contains("lil") && lilTranslator != null) {
                                readableName = (string)lilTranslator.Invoke(null, new[] { readableName });
                            }

                            if (propertyName.StartsWith("m_start")) {
                                sectionStack.Push(readableName.RemoveHtmlTags().NormalizeSpaces());
                            } else if (propertyName.StartsWith("m_end")) {
                                sectionStack.Pop();
                            } else if (propertyName.StartsWith("m_")) {
                                sectionStack.Clear();
                                sectionStack.Push(readableName.RemoveHtmlTags().NormalizeSpaces());
                            }

                            readableName = readableName.NormalizeSpaces();
                            var showPropertyNameSuffix = true;
                            if (string.IsNullOrEmpty(readableName) || readableName == propertyName) {
                                readableName = propertyName;
                                showPropertyNameSuffix = false;
                            }

                            if (sectionStack.Any()) {
                                readableName = sectionStack.Reverse().Join(" > ") + " > " + readableName;
                            }

                            var propType = shader.GetPropertyType(i);
                            if (propType != ShaderPropertyType.Float &&
                                propType != ShaderPropertyType.Range &&
                                propType != ShaderPropertyType.Color &&
                                propType != ShaderPropertyType.Vector &&
                                propType != ShaderPropertyType.Texture
                            ) continue;

                            var flags = shader.GetPropertyFlags(i);
                            var isHidden = (flags & ShaderPropertyFlags.HideInInspector) != 0;
                            if (isHidden) continue;

                            var attributes = shader.GetPropertyAttributes(i);
                            if (attributes.Any(a => a.StartsWith("ThryToggle"))) continue;
                            if (attributes.Any(a => a.StartsWith("DoNotAnimate"))) continue;
                            if (attributes.Any(a => a.StartsWith("NoAnimate"))) continue;
                            if (attributes.Any(a => a.StartsWith("Helpbox"))) continue;
                            if (attributes.Any(a => a.StartsWith("ThryShaderOptimizerLockButton"))) continue;
                            if (attributes.Any(a => a.StartsWith("ThryWideEnum"))) continue;

                            if (propType == ShaderPropertyType.Texture) {
                                propertyName += "_ST";
                            }

                            var entryName = readableName;
                            if (propType == ShaderPropertyType.Texture) {
                                entryName += " (Scale+Offset)";
                            }
                            if (renderers.Count > 1) {
                                entryName += $" (Mesh: {renderer.owner().GetPath(avatarObject)})";
                            }
                            if (sharedMaterials.Length > 1) {
                                entryName += $" (Mat: {material.name})";
                            }
                            if (showPropertyNameSuffix) {
                                entryName += $" ({propertyName})";
                            }
                            matGroup.Add(entryName, propertyName);
                        }
                    }    
                }
            }
        }
        
        public static VisualElement RendererSelector(SerializedProperty allRenderersProp, SerializedProperty rendererProp) {
            var content = new VisualElement();

            var allRenderersField = VRCFuryEditorUtils.Prop(allRenderersProp, "Apply to all renderers");
            content.Add(allRenderersField);

            VisualElement rendererField;
            if (VRCFuryEditorUtils.GetPropertyType(rendererProp) == typeof(GameObject)) {
                rendererField = VRCFuryEditorUtils.Prop(
                    null,
                    "Renderer",
                    fieldOverride: VRCFuryEditorUtils.FilteredGameObjectProp<Renderer>(rendererProp)
                );
            } else {
                rendererField = VRCFuryEditorUtils.Prop(rendererProp, "Renderer");
            }
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
            content.Add(VRCFuryEditorUtils.OnChange(allRenderersProp, UpdateVisibility));
            return content;
        }
        
        private static IList<Renderer> FindRenderers(
            bool allRenderers,
            Renderer singleRenderer,
            VFGameObject avatarObject
        ) {
            IList<Renderer> renderers;
            if (allRenderers) {
                renderers = avatarObject.GetComponentsInSelfAndChildren<Renderer>();
            } else {
                renderers = new[] { singleRenderer };
            }
            renderers = renderers.NotNull().ToArray();
            return renderers;
        }

        private static ShaderUtil.ShaderPropertyType? FindMaterialPropertyType(
            IList<Renderer> renderers,
            string propName
        ) {
            return renderers
                .Select(r => r.GetPropertyType(propName))
                .NotNull()
                .DefaultIfEmpty(null)
                .First();
        }

        private static MaterialPropertyAction.Type GetMaterialPropertyActionTypeToUse(
            IList<Renderer> renderers,
            string propName,
            MaterialPropertyAction.Type setting,
            bool forceRedetect
        ) {
            if (!forceRedetect && setting != MaterialPropertyAction.Type.LegacyAuto) {
                return setting;
            }
            switch (FindMaterialPropertyType(renderers, propName)) {
                case ShaderUtil.ShaderPropertyType.Color:
                    return MaterialPropertyAction.Type.Color;
                case ShaderUtil.ShaderPropertyType.Vector:
                    return MaterialPropertyAction.Type.Vector;
                case MaterialExtensions.StPropertyType:
                    return MaterialPropertyAction.Type.St;
                case null:
                    return setting == MaterialPropertyAction.Type.LegacyAuto
                        ? MaterialPropertyAction.Type.Float
                        : setting;
                default:
                    return MaterialPropertyAction.Type.Float;
            }
        }
    }
}
