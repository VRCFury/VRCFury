using UnityEditor;
using UnityEngine.UIElements;
using VF.Builder;
using VF.Feature.Base;
using VF.Inspector;
using VF.Model.Feature;

namespace VF.Feature {
    [FeatureTitle("Menu Folder")]
    internal class MenuFolderBuilder : FeatureBuilder<MenuFolder> {
        
        [FeatureEditor]
        public static VisualElement Editor(SerializedProperty prop, VFGameObject avatarObject, VFGameObject componentObject) {
            var content = new VisualElement();
            content.Add(VRCFuryEditorUtils.Info("VRCFury Toggles and Full Controllers that are children of this object will automatically be placed in the specified subfolder."));

            var folderPath = prop.FindPropertyRelative("folderPath");
            
            content.Add(MoveMenuItemBuilder.SelectButton(avatarObject, componentObject.parent, true, folderPath, label: "Folder Path"));

            return content;
        }

      
    }
}
