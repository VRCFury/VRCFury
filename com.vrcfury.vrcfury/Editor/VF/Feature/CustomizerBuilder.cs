using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using VF.Feature.Base;
using VF.Inspector;
using VF.Model.Feature;

namespace VF.Feature {
    [FeatureTitle("Customizer")]
    [FeatureHideInMenu]
    internal class CustomizerBuilder : FeatureBuilder<Customizer> {
        
        [CustomPropertyDrawer(typeof(Customizer.CustomizerItem))]
        public class CustomizerItemDrawer : PropertyDrawer {
            public override VisualElement CreatePropertyGUI(SerializedProperty prop) {
                var type = VRCFuryEditorUtils.GetManagedReferenceTypeName(prop);
                switch (type) {
                    case nameof(Customizer.MenuItem): {
                        var row = new VisualElement();
                        row.Add(VRCFuryEditorUtils.Prop(prop.FindPropertyRelative("title"), "Title"));
                        row.Add(VRCFuryEditorUtils.Prop(prop.FindPropertyRelative("path"), "Menu Path"));
                        return row;
                    }
                    case nameof(Customizer.ClipItem): {
                        var row = new VisualElement();
                        row.Add(VRCFuryEditorUtils.Prop(prop.FindPropertyRelative("title"), "Title"));
                        row.Add(VRCFuryEditorUtils.Prop(prop.FindPropertyRelative("clip"), "Animation Clip"));
                        return row;
                    }
                }
                return VRCFuryEditorUtils.WrappedLabel($"Unknown action type: {type}");
            }
        }

        [FeatureEditor]
        public static VisualElement Editor(SerializedProperty prop) {
            var c = new VisualElement();
            c.Add(VRCFuryEditorUtils.Info(
                "This feature lets you define things in your prefab which" +
                " users can customize. They will be able to customize these items" +
                " outside of the prefab, without modifying the prefab itself."));
            var list = prop.FindPropertyRelative("items");
            void OnPlus() {
                var menu = new GenericMenu();
                menu.AddItem(new GUIContent("Menu Path Override"), false,
                    () => { VRCFuryEditorUtils.AddToList(list, entry => entry.managedReferenceValue = new Customizer.MenuItem()); });
                menu.AddItem(new GUIContent("Animation Clip Override"), false,
                    () => { VRCFuryEditorUtils.AddToList(list, entry => entry.managedReferenceValue = new Customizer.ClipItem()); });
                menu.ShowAsContext();
            }
            c.Add(VRCFuryEditorUtils.List(list, OnPlus));
            return c;
        }
    }
}