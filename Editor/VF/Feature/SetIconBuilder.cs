using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using VF.Builder;
using VF.Feature.Base;
using VF.Inspector;
using VF.Menu;
using VF.Model.Feature;
using VRC.SDK3.Avatars.ScriptableObjects;

namespace VF.Feature {
    public class SetIconBuilder : FeatureBuilder<SetIcon> {
        // We run this twice, once before MoveMenuItems and once after, so users can set the icon on either the
        // old or new path.
        [FeatureBuilderAction(FeatureOrder.SetMenuIcons1)]
        public void Apply1() {
            Apply();
        }
        [FeatureBuilderAction(FeatureOrder.SetMenuIcons2)]
        public void Apply2() {
            Apply();
        }
        
        public void Apply() {
            MoveMenuItemBuilder.GetMenu(manager.GetMenu(), model.path, false, out _, out _, out var controlName, out var parentMenu);
            if (!parentMenu) {
                Debug.LogWarning("Parent menu did not exist");
                return;
            }

            var controls = parentMenu.controls.Where(c => c.name == controlName).ToList();
            if (controls.Count == 0) {
                Debug.LogWarning("No menu control matched path");
                return;
            }

            foreach (var control in controls) {
                control.icon = model.icon;
            }
        }

        public override string GetEditorTitle() {
            return "Override Menu Icon";
        }
        
        public override VisualElement CreateEditor(SerializedProperty prop) {
            var content = new VisualElement();
            content.Add(VRCFuryEditorUtils.Info("This feature will override the menu icon for the given menu path. You can use slashes to look in subfolders."));
            content.Add(VRCFuryEditorUtils.Prop(prop.FindPropertyRelative("path"), "Menu Path"));
            content.Add(VRCFuryEditorUtils.Prop(prop.FindPropertyRelative("icon"), "Icon"));
            return content;
        }
    }
}