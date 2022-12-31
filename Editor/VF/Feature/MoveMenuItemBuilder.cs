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
            GetMenu(manager.GetMenu(), model.fromPath, false, out var fromPath, out var fromPrefix, out var fromName, out var fromMenu);
            if (!fromMenu) {
                Debug.LogWarning("From menu did not exist");
                return;
            }
            
            GetMenu(manager.GetMenu(), model.toPath, true, out var toPath, out var toPrefix, out var toName, out var toMenu);

            var fromControls = fromMenu.controls.Where(c => c.name == fromName).ToList();
            if (fromControls.Count == 0) {
                Debug.LogWarning("No menu control matched fromPath");
                return;
            }

            fromMenu.controls.RemoveAll(c => fromControls.Contains(c));
            var menuManager = manager.GetMenu();
            foreach (var control in fromControls) {
                if (control.type == VRCExpressionsMenu.Control.ControlType.SubMenu) {
                    menuManager.GetSubmenu(toPath, createFromControl: control);
                    menuManager.MergeMenu(toPath, control.subMenu);
                } else {
                    control.name = toName;
                    var tmpMenu = ScriptableObject.CreateInstance<VRCExpressionsMenu>();
                    tmpMenu.controls.Add(control);
                    menuManager.MergeMenu(toPrefix, tmpMenu);
                }
            }
        }

        public static void GetMenu(
            MenuManager menu,
            string rawPath,
            bool create,
            out string[] path,
            out string[] prefix,
            out string name,
            out VRCExpressionsMenu prefixMenu
        ) {
            path = string.IsNullOrWhiteSpace(rawPath) ? new string[]{} : rawPath.Split('/');
            if (path.Length > 0) {
                prefix = MenuManager.Slice(path, path.Length - 1);
                name = path[path.Length - 1];
            } else {
                prefix = new string[]{};
                name = "";
            }
            prefixMenu = menu.GetSubmenu(prefix, createIfMissing: create);
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