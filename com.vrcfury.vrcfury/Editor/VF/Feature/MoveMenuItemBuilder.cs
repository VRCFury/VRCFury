using System.Collections.Generic;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using VF.Feature.Base;
using VF.Inspector;
using VF.Model.Feature;
using VF.Utils;

namespace VF.Feature {
    internal class MoveMenuItemBuilder : FeatureBuilder<MoveMenuItem> {
        public override string GetEditorTitle() {
            return "Move Menu Items";
        }

        public override VisualElement CreateEditor(SerializedProperty prop) {
            var container = new VisualElement();

            container.Add(VRCFuryEditorUtils.Info("This feature will move menu items to another location. You can use slashes to make subfolders."));

            var menuItemsContainer = new VisualElement();
            container.Add(menuItemsContainer);

            VisualElement RenderMenuList() {
                var output = new VisualElement();
                var header = new VisualElement().Row();
                header.Add(VRCFuryEditorUtils.WrappedLabel("From Path").FlexGrow(1));
                header.Add(VRCFuryEditorUtils.WrappedLabel("To Path").FlexGrow(1));
                output.Add(header);
                output.Add(new VisualElement().Row());

                var menuItems = prop.FindPropertyRelative("menuItems");

                menuItemsContainer.Add(output);
                output.Add(VRCFuryEditorUtils.List(menuItems,
                    onPlus: () => VRCFuryEditorUtils.AddToList(menuItems)));

                return output;
            }

            menuItemsContainer.Clear();
            menuItemsContainer.Add(RenderMenuList());
            menuItemsContainer.Bind(prop.serializedObject);

            return container;
        }

        [CustomPropertyDrawer(typeof(MoveMenuItem.MenuItem))]
        public class MenuItemsDrawer : PropertyDrawer {
            public override VisualElement CreatePropertyGUI(SerializedProperty prop) {
                var output = new VisualElement().Row();

                output.Add(VRCFuryEditorUtils.Prop(
                    prop.FindPropertyRelative("fromPath"))
                    .FlexBasis(0)
                    .FlexGrow(1)
                    .Margin(0, 2));

                output.Add(VRCFuryEditorUtils.Prop(prop.FindPropertyRelative("toPath")).FlexBasis(0).FlexGrow(1));

                return output;
            }
        }

        public override bool AvailableOnRootOnly() {
            return true;
        }
    }
}