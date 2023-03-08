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
    public class MoveMenuItemBuilder : FeatureBuilder<MoveMenuItem> {
        [FeatureBuilderAction(FeatureOrder.MoveMenuItems)]
        public void Apply() {
            var result = manager.GetMenu().Move(model.fromPath, model.toPath);
            if (!result) {
                Debug.LogWarning("Failed to find items in menu to move");
            }
        }

        public override string GetEditorTitle() {
            return "Move Menu Item";
        }
        
        public override VisualElement CreateEditor(SerializedProperty prop) {
            var content = new VisualElement();
            content.Add(VRCFuryEditorUtils.Info("This feature will move a menu item to another location. You can use slashes to make subfolders."));
            content.Add(VRCFuryEditorUtils.Prop(prop.FindPropertyRelative("fromPath"), "From Path"));
            content.Add(VRCFuryEditorUtils.Prop(prop.FindPropertyRelative("toPath"), "To Path"));
            return content;
        }

        public override bool AvailableOnProps() {
            return false;
        }
    }
}