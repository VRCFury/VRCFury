using UnityEditor;
using UnityEngine.UIElements;
using VF.Builder;
using VF.Feature.Base;
using VF.Inspector;
using VF.Model.Feature;
using VF.Utils;

namespace VF.Feature {
    [FeatureTitle("Menu Folder")]
    internal class MenuFolderBuilder : FeatureBuilder<MenuFolder> {
        
        [FeatureEditor]
        public static VisualElement Editor(SerializedProperty prop, VFGameObject avatarObject, VFGameObject componentObject) {
            var content = new VisualElement();
            content.Add(VRCFuryEditorUtils.Info("VRCFury Toggles and Full Controllers that are children of this object will automatically be placed in the specified subfolder."));

            var folderPath = prop.FindPropertyRelative("folderPath");
            
            content.Add(MoveMenuItemBuilder.SelectButton(avatarObject, componentObject.parent, true, folderPath, label: "Folder Path"));
            content.Add(VRCFuryEditorUtils.Debug(refreshMessage: () => {
                var fullPath = MenuManager.PrependFolders("", componentObject);
                if (!string.IsNullOrEmpty(fullPath)) return "Full Folder Path: " + fullPath;
                return "";
            }));

            return content;
        }

      
    }
}
