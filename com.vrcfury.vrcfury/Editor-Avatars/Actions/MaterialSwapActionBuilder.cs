using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using VF.Feature.Base;
using VF.Inspector;
using VF.Model.StateAction;
using VF.Utils;

namespace VF.Actions {
    [FeatureTitle("Material Swap")]
    internal class MaterialSwapActionBuilder : ActionBuilder<MaterialAction> {
        public AnimationClip Build(MaterialAction model) {
            var onClip = NewClip();
            var renderer = model.renderer;
            if (renderer == null) return onClip;
            var mat = model.mat?.Get();
            if (mat == null) return onClip;

            var propertyName = "m_Materials.Array.data[" + model.materialIndex + "]";
            onClip.SetCurve(renderer, propertyName, mat);
            return onClip;
        }

        [FeatureEditor]
        public static VisualElement Editor(SerializedProperty prop) {
            var content = new VisualElement();
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
    }
}
