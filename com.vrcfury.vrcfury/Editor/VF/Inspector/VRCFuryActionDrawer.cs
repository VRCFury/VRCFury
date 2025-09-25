using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using VF.Builder;
using VF.Feature.Base;
using VF.Model.StateAction;
using VF.Utils;

namespace VF.Inspector {

    [CustomPropertyDrawer(typeof(VF.Model.StateAction.Action))]
    internal class VRCFuryActionDrawer : PropertyDrawer {
        public override VisualElement CreatePropertyGUI(SerializedProperty prop) {
            var el = new VisualElement();
            el.AddToClassList("vfAction");
            el.Add(Render(prop));
            return el;
        }

        private static VisualElement Render(SerializedProperty prop) {
            var col = new VisualElement();
            
            var el = RenderInner(prop);
            col.Add(el);
            
            var desktopActive = prop.FindPropertyRelative("desktopActive");
            var androidActive = prop.FindPropertyRelative("androidActive");
            col.AddManipulator(new ContextualMenuManipulator(e => {
                if (e.menu.MenuItems().OfType<DropdownMenuAction>().Any(i => i.name == "Desktop Only")) {
                    return;
                }
                if (e.menu.MenuItems().Count > 0) {
                    e.menu.AppendSeparator();
                }
                e.menu.AppendAction("Desktop Only", a => {
                    desktopActive.boolValue = !desktopActive.boolValue;
                    androidActive.boolValue = false;
                    prop.serializedObject.ApplyModifiedProperties();
                }, desktopActive.boolValue ? DropdownMenuAction.Status.Checked : DropdownMenuAction.Status.Normal);
                e.menu.AppendAction("Quest+Android+iOS Only", a => {
                    androidActive.boolValue = !androidActive.boolValue;
                    desktopActive.boolValue = false;
                    prop.serializedObject.ApplyModifiedProperties();
                }, androidActive.boolValue ? DropdownMenuAction.Status.Checked : DropdownMenuAction.Status.Normal);
            }));
            
            col.Add(VRCFuryEditorUtils.RefreshOnChange(() => {
                var row = new VisualElement().Row().FlexWrap();

                void AddFlag(string tag) {
                    var flag = new Label(tag);
                    flag.style.width = StyleKeyword.Auto;
                    flag.style.backgroundColor = new Color(1f, 1f, 1f, 0.1f);
                    flag.style.borderTopRightRadius = 5;
                    flag.style.marginRight = 5;
                    flag.Padding(2, 4);
                    row.Add(flag);
                }
                
                if (desktopActive.boolValue) AddFlag("Desktop Only");
                if (androidActive.boolValue) AddFlag("Quest+Android+iOS Only");

                return row;
            }, desktopActive, androidActive));

            return col;
        }
        
        private static VisualElement RenderInner(SerializedProperty prop) {
            return FeatureFinder.RenderFeatureEditor(prop, (title, body, builderType) => {
                var output = new VisualElement();
                if (builderType?.GetCustomAttribute<FeatureHideTitleInEditorAttribute>() == null)
                    output.Add(Title(title));
                output.Add(body);
                return output;
            });
        }

        public static VisualElement Title(string title) {
            return new Label(title).Bold();
        }
    }

}
