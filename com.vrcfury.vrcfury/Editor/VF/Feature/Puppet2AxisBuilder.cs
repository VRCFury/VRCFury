using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using VF.Builder;
using VF.Feature.Base;
using VF.Injector;
using VF.Inspector;
using VF.Model.Feature;
using VF.Service;
using VF.Utils;

namespace VF.Feature {

    [FeatureTitle("2Axis Puppet")]
    [FeatureRootOnly]
    internal class Puppet2AxisBuilder : FeatureBuilder<Puppet2Axis> {
        [VFAutowired] private readonly GlobalsService globals;
        public const string menuPathTooltip = "This is where you'd like the puppet to be located in the menu. This is unrelated"
                                              + " to the menu filenames -- simply enter the title you'd like to use. If you'd like the puppet to be in a submenu, use slashes. For example:\n\n"
                                              + "If you want the puppet to be called 'Shirt' in the root menu, you'd put:\nShirt\n\n"
                                              + "If you want the puppet to be called 'Pants' in a submenu called 'Clothing', you'd put:\nClothing/Pants";

        [FeatureBuilderAction]
        public void Apply() {
            var puppet = new Puppet {
                name = model.name,
                enableIcon = model.enableIcon,
                icon = model.icon,
                defaultX = model.setDefaults ? model.defaultX : 0,
                defaultY = model.setDefaults ? model.defaultY : 0,
                saved = model.saved
            };
            puppet.stops.Add(new Puppet.Stop(0,-1,model.down));
            puppet.stops.Add(new Puppet.Stop(0,1,model.up));
            puppet.stops.Add(new Puppet.Stop(-1,0,model.left));
            puppet.stops.Add(new Puppet.Stop(1,0,model.right));
            if (puppet.stops.Count > 0) {
                globals.addOtherFeature(puppet);
            }
        }

        [FeatureEditor]
        public static VisualElement Editor(SerializedProperty prop, VFGameObject avatarObject, VFGameObject componentObject) {
            var content = new VisualElement();
            var savedProp = prop.FindPropertyRelative("saved");
            var setDefaultsProp = prop.FindPropertyRelative("setDefaults");
            var enableIconProp = prop.FindPropertyRelative("enableIcon");
            var flex = new VisualElement().Row();
            content.Add(flex);

            var pathProp = prop.FindPropertyRelative("name");
            flex.Add(VRCFuryEditorUtils.Prop(pathProp, "Menu Path", tooltip: menuPathTooltip).FlexGrow(1));
            var button = new Button()
                .Text("Options")
                .OnClick(() => {
                    var advMenu = new GenericMenu();
                    var pos = Event.current.mousePosition;

                    advMenu.AddItem(new GUIContent("Select Menu Folder"), false, () => {
                        MoveMenuItemBuilder.SelectButton(
                            avatarObject,
                            true,
                            pathProp,
                            append: () => MoveMenuItemBuilder.GetLastMenuSlug(pathProp.stringValue, "New Toggle"),
                            immediate: true,
                            pos: pos
                        );
                    });

                    advMenu.AddItem(new GUIContent("Saved Between Worlds"), savedProp.boolValue, () => {
                        savedProp.boolValue = !savedProp.boolValue;
                        prop.serializedObject.ApplyModifiedProperties();
                    });
                    
                    advMenu.AddItem(new GUIContent("Set Custom Menu Icon"), enableIconProp.boolValue, () => {
                        enableIconProp.boolValue = !enableIconProp.boolValue;
                        prop.serializedObject.ApplyModifiedProperties();
                    });

                    advMenu.AddItem(new GUIContent("Set Default Values"), setDefaultsProp.boolValue, () => {
                        setDefaultsProp.boolValue = !setDefaultsProp.boolValue;
                        prop.serializedObject.ApplyModifiedProperties();
                    });

                    advMenu.ShowAsContext();
                });
            flex.Add(button);
            
            content.Add(VRCFuryEditorUtils.RefreshOnChange(() => {
                var c = new VisualElement();
                if (enableIconProp.boolValue) {
                    c.Add(VRCFuryEditorUtils.Prop(prop.FindPropertyRelative("icon"), "Menu Icon"));
                }
                return c;
            }, enableIconProp));
            content.Add(VRCFuryActionSetDrawer.render(prop.FindPropertyRelative("up"), "Up"));
            content.Add(VRCFuryActionSetDrawer.render(prop.FindPropertyRelative("right"), "Right"));
            content.Add(VRCFuryActionSetDrawer.render(prop.FindPropertyRelative("down"), "Down"));
            content.Add(VRCFuryActionSetDrawer.render(prop.FindPropertyRelative("left"), "Left"));
            content.Add(VRCFuryEditorUtils.RefreshOnChange(() => {
                var output = new VisualElement();
                if (!setDefaultsProp.boolValue) return output;
                var sliderOptions = new VisualElement();
                sliderOptions.Add(VRCFuryEditorUtils.Prop(null, "Default X",
                    fieldOverride: new AxisSlider(prop.FindPropertyRelative("defaultX"), -1, 1)));
                sliderOptions.Add(VRCFuryEditorUtils.Prop(null, "Default Y",
                    fieldOverride: new AxisSlider(prop.FindPropertyRelative("defaultY"), -1, 1)));
                output.Add(MakeTabbed("This puppet has defaults", sliderOptions));

                return output;
            }, setDefaultsProp));
            // Tags
            content.Add(VRCFuryEditorUtils.RefreshOnChange(() => {
                var tags = new List<string>();
                if (savedProp.boolValue)
                    tags.Add("Saved");
                
                var row = new VisualElement().Row().FlexWrap();
                foreach (var tag in tags) {
                    var flag = new Label(tag).Padding(2,4);
                    flag.style.width = StyleKeyword.Auto;
                    flag.style.backgroundColor = new Color(1f, 1f, 1f, 0.1f);
                    flag.style.borderTopRightRadius = 5;
                    flag.style.marginRight = 5;
                    row.Add(flag);
                }

                return row;
                },
                savedProp
            ));

            return content;
        }

        private static VisualElement MakeTabbed(string label, VisualElement child) {
            var output = new VisualElement();
            output.Add(VRCFuryEditorUtils.WrappedLabel(label).Bold());
            var tabbed = new VisualElement { style = { paddingLeft = 10 } };
            tabbed.Add(child);
            output.Add(tabbed);
            return output;
        }
    }

}