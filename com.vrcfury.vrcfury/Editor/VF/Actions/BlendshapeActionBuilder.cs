using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using VF.Builder;
using VF.Feature.Base;
using VF.Injector;
using VF.Inspector;
using VF.Model.StateAction;
using VF.Utils;

namespace VF.Actions {
    [FeatureTitle("BlendShape")]
    internal class BlendshapeActionBuilder : ActionBuilder<BlendShapeAction> {
        [VFAutowired] private readonly VFGameObject avatarObject;
        
        public AnimationClip Build(BlendShapeAction model) {
            var onClip = NewClip();
            var foundOne = false;
            foreach (var skin in avatarObject.GetComponentsInSelfAndChildren<SkinnedMeshRenderer>()) {
                if (!model.allRenderers && model.renderer != skin) continue;
                if (!skin.HasBlendshape(model.blendShape)) continue;
                foundOne = true;
                onClip.SetCurve(skin, "blendShape." + model.blendShape, model.blendShapeValue);
            }
            if (!foundOne) {
                //Debug.LogWarning("BlendShape not found: " + blendShape.blendShape);
            }
            return onClip;
        }

        [FeatureEditor]
        public static VisualElement Editor(SerializedProperty prop, VFGameObject avatarObject) {
            var content = new VisualElement();

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
                    valueProp.serializedObject.ApplyModifiedPropertiesWithoutUndo();
                }
                if (e.newValue > 100) {
                    valueProp.floatValue = 100;
                    valueProp.serializedObject.ApplyModifiedPropertiesWithoutUndo();
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
                    blendshapeProp.serializedObject.ApplyModifiedProperties();
                });
            }

            return content;
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